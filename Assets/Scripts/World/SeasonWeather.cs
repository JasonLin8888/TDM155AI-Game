using System.Collections.Generic;
using UnityEngine;

public enum Season { Spring, Summer, Autumn, Winter }
public enum Weather { Clear, Rain, Windy, Snow, Leaves }
public enum Role { Grass, FoliageA, FoliageB, Crop, Water }

/// Each region starts in a different season and keeps cycling through all four.
/// Seasons recolor shared "role" materials per region, and drive sky, fog, light and weather particles.
public class SeasonWeather : MonoBehaviour
{
    public const int Regions = 4;
    public static SeasonWeather I;
    public float seasonLength = 75f;
    public static readonly int[] StartSeason = { 0, 1, 2, 3 }; // city spring, suburbs summer, farms autumn, forest winter

    readonly Dictionary<(int, Role), Material> mats = new Dictionary<(int, Role), Material>();
    float clock;
    int[] seasonOf = new int[Regions];
    int[] prevSeason = new int[Regions];
    float[] blend = new float[Regions];

    public Weather CurrentWeather { get; private set; }
    public Season CurrentSeason { get; private set; }
    int activeRegion;
    float weatherTimer;

    ParticleSystem rain, snow, leaves, wind;
    Light sun;
    Camera cam;
    Transform follow;

    static readonly Color[] grass = { Palette.Hex("#8fd17a"), Palette.Hex("#a9cc5a"), Palette.Hex("#d6a95a"), Palette.Hex("#eef3fa") };
    static readonly Color[] folA = { Palette.Hex("#9be38a"), Palette.Hex("#4fa856"), Palette.Hex("#ef8a3a"), Palette.Hex("#f2f5fb") };
    static readonly Color[] folB = { Palette.Hex("#ffb7d0"), Palette.Hex("#6cc070"), Palette.Hex("#d9483b"), Palette.Hex("#4f7a6a") };
    static readonly Color[] crop = { Palette.Hex("#7cd36a"), Palette.Hex("#f2cf5b"), Palette.Hex("#f08a2e"), Palette.Hex("#e8edf5") };
    static readonly Color[] water = { Palette.Hex("#6ec6f0"), Palette.Hex("#4bb3e8"), Palette.Hex("#5a9fc4"), Palette.Hex("#bfe3f2") };
    static readonly Color[] sky = { Palette.Hex("#bfe4ff"), Palette.Hex("#9fd6ff"), Palette.Hex("#ffd9b0"), Palette.Hex("#d6dfeb") };
    static readonly Color[] sunCol = { Palette.Hex("#fff3e0"), Palette.Hex("#fff0c8"), Palette.Hex("#ffd2a0"), Palette.Hex("#e6eeff") };
    public static readonly string[] SeasonNames = { "Spring", "Summer", "Autumn", "Winter" };
    public static readonly string[] WeatherNames = { "Clear", "Rainy", "Breezy", "Snowing", "Falling leaves" };

    public Material Mat(int region, Role role)
    {
        if (!mats.TryGetValue((region, role), out var m))
        {
            m = Palette.NewMat(ColorFor(role, StartSeason[region]));
            mats[(region, role)] = m;
        }
        return m;
    }

    static Color ColorFor(Role r, int s)
    {
        switch (r)
        {
            case Role.Grass: return grass[s];
            case Role.FoliageA: return folA[s];
            case Role.FoliageB: return folB[s];
            case Role.Crop: return crop[s];
            default: return water[s];
        }
    }

    public Season SeasonOf(int region) => (Season)seasonOf[Mathf.Clamp(region, 0, Regions - 1)];

    public void Init(Light sunLight, Camera camera, Transform followTarget)
    {
        I = this;
        sun = sunLight; cam = camera; follow = followTarget;
        for (int r = 0; r < Regions; r++) { seasonOf[r] = prevSeason[r] = StartSeason[r]; blend[r] = 1; }
        rain = MakeParticles("Rain", Palette.Hex("#cfe6ff"), 900, .9f, .05f, new Vector3(0, -22, 0), true);
        snow = MakeParticles("Snow", Color.white, 500, 6f, .14f, new Vector3(0, -2.2f, 0), false);
        leaves = MakeParticles("Leaves", Palette.Hex("#ef8a3a"), 70, 6f, .22f, new Vector3(2.5f, -1.8f, 0), false);
        wind = MakeParticles("Wind", new Color(1, 1, 1, .5f), 60, 1.2f, .04f, new Vector3(0, 0, -26), true);
        var lc = leaves.main; lc.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex("#ef8a3a"), Palette.Hex("#d9483b"));
        var ln = leaves.noise; ln.enabled = true; ln.strength = 1.4f; ln.frequency = .4f;
        var sn = snow.noise; sn.enabled = true; sn.strength = .6f; sn.frequency = .3f;
        SetWeather(PickWeather(StartSeason[0]));
    }

    ParticleSystem MakeParticles(string name, Color c, float rate, float life, float size, Vector3 vel, bool stretch)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = life;
        main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(size * .6f, size * 1.3f);
        main.startColor = c;
        main.maxParticles = 4000;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var em = ps.emission; em.rateOverTime = rate;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(46, 1, 46);
        var v = ps.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.World;
        v.x = new ParticleSystem.MinMaxCurve(vel.x * .8f, vel.x * 1.2f);
        v.y = new ParticleSystem.MinMaxCurve(vel.y * .8f, vel.y * 1.2f);
        v.z = new ParticleSystem.MinMaxCurve(vel.z * .8f, vel.z * 1.2f);
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = Palette.Particle;
        if (stretch) { r.renderMode = ParticleSystemRenderMode.Stretch; r.lengthScale = 1f; r.velocityScale = .06f; }
        return ps;
    }

    static Weather PickWeather(int s)
    {
        float r = Random.value;
        switch (s)
        {
            case 0: return r < .45f ? Weather.Rain : r < .7f ? Weather.Windy : Weather.Clear;
            case 1: return r < .6f ? Weather.Clear : r < .85f ? Weather.Windy : Weather.Rain;
            case 2: return r < .5f ? Weather.Leaves : r < .75f ? Weather.Rain : Weather.Windy;
            default: return r < .7f ? Weather.Snow : Weather.Windy;
        }
    }

    void SetWeather(Weather w)
    {
        CurrentWeather = w;
        Toggle(rain, w == Weather.Rain);
        Toggle(snow, w == Weather.Snow);
        Toggle(leaves, w == Weather.Leaves || (w == Weather.Windy && CurrentSeason == Season.Autumn));
        Toggle(wind, w == Weather.Windy || w == Weather.Leaves);
        if (w == Weather.Windy && (CurrentSeason == Season.Spring))
        {
            var lc = leaves.main; lc.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex("#ffb7d0"), Color.white);
            Toggle(leaves, true);
        }
        else
        {
            var lc = leaves.main; lc.startColor = new ParticleSystem.MinMaxGradient(Palette.Hex("#ef8a3a"), Palette.Hex("#d9483b"));
        }
        weatherTimer = 35f + Random.value * 25f;
        if (Synth.I != null) Synth.I.weather = (int)w;
    }

    static void Toggle(ParticleSystem ps, bool on)
    {
        if (on && !ps.isPlaying) ps.Play();
        else if (!on && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void SetActiveRegion(int r)
    {
        if (r == activeRegion) return;
        activeRegion = r;
        CurrentSeason = (Season)seasonOf[r];
        SetWeather(PickWeather(seasonOf[r]));
    }

    public float SeasonProgress => (clock % seasonLength) / seasonLength;

    void Update()
    {
        if (GameManager.I != null && GameManager.I.State == GameState.Paused) return;
        clock += Time.deltaTime;
        int cycles = Mathf.FloorToInt(clock / seasonLength);
        for (int r = 0; r < Regions; r++)
        {
            int s = (StartSeason[r] + cycles) % 4;
            if (s != seasonOf[r]) { prevSeason[r] = seasonOf[r]; seasonOf[r] = s; blend[r] = 0; }
            if (blend[r] < 1)
            {
                blend[r] = Mathf.Min(1, blend[r] + Time.deltaTime / 5f);
                foreach (Role role in System.Enum.GetValues(typeof(Role)))
                    if (mats.TryGetValue((r, role), out var m))
                        m.color = Color.Lerp(ColorFor(role, prevSeason[r]), ColorFor(role, seasonOf[r]), blend[r]);
            }
        }

        var season = (Season)seasonOf[activeRegion];
        if (season != CurrentSeason) { CurrentSeason = season; SetWeather(PickWeather((int)season)); }
        weatherTimer -= Time.deltaTime;
        if (weatherTimer <= 0) SetWeather(PickWeather((int)season));

        // sky / fog / light
        int si = (int)season;
        var target = sky[si];
        if (CurrentWeather == Weather.Rain) target = Color.Lerp(target, Palette.Hex("#8f9aa8"), .55f);
        if (CurrentWeather == Weather.Snow) target = Color.Lerp(target, Color.white, .3f);
        if (cam != null)
        {
            cam.backgroundColor = Color.Lerp(cam.backgroundColor, target, Time.deltaTime * .8f);
            RenderSettings.fogColor = cam.backgroundColor;
        }
        float fogEnd = CurrentWeather == Weather.Rain ? 130f : CurrentWeather == Weather.Snow ? 110f : 240f;
        RenderSettings.fogEndDistance = Mathf.Lerp(RenderSettings.fogEndDistance, fogEnd, Time.deltaTime * .5f);
        RenderSettings.fogStartDistance = RenderSettings.fogEndDistance * .25f;
        RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, Color.Lerp(target, Color.white, .35f) * .75f, Time.deltaTime);
        if (sun != null)
        {
            sun.color = Color.Lerp(sun.color, sunCol[si], Time.deltaTime);
            sun.intensity = Mathf.Lerp(sun.intensity, CurrentWeather == Weather.Rain ? .7f : 1.25f, Time.deltaTime);
        }

        if (follow != null)
        {
            var p = follow.position;
            rain.transform.position = p + new Vector3(0, 14, 6);
            snow.transform.position = p + new Vector3(0, 9, 6);
            leaves.transform.position = p + new Vector3(-6, 8, 6);
            wind.transform.position = p + new Vector3(0, 2.5f, 26);
        }

        if (Synth.I != null) { Synth.I.season = si; Synth.I.weather = (int)CurrentWeather; }
    }
}
