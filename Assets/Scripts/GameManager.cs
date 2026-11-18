using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameState { Title, Playing, Paused, Ending }

/// Boots the whole game from an empty scene and runs the high-level loop:
/// title -> skating across regions -> meeting friends -> Nana's cabin -> ending.
public class GameManager : MonoBehaviour
{
    public static GameManager I;
    public GameState State { get; private set; } = GameState.Title;

    [HideInInspector] public Skater skater;
    [HideInInspector] public FollowCamera camFollow;
    [HideInInspector] public SeasonWeather seasons;
    Synth synth;
    GameUI ui;

    public int Region { get; private set; } = -1;
    public int Vibes { get; private set; }
    public int Combo { get; private set; } = 1;
    public int BestCombo { get; private set; } = 1;
    public int FriendsMet { get; private set; }
    public int TotalFriends { get; private set; }
    public float PlayTime { get; private set; }
    public float Progress01 => skater == null ? 0 : Mathf.Clamp01(skater.transform.position.z / WorldBuilder.NanaZ);
    public bool ReachedNana { get; private set; }

    // dialogue
    public bool InDialogue => talking != null;
    public string DlgSpeaker { get; private set; }
    public string DlgText { get; private set; }
    public int DlgShown { get; private set; }
    readonly Queue<string> lineQueue = new Queue<string>();
    Creature talking;
    float revealAcc, holdTimer, comboTimer, splashCooldown;
    bool lineDone;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => I = null;

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        Application.targetFrameRate = 120;

        var cam = Camera.main;
        if (cam == null)
        {
            var cg = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = cg.AddComponent<Camera>();
            cg.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Palette.Hex("#bfe4ff");
        cam.nearClipPlane = .1f; cam.farClipPlane = 600f;
        if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();

        // The synth lives on its own object: OnAudioFilterRead can't share a GameObject with the AudioListener.
        var synthGo = new GameObject("Synth");
        synthGo.AddComponent<AudioSource>();
        synth = synthGo.AddComponent<Synth>();

        Light sun = null;
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null) { sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional; }
        sun.transform.rotation = Quaternion.Euler(48, -35, 0);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = .55f;
        RenderSettings.sun = sun;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 60; RenderSettings.fogEndDistance = 240;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.75f, .78f, .82f);

        seasons = new GameObject("Seasons").AddComponent<SeasonWeather>();
        SeasonWeather.I = seasons;
        var builder = new GameObject("WorldBuilder").AddComponent<WorldBuilder>();
        builder.Generate(seasons);

        skater = new GameObject("Ollie").AddComponent<Skater>();
        skater.model = OllieModel.Create(skater.transform);
        skater.Place(new Vector3(0, 0, 6), 0);
        skater.InputEnabled = false;
        skater.OnTrick += HandleTrick;
        skater.OnBail += HandleBail;

        seasons.Init(sun, cam, skater.transform);

        camFollow = cam.gameObject.GetComponent<FollowCamera>();
        if (camFollow == null) camFollow = cam.gameObject.AddComponent<FollowCamera>();
        camFollow.target = skater;
        camFollow.titleOrbit = true;
        camFollow.Snap();

        ui = gameObject.AddComponent<GameUI>();

        int n = 0;
        foreach (var c in Creature.All) if (!c.isGrandma) n++;
        TotalFriends = n;
        SetRegion(0, false);
    }

    void StartGame()
    {
        State = GameState.Playing;
        camFollow.titleOrbit = false;
        skater.InputEnabled = true;
        synth.titleScreen = false;
        ui.ShowControls(14f);
        Synth.Sfx(SfxType.Click);
        ui.Banner(WorldBuilder.RegionNames[0], SeasonLine(0));
    }

    string SeasonLine(int r) => SeasonWeather.SeasonNames[(int)seasons.SeasonOf(r)] + "  ·  " + SeasonWeather.WeatherNames[(int)seasons.CurrentWeather];

    void SetRegion(int r, bool announce)
    {
        if (r == Region) return;
        Region = r;
        seasons.SetActiveRegion(r);
        synth.region = r;
        if (announce)
        {
            ui.Banner(WorldBuilder.RegionNames[r], SeasonLine(r));
            Synth.Sfx(SfxType.Region);
            skater.SetCheckpoint(new Vector3(0, 0, r * WorldBuilder.RegionLen + 6), 0);
        }
    }

    void Update()
    {
        // pause toggling works in any gameplay state
        if (Inp.Pause && (State == GameState.Playing || State == GameState.Paused)) TogglePause();

        switch (State)
        {
            case GameState.Title:
                if (Inp.Confirm) StartGame();
                break;
            case GameState.Paused:
                if (Inp.Down(UnityEngine.InputSystem.Key.R)) Restart();
                if (Inp.Down(UnityEngine.InputSystem.Key.Q)) Quit();
                break;
            case GameState.Playing:
                PlayTime += Time.deltaTime;
                TickPlaying();
                break;
            case GameState.Ending:
                if (Inp.Down(UnityEngine.InputSystem.Key.R)) Restart();
                else if (Inp.Confirm) { State = GameState.Playing; skater.InputEnabled = true; Synth.Sfx(SfxType.Click); }
                break;
        }

        synth.paused = State == GameState.Paused;
        synth.grounded = skater.Grounded;
        synth.grinding = skater.Grinding;
        synth.rollSpeed01 = skater.Grounded && !skater.Grinding ? skater.Speed01 : 0f;
        synth.speed01 = skater.Speed01;
        if (Inp.Down(UnityEngine.InputSystem.Key.H)) ui.ToggleControls();
    }

    void TickPlaying()
    {
        var p = skater.transform.position;
        SetRegion(WorldBuilder.RegionAt(p.z), true);

        if (skater.Grounded && !skater.Grinding)
        {
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0 && Combo > 1) Combo = 1;
        }

        splashCooldown -= Time.deltaTime;
        if (splashCooldown <= 0 && Mathf.Abs(p.z - WorldBuilder.StreamZ) < 2f && skater.Grounded)
        {
            Synth.Sfx(SfxType.Splash); splashCooldown = 2f;
        }

        if (talking != null) TickDialogue();
        else
        {
            foreach (var c in Creature.All)
            {
                if (c.met) continue;
                var d = c.transform.position - p; d.y = 0;
                float range = c.isGrandma ? 8f : 7f;
                if (d.magnitude < range) { StartDialogue(c); break; }
            }
        }
    }

    // ---------------------------------------------------------------- tricks
    void HandleTrick(string name, int pts)
    {
        int gained = pts * Combo;
        Vibes += gained;
        ui.Popup(name, gained, Combo);
        Combo = Mathf.Min(Combo + 1, 10);
        BestCombo = Mathf.Max(BestCombo, Combo);
        comboTimer = 2.2f;
    }

    void HandleBail()
    {
        Combo = 1;
        ui.Popup("Bail!", 0, 0);
    }

    // ---------------------------------------------------------------- dialogue
    void StartDialogue(Creature c)
    {
        talking = c;
        c.met = true;
        lineQueue.Clear();
        foreach (var l in c.lines) lineQueue.Enqueue(l);
        var call = c.GetComponent<CallTag>();
        if (call != null) Synth.Sfx(call.call);
        if (c.isGrandma)
        {
            ReachedNana = true;
            skater.InputEnabled = false;
            var dir = c.transform.position - skater.transform.position;
            skater.Place(skater.transform.position, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
        }
        NextLine();
    }

    void NextLine()
    {
        if (lineQueue.Count == 0) { EndDialogue(); return; }
        var raw = lineQueue.Dequeue();
        int bar = raw.IndexOf('|');
        DlgSpeaker = bar > 0 ? raw.Substring(0, bar) : talking.displayName;
        DlgText = bar > 0 ? raw.Substring(bar + 1) : raw;
        DlgShown = 0; revealAcc = 0; holdTimer = 0; lineDone = false;
    }

    void TickDialogue()
    {
        bool ollie = DlgSpeaker == "Ollie";
        if (!lineDone)
        {
            revealAcc += Time.deltaTime * 42f;
            int target = Mathf.Min(DlgText.Length, Mathf.FloorToInt(revealAcc));
            while (DlgShown < target)
            {
                DlgShown++;
                char ch = DlgText[DlgShown - 1];
                if (DlgShown % 2 == 0 && char.IsLetter(ch))
                {
                    float pitch = ollie ? 560f : 330f * talking.voicePitch;
                    Synth.Voice(pitch, ollie ? 3 : (talking.displayName.Length % 2 == 0 ? 1 : 2));
                }
            }
            if (DlgShown >= DlgText.Length) lineDone = true;
            if (Inp.Advance) { DlgShown = DlgText.Length; lineDone = true; }
        }
        else
        {
            holdTimer += Time.deltaTime;
            if (holdTimer > 1.4f + DlgText.Length * .02f || Inp.Advance) NextLine();
        }
    }

    void EndDialogue()
    {
        var c = talking;
        talking = null;
        DlgSpeaker = null; DlgText = null;
        if (c.isGrandma)
        {
            State = GameState.Ending;
            Synth.Sfx(SfxType.Chime);
            return;
        }
        FriendsMet++;
        Vibes += 150;
        Synth.Sfx(SfxType.Chime);
        ui.Popup("New friend: " + c.displayName, 150, 0);
    }

    // ---------------------------------------------------------------- menus
    public void TogglePause()
    {
        if (State == GameState.Playing) { State = GameState.Paused; Time.timeScale = 0f; }
        else if (State == GameState.Paused) { State = GameState.Playing; Time.timeScale = 1f; }
        Synth.Sfx(SfxType.Click);
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
