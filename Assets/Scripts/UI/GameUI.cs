using System.Collections.Generic;
using UnityEngine;

/// All on-screen UI, drawn with IMGUI so it needs no scene setup.
public class GameUI : MonoBehaviour
{
    class Pop { public string text; public float t; public Color c; }
    readonly List<Pop> pops = new List<Pop>();
    string bannerTitle, bannerSub; float bannerT;
    float controlsT; bool controlsPinned;

    Texture2D white;
    GUIStyle title, sub, body, small, big, speaker, popStyle, keyStyle;
    Font font;
    float scale = 1;

    static readonly Color Ink = Palette.Hex("#2b2533");
    static readonly Color Cream = Palette.Hex("#fff8ec");
    static readonly Color Accent = Palette.Hex("#ff8a3d");
    static readonly Color[] RegionCol = { Palette.Hex("#3b6df2"), Palette.Hex("#57c27a"), Palette.Hex("#e0a030"), Palette.Hex("#4f7a6a") };

    public void Popup(string text, int pts, int combo)
    {
        string s = pts > 0 ? $"{text}  +{pts}" : text;
        if (combo > 1) s += $"  x{combo}";
        pops.Add(new Pop { text = s, t = 0, c = text == "Bail!" ? Palette.Hex("#ff5d5d") : text.StartsWith("New friend") ? Palette.Hex("#57c27a") : Accent });
        if (pops.Count > 5) pops.RemoveAt(0);
    }

    public void Banner(string t, string s) { bannerTitle = t; bannerSub = s; bannerT = 0; }
    public void ShowControls(float seconds) => controlsT = seconds;
    public void ToggleControls() { controlsPinned = !controlsPinned; controlsT = 0; }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        for (int i = pops.Count - 1; i >= 0; i--) { pops[i].t += dt; if (pops[i].t > 2.2f) pops.RemoveAt(i); }
        bannerT += dt;
        controlsT -= dt;
    }

    void Styles()
    {
        float s = Screen.height / 1080f;
        if (title != null && Mathf.Approximately(s, scale)) return;
        scale = s;
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Avenir Next","Helvetica Neue", "Arial" }, 32);
        GUIStyle Make(int size, FontStyle fs, TextAnchor a) =>
            new GUIStyle { font = font, fontSize = Mathf.RoundToInt(size * s), fontStyle = fs, alignment = a, wordWrap = true, richText = true, normal = { textColor = Color.white } };
        title = Make(96, FontStyle.Bold, TextAnchor.MiddleCenter);
        sub = Make(34, FontStyle.Normal, TextAnchor.MiddleCenter);
        big = Make(56, FontStyle.Bold, TextAnchor.MiddleCenter);
        body = Make(30, FontStyle.Normal, TextAnchor.UpperLeft);
        small = Make(24, FontStyle.Normal, TextAnchor.UpperLeft);
        speaker = Make(28, FontStyle.Bold, TextAnchor.UpperLeft);
        popStyle = Make(38, FontStyle.Bold, TextAnchor.MiddleCenter);
        keyStyle = Make(24, FontStyle.Bold, TextAnchor.MiddleCenter); keyStyle.wordWrap = false;
    }

    void Rect(Rect r, Color c) { var old = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = old; }

    void Label(Rect r, string text, GUIStyle st, Color c, bool shadow = true)
    {
        if (shadow)
        {
            var sc = st.normal.textColor;
            st.normal.textColor = new Color(0, 0, 0, .35f * c.a);
            GUI.Label(new Rect(r.x + 3 * scale, r.y + 3 * scale, r.width, r.height), text, st);
            st.normal.textColor = sc;
        }
        var old = st.normal.textColor; st.normal.textColor = c;
        GUI.Label(r, text, st);
        st.normal.textColor = old;
    }

    void Panel(Rect r, float alpha = .82f)
    {
        Rect(new Rect(r.x + 6 * scale, r.y + 6 * scale, r.width, r.height), new Color(0, 0, 0, .18f * alpha));
        Rect(r, new Color(Ink.r, Ink.g, Ink.b, alpha));
    }

    void OnGUI()
    {
        var gm = GameManager.I;
        if (gm == null) return;
        Styles();
        float W = Screen.width, H = Screen.height, s = scale;

        switch (gm.State)
        {
            case GameState.Title: DrawTitle(W, H, s); return;
            case GameState.Ending: DrawHud(gm, W, H, s); DrawEnding(gm, W, H, s); return;
        }

        DrawHud(gm, W, H, s);
        if (gm.State == GameState.Paused) DrawPause(W, H, s);
    }

    void DrawTitle(float W, float H, float s)
    {
        Rect(new Rect(0, 0, W, H), new Color(0, 0, 0, .12f));
        float bob = Mathf.Sin(Time.unscaledTime * 2f) * 6f * s;
        Label(new Rect(0, H * .16f + bob, W, 120 * s), "Ollie's Long Way to Nana's", title, Cream);
        Label(new Rect(0, H * .16f + 110 * s, W, 50 * s), "a cozy skate through the city, the suburbs, the farms and the forest", sub, Cream);
        float a = .6f + .4f * Mathf.Sin(Time.unscaledTime * 3f);
        Label(new Rect(0, H * .8f, W, 60 * s), "Press  Space / Enter  to start skating", big, new Color(1, 1, 1, a));
        Label(new Rect(0, H * .8f + 70 * s, W, 40 * s), "headphones recommended  ·  every sound is made live in code", sub, new Color(1, 1, 1, .8f));
    }

    void DrawHud(GameManager gm, float W, float H, float s)
    {
        var sw = SeasonWeather.I;
        int r = Mathf.Max(0, gm.Region);
        // top-left: where & when
        var tl = new Rect(24 * s, 24 * s, 420 * s, 104 * s);
        Panel(tl, .72f);
        Rect(new Rect(tl.x, tl.y, 8 * s, tl.height), RegionCol[r]);
        Label(new Rect(tl.x + 24 * s, tl.y + 12 * s, tl.width, 40 * s), WorldBuilder.RegionNames[r], speaker, Cream, false);
        string season = sw != null ? SeasonWeather.SeasonNames[(int)sw.SeasonOf(r)] + "  ·  " + SeasonWeather.WeatherNames[(int)sw.CurrentWeather] : "";
        Label(new Rect(tl.x + 24 * s, tl.y + 54 * s, tl.width, 36 * s), season, small, new Color(1, 1, 1, .85f), false);
        if (sw != null) Rect(new Rect(tl.x + 24 * s, tl.y + 90 * s, (tl.width - 48 * s) * sw.SeasonProgress, 4 * s), new Color(1, 1, 1, .5f));

        // top-right: vibes & friends
        var tr = new Rect(W - 344 * s, 24 * s, 320 * s, 104 * s);
        Panel(tr, .72f);
        Label(new Rect(tr.x + 22 * s, tr.y + 10 * s, tr.width, 44 * s), $"Vibes  <b>{gm.Vibes:N0}</b>" + (gm.Combo > 1 ? $"   <color=#ffb547>x{gm.Combo}</color>" : ""), body, Cream, false);
        Label(new Rect(tr.x + 22 * s, tr.y + 56 * s, tr.width, 36 * s), $"Friends met  <b>{gm.FriendsMet}/{gm.TotalFriends}</b>", small, new Color(1, 1, 1, .85f), false);

        // top-center: journey progress
        float pw = Mathf.Min(560 * s, W - 860 * s);
        if (pw > 160 * s)
        {
            var pr = new Rect((W - pw) / 2, 40 * s, pw, 14 * s);
            Rect(new Rect(pr.x - 4 * s, pr.y - 4 * s, pr.width + 8 * s, pr.height + 8 * s), new Color(Ink.r, Ink.g, Ink.b, .6f));
            for (int i = 0; i < 4; i++)
            {
                float x0 = pr.x + pr.width * (i * WorldBuilder.RegionLen / WorldBuilder.NanaZ);
                float x1 = pr.x + pr.width * Mathf.Min(1, (i + 1) * WorldBuilder.RegionLen / WorldBuilder.NanaZ);
                Rect(new Rect(x0, pr.y, x1 - x0 - 2 * s, pr.height), new Color(RegionCol[i].r, RegionCol[i].g, RegionCol[i].b, .45f));
            }
            Rect(new Rect(pr.x, pr.y, pr.width * gm.Progress01, pr.height), new Color(1, 1, 1, .75f));
            Label(new Rect(pr.x + pr.width * gm.Progress01 - 60 * s, pr.y + 18 * s, 120 * s, 30 * s), "Ollie", keyStyle, Cream);
            Label(new Rect(pr.x + pr.width - 60 * s, pr.y - 38 * s, 120 * s, 30 * s), "Nana", keyStyle, Cream);
        }

        // region banner
        if (bannerTitle != null && bannerT < 4.5f)
        {
            float a = Mathf.Clamp01(bannerT * 2f) * Mathf.Clamp01((4.5f - bannerT) * 1.5f);
            Label(new Rect(0, H * .2f, W, 110 * s), bannerTitle, title, new Color(Cream.r, Cream.g, Cream.b, a));
            Label(new Rect(0, H * .2f + 100 * s, W, 50 * s), bannerSub, sub, new Color(1, 1, 1, a * .9f));
        }

        // trick popups
        for (int i = 0; i < pops.Count; i++)
        {
            var p = pops[i];
            float a = Mathf.Clamp01((2.2f - p.t) * 2f);
            float y = H * .42f - i * 50 * s - p.t * 30 * s;
            Label(new Rect(0, y, W, 50 * s), p.text, popStyle, new Color(p.c.r, p.c.g, p.c.b, a));
        }

        // dialogue box
        if (gm.InDialogue && gm.DlgText != null)
        {
            float bw = Mathf.Min(1100 * s, W - 80 * s);
            var box = new Rect((W - bw) / 2, H - 230 * s, bw, 170 * s);
            Panel(box, .88f);
            bool ollie = gm.DlgSpeaker == "Ollie";
            var tag = new Rect(box.x + 24 * s, box.y - 22 * s, 260 * s, 44 * s);
            Rect(tag, ollie ? Palette.Hex("#ffb547") : RegionCol[Mathf.Max(0, gm.Region)]);
            Label(new Rect(tag.x + 16 * s, tag.y + 6 * s, tag.width, tag.height), gm.DlgSpeaker, speaker, ollie ? Ink : Cream, false);
            Label(new Rect(box.x + 32 * s, box.y + 40 * s, box.width - 64 * s, box.height - 50 * s), gm.DlgText.Substring(0, gm.DlgShown), body, Cream, false);
            Label(new Rect(box.xMax - 220 * s, box.yMax - 40 * s, 200 * s, 30 * s), "E  to skip", small, new Color(1, 1, 1, .45f), false);
        }

        // controls card
        if (controlsPinned || controlsT > 0)
        {
            float a = controlsPinned ? 1 : Mathf.Clamp01(controlsT);
            var cr = new Rect(24 * s, H - 430 * s, 430 * s, 400 * s);
            if (gm.InDialogue) cr.y = 150 * s;
            Panel(cr, .78f * a);
            string[,] rows = {
                { "W / S", "push · brake" }, { "A / D", "carve  (in air: spin)" }, { "Space", "ollie · jump off rails" },
                { "J", "kickflip" }, { "K", "heelflip" }, { "L", "shove-it" }, { "Shift", "manual" },
                { "Land on rails", "grind" }, { "Esc", "pause · H toggles this" } };
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                float y = cr.y + 20 * s + i * 41 * s;
                Label(new Rect(cr.x + 20 * s, y, 170 * s, 36 * s), $"<b>{rows[i, 0]}</b>", small, new Color(1, .75f, .45f, a), false);
                Label(new Rect(cr.x + 190 * s, y, 240 * s, 36 * s), rows[i, 1], small, new Color(1, 1, 1, .9f * a), false);
            }
        }
        else
            Label(new Rect(24 * s, H - 50 * s, 400 * s, 30 * s), "H  controls", small, new Color(1, 1, 1, .5f));
    }

    void DrawPause(float W, float H, float s)
    {
        Rect(new Rect(0, 0, W, H), new Color(Ink.r, Ink.g, Ink.b, .55f));
        Label(new Rect(0, H * .3f, W, 110 * s), "Paused", title, Cream);
        Label(new Rect(0, H * .3f + 130 * s, W, 50 * s), "Esc  resume      R  restart      Q  quit", sub, Cream);
    }

    void DrawEnding(GameManager gm, float W, float H, float s)
    {
        Rect(new Rect(0, 0, W, H), new Color(Ink.r, Ink.g, Ink.b, .55f));
        Label(new Rect(0, H * .16f, W, 110 * s), "You made it to Nana's!", title, Cream);
        int mins = Mathf.FloorToInt(gm.PlayTime / 60f), secs = Mathf.FloorToInt(gm.PlayTime % 60f);
        string stats = $"Vibes  <b>{gm.Vibes:N0}</b>\nFriends met  <b>{gm.FriendsMet} / {gm.TotalFriends}</b>\nBest combo  <b>x{gm.BestCombo}</b>\nJourney time  <b>{mins}:{secs:00}</b>";
        Label(new Rect(0, H * .36f, W, 260 * s), stats, sub, Cream);
        string tip = gm.FriendsMet < gm.TotalFriends ? "Some friends are still out there. Skate back and say hi!" : "You met everyone. Nana is very proud.";
        Label(new Rect(0, H * .66f, W, 50 * s), tip, sub, new Color(1, .85f, .6f));
        Label(new Rect(0, H * .78f, W, 60 * s), "Enter  keep skating        R  ride again", big, Cream);
    }
}
