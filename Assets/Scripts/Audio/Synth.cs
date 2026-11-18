using System.Collections.Concurrent;
using UnityEngine;

public enum SfxType { Pop, Land, Flip, Bail, Push, Bonk, Honk, TractorHorn, BikeBell, GrindStart, Chime, Click, Splash, Moo, Woof, Quack, Hoot, Cluck, Oink, Baa, Meow, Squeak, Region }

/// Everything you hear is synthesized here, sample by sample, in OnAudioFilterRead.
/// Main thread -> audio thread communication goes through a lock-free queue and volatile fields.
[RequireComponent(typeof(AudioSource))]
public class Synth : MonoBehaviour
{
    public static Synth I;

    struct Ev { public int kind; public SfxType t; public float a, b; }
    static readonly ConcurrentQueue<Ev> queue = new ConcurrentQueue<Ev>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { while (queue.TryDequeue(out _)) { } I = null; }

    public static void Sfx(SfxType t, float a = 1f, float b = 0f) => queue.Enqueue(new Ev { kind = 0, t = t, a = a, b = b });
    public static void TrickNote(int combo) => queue.Enqueue(new Ev { kind = 1, a = combo });
    public static void Voice(float pitch, float wave) => queue.Enqueue(new Ev { kind = 2, a = pitch, b = wave });

    // ---- state written by the game ----
    public volatile int region, season, weather;     // weather: 0 clear 1 rain 2 windy 3 snow 4 leaves
    public volatile float rollSpeed01, speed01;
    public volatile bool grounded = true, grinding, paused, titleScreen = true;
    public float masterVolume = .85f;

    // ---- voices ----
    const int KICK = 0, SNARE = 1, HAT = 2, BASS = 3, KEYS = 4, PAD = 5, PLUCK = 6, BELL = 7, SWEEP = 8, NOISE = 9;
    class V
    {
        public bool on, music;
        public int type, wave;
        public float t, dur, f, f1, amp, gl, gr, ph, ph2, lp, cut;
        public readonly float[] ks = new float[2400];
        public int ksLen, ksI;
    }
    const int MaxVoices = 64;
    readonly V[] voices = new V[MaxVoices];

    int sr = 48000;
    float dt;
    uint rng = 22222;
    double clock;

    // ---- music ----
    class Style
    {
        public float bpm, swing;
        public int[][] chords; public int[] roots;
        public string kick, snare, hat, bass, keys;
        public int keysType;        // 0 stabs, 1 sustained EP, 2 pluck arpeggio, 3 pad
        public int bassWave;        // 0 sine, 1 saw
        public bool brush, bells;
        public int[] penta;
    }
    Style[] styles;
    int curStyle = 0, step;
    double stepAcc, stepLen;
    int arpI;

    // continuous layer state
    float rollLp, grindLp1, grindLp2, rainLp, windLp, humLp, windPhase, gust, clackTimer, birdTimer, mlpL, mlpR, musicDuck = 1f;

    void Awake()
    {
        I = this;
        sr = AudioSettings.outputSampleRate;
        dt = 1f / sr;
        for (int i = 0; i < MaxVoices; i++) voices[i] = new V();
        BuildStyles();
        stepLen = StepSamples(styles[0], 0);

        var src = GetComponent<AudioSource>();
        // A silent looping clip keeps the filter callback running.
        src.clip = AudioClip.Create("silence", sr, 1, sr, false);
        src.loop = true;
        src.spatialBlend = 0;
        src.Play();
    }

    void BuildStyles()
    {
        styles = new Style[4];
        styles[0] = new Style
        { // City: funky, D dorian
            bpm = 100, swing = .06f,
            chords = new[] { new[] { 62, 65, 69, 72 }, new[] { 59, 62, 65, 67 }, new[] { 59, 62, 64, 67 }, new[] { 61, 64, 67, 69 } },
            roots = new[] { 38, 43, 40, 45 },
            kick = "x..x..x...x..x..", snare = "....x.......x...", hat = "xxxxxxxxxxxxxxxx",
            bass = "x..x..o.x..x.o..", keys = "..x...x.....x..x", keysType = 0, bassWave = 1,
        };
        styles[1] = new Style
        { // Suburbs: lazy lo-fi, F major
            bpm = 84, swing = .2f,
            chords = new[] { new[] { 57, 60, 64, 65 }, new[] { 55, 59, 62, 64 }, new[] { 53, 57, 60, 64 }, new[] { 55, 59, 60, 64 } },
            roots = new[] { 41, 40, 38, 36 },
            kick = "x.......x.x.....", snare = "....x.......x...", hat = "x.x.x.x.x.x.x.x.",
            bass = "x.........x.....", keys = "x.......x.......", keysType = 1, bassWave = 0, brush = true,
        };
        styles[2] = new Style
        { // Farms: boom-chick bluegrass, G major
            bpm = 116, swing = 0f,
            chords = new[] { new[] { 55, 59, 62, 67 }, new[] { 55, 60, 64, 67 }, new[] { 57, 62, 66, 69 }, new[] { 55, 59, 62, 67 } },
            roots = new[] { 43, 48, 50, 43 },
            kick = "x.......x.......", snare = "....x.......x...", hat = "..x...x...x...x.",
            bass = "x...f...x...f...", keys = "x.x.x.x.x.x.x.x.", keysType = 2, bassWave = 0, brush = true,
        };
        styles[3] = new Style
        { // Forest & mountains: ambient, A minor pentatonic
            bpm = 70, swing = .1f,
            chords = new[] { new[] { 57, 60, 64, 69 }, new[] { 53, 57, 60, 65 }, new[] { 55, 60, 64, 67 }, new[] { 55, 59, 62, 67 } },
            roots = new[] { 45, 41, 48, 43 },
            kick = "x...............", snare = "................", hat = "................",
            bass = "x...............", keys = "x...............", keysType = 3, bassWave = 0, bells = true,
            penta = new[] { 69, 72, 74, 76, 79, 81, 84 },
        };
    }

    double StepSamples(Style s, int stepIndex)
    {
        double baseLen = sr * 60.0 / s.bpm / 4.0;
        return stepIndex % 2 == 0 ? baseLen * (1 + s.swing) : baseLen * (1 - s.swing);
    }

    static float Mtof(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);
    float Noise() { rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; return rng / 2147483648f - 1f; }
    float Rand01() => Noise() * .5f + .5f;
    float Coef(float hz) => 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Min(hz, sr * .45f) / sr);

    V Alloc(int type, float dur, float amp, bool music, float pan = 0, float delay = 0)
    {
        V best = null; float oldest = -1;
        for (int i = 0; i < MaxVoices; i++)
        {
            var v = voices[i];
            if (!v.on) { best = v; break; }
            if (v.t > oldest) { oldest = v.t; best = v; }
        }
        best.on = true; best.music = music; best.type = type; best.dur = dur; best.amp = amp;
        best.t = -delay; best.ph = best.ph2 = best.lp = 0; best.wave = 0; best.cut = 1;
        best.gl = Mathf.Sqrt(.5f * (1 - pan)); best.gr = Mathf.Sqrt(.5f * (1 + pan));
        return best;
    }

    // ---------- instruments ----------
    void Kick(float amp) => Alloc(KICK, .45f, amp, true);
    void Snare(float amp, bool brush) { var v = Alloc(SNARE, .3f, amp, true, -.15f); v.wave = brush ? 1 : 0; }
    void Hat(float amp) => Alloc(HAT, .08f, amp, true, .3f);
    void Bass(int midi, float dur, float amp, int wave) { var v = Alloc(BASS, dur, amp, true); v.f = Mtof(midi); v.wave = wave; v.cut = Coef(wave == 1 ? 700 : 2000); }
    void Keys(int midi, float dur, float amp, float pan) { var v = Alloc(KEYS, dur, amp, true, pan); v.f = Mtof(midi); }
    void Pad(int midi, float dur, float amp, float pan) { var v = Alloc(PAD, dur, amp, true, pan); v.f = Mtof(midi); v.cut = Coef(900); }
    void Bell(float hz, float amp, float decay, bool music, float pan = 0, float delay = 0) { var v = Alloc(BELL, 4f / decay + .05f, amp, music, pan, delay); v.f = hz; v.cut = decay; }
    void Sweep(float f0, float f1, float dur, float amp, int wave, float pan = 0, float delay = 0) { var v = Alloc(SWEEP, dur, amp, false, pan, delay); v.f = f0; v.f1 = f1; v.wave = wave; }
    void NoiseBurst(float dur, float amp, float cut0, float cut1, bool highpass, float delay = 0) { var v = Alloc(NOISE, dur, amp, false, 0, delay); v.f = cut0; v.f1 = cut1; v.wave = highpass ? 1 : 0; }
    void Pluck(float hz, float amp, bool music, float pan = 0)
    {
        var v = Alloc(PLUCK, 1.6f, amp, music, pan);
        v.ksLen = Mathf.Clamp(Mathf.RoundToInt(sr / hz), 2, v.ks.Length);
        v.ksI = 0;
        float lp = 0;
        for (int i = 0; i < v.ksLen; i++) { lp += (Noise() - lp) * .6f; v.ks[i] = lp; }
    }

    // ---------- sequencer ----------
    void OnStep()
    {
        int bar = step / 16, s = step % 16;
        if (s == 0 && bar == 0 && region != curStyle) { curStyle = Mathf.Clamp(region, 0, styles.Length - 1); }
        var st = styles[curStyle];
        var chord = st.chords[bar];
        int root = st.roots[bar];
        float energy = titleScreen ? .5f : .35f + .65f * speed01;
        float beat = 60f / st.bpm;

        if (st.kick[s] == 'x') Kick(curStyle == 3 ? .35f : .8f);
        if (st.snare[s] == 'x') Snare(st.brush ? .28f : .45f, st.brush);
        if (st.hat[s] == 'x') Hat(.12f * energy * (s % 4 == 2 ? 1.3f : 1f));
        // extra shaker when going fast
        if (speed01 > .55f && s % 2 == 1 && curStyle != 3) Hat(.05f * speed01);

        char b = st.bass[s];
        if (b != '.')
        {
            int n = b == 'o' ? root + 12 : b == 'f' ? root + 7 : root;
            float len = curStyle == 0 ? beat * .4f : curStyle == 2 ? beat * .8f : beat * (curStyle == 3 ? 3.8f : 2.4f);
            Bass(n, len, curStyle == 3 ? .22f : .3f, st.bassWave);
        }

        if (st.keys[s] == 'x')
        {
            switch (st.keysType)
            {
                case 0: for (int i = 0; i < chord.Length; i++) Keys(chord[i] + 12, beat * .35f, .08f, -.3f + i * .2f); break;
                case 1: for (int i = 0; i < chord.Length; i++) Keys(chord[i], beat * 3.8f, .07f, -.3f + i * .2f); break;
                case 2: Pluck(Mtof(chord[arpI++ % chord.Length] + 12), .22f, true, (arpI % 2 == 0) ? -.3f : .3f); break;
                case 3: for (int i = 0; i < chord.Length; i++) Pad(chord[i], beat * 4.2f, .05f, -.4f + i * .27f); break;
            }
        }
        if (st.bells && s % 2 == 0 && Rand01() < .3f)
            Bell(Mtof(st.penta[(int)(Rand01() * st.penta.Length) % st.penta.Length]), .07f, 2.5f, true, Noise() * .6f);

        // grinding adds a bright arpeggio on top of whatever is playing
        if (grinding && s % 2 == 0)
            Pluck(Mtof(chord[(s / 2) % chord.Length] + 24), .12f, true, .2f);
    }

    // ---------- events ----------
    void Handle(Ev e)
    {
        if (e.kind == 1)
        {
            var chord = styles[curStyle].chords[(step / 16) % 4];
            int c = Mathf.Max(0, (int)e.a - 1);
            int note = chord[c % chord.Length] + 12 * (1 + Mathf.Min(c / chord.Length, 1));
            Bell(Mtof(note), .2f, 3f, false, .1f);
            Pluck(Mtof(note), .18f, false, -.1f);
            return;
        }
        if (e.kind == 2)
        {
            float f = e.a * (.85f + Rand01() * .4f);
            Sweep(f, f * (.8f + Rand01() * .5f), .065f, .11f, (int)e.b);
            return;
        }
        switch (e.t)
        {
            case SfxType.Pop: NoiseBurst(.05f, .35f, 6000, 2000, true); Sweep(700, 220, .07f, .3f, 0); break;
            case SfxType.Land: NoiseBurst(.14f, .25f + .35f * e.a, 2500, 200, false); Sweep(120, 50, .12f, .3f + .3f * e.a, 0); break;
            case SfxType.Flip: NoiseBurst(.22f, .12f, 500, 4000, true); Sweep(500, 1100, .18f, .06f, 3); break;
            case SfxType.Bail:
                Sweep(700, 140, .55f, .25f, 0); NoiseBurst(.35f, .3f, 3000, 300, false);
                Sweep(1200, 300, .7f, .05f, 1, 0, .1f); break;
            case SfxType.Push: NoiseBurst(.16f, .18f, 900, 250, false); break;
            case SfxType.Bonk: Sweep(320, 180, .12f, .25f, 3); break;
            case SfxType.Honk: Sweep(392, 392, .16f, .07f, 1, -.4f); Sweep(494, 494, .16f, .05f, 1, -.4f); Sweep(392, 392, .22f, .07f, 1, -.4f, .22f); Sweep(494, 494, .22f, .05f, 1, -.4f, .22f); break;
            case SfxType.TractorHorn: Sweep(196, 190, .5f, .1f, 2, .4f); Sweep(233, 226, .5f, .07f, 2, .4f); break;
            case SfxType.BikeBell: Bell(2400, .12f, 18, false, .3f); Bell(2400, .12f, 18, false, .3f, .13f); break;
            case SfxType.GrindStart: NoiseBurst(.08f, .25f, 8000, 3000, true); Bell(1480, .1f, 12, false); break;
            case SfxType.Chime:
                { var ch = styles[curStyle].chords[(step / 16) % 4]; for (int i = 0; i < 4; i++) Bell(Mtof(ch[i % ch.Length] + 24), .12f, 4, false, -.3f + i * .2f, i * .09f); }
                break;
            case SfxType.Click: Sweep(1300, 1000, .035f, .1f, 0); break;
            case SfxType.Splash: NoiseBurst(.5f, .3f, 4000, 300, false); Sweep(900, 300, .25f, .05f, 0); break;
            case SfxType.Moo: Sweep(150, 115, .8f, .12f, 2); Sweep(152, 118, .8f, .06f, 3); break;
            case SfxType.Woof: Sweep(380, 200, .12f, .14f, 1); Sweep(380, 200, .12f, .14f, 1, 0, .2f); break;
            case SfxType.Quack: Sweep(650, 520, .13f, .1f, 2); Sweep(650, 520, .13f, .1f, 2, 0, .18f); break;
            case SfxType.Hoot: Sweep(420, 400, .28f, .14f, 0); Sweep(420, 380, .4f, .14f, 0, 0, .38f); break;
            case SfxType.Cluck: for (int i = 0; i < 3; i++) Sweep(900, 600, .06f, .1f, 3, 0, i * .1f); break;
            case SfxType.Oink: Sweep(250, 320, .15f, .14f, 1); Sweep(250, 300, .15f, .12f, 1, 0, .2f); break;
            case SfxType.Baa: Sweep(520, 480, .6f, .1f, 2); break;
            case SfxType.Meow: Sweep(600, 900, .15f, .1f, 3); Sweep(900, 500, .3f, .1f, 3, 0, .15f); break;
            case SfxType.Squeak: Sweep(1800, 2600, .08f, .1f, 0); Sweep(2000, 2800, .08f, .1f, 0, 0, .12f); break;
            case SfxType.Region: { var ch = styles[Mathf.Clamp(region, 0, 3)].chords[0]; for (int i = 0; i < 4; i++) Bell(Mtof(ch[i] + 12), .1f, 2.5f, false, -.3f + i * .2f, i * .14f); } break;
        }
    }

    // ---------- render ----------
    float RenderVoice(V v)
    {
        v.t += dt;
        if (v.t < 0) return 0;
        if (v.t > v.dur) { v.on = false; return 0; }
        float x = v.t, s = 0;
        const float TAU = Mathf.PI * 2f;
        switch (v.type)
        {
            case KICK:
                {
                    float f = 48f + 120f * Mathf.Exp(-x * 30f);
                    v.ph += f * dt; if (v.ph > 1) v.ph -= 1;
                    s = Mathf.Sin(TAU * v.ph) * Mathf.Exp(-x * 8f) * v.amp;
                    break;
                }
            case SNARE:
                {
                    float n = Noise(); v.lp += (n - v.lp) * .45f;
                    float tone = Mathf.Sin(TAU * 190f * x) * Mathf.Exp(-x * 28f);
                    s = ((n - v.lp) * Mathf.Exp(-x * (v.wave == 1 ? 9f : 20f)) + tone * .5f) * v.amp;
                    break;
                }
            case HAT:
                {
                    float n = Noise(); v.lp += (n - v.lp) * .55f;
                    s = (n - v.lp) * Mathf.Exp(-x * 45f) * v.amp;
                    break;
                }
            case BASS:
                {
                    v.ph += v.f * dt; if (v.ph > 1) v.ph -= 1;
                    float w = v.wave == 1 ? (2f * v.ph - 1f) * .7f + Mathf.Sin(TAU * v.ph) * .5f : Mathf.Sin(TAU * v.ph) + .15f * Mathf.Sin(TAU * 2 * v.ph);
                    v.lp += (w - v.lp) * v.cut;
                    float env = Mathf.Min(1f, x / .006f) * (.65f + .35f * Mathf.Exp(-x * 5f)) * Mathf.Clamp01((v.dur - x) / .06f);
                    s = v.lp * env * v.amp;
                    break;
                }
            case KEYS:
                {
                    v.ph += v.f * dt; if (v.ph > 1) v.ph -= 1;
                    float w = Mathf.Sin(TAU * v.ph) + .3f * Mathf.Sin(TAU * 2 * v.ph) * Mathf.Exp(-x * 3f) + .12f * Mathf.Sin(TAU * 4 * v.ph) * Mathf.Exp(-x * 9f);
                    float env = Mathf.Min(1f, x / .004f) * Mathf.Exp(-x * 1.1f) * Mathf.Clamp01((v.dur - x) / .08f) * (1f + .12f * Mathf.Sin(TAU * 4.5f * x));
                    s = w * env * v.amp;
                    break;
                }
            case PAD:
                {
                    v.ph += v.f * dt; if (v.ph > 1) v.ph -= 1;
                    v.ph2 += v.f * 1.0065f * dt; if (v.ph2 > 1) v.ph2 -= 1;
                    float w = (2f * v.ph - 1f) + (2f * v.ph2 - 1f);
                    v.lp += (w - v.lp) * v.cut;
                    float env = Mathf.Clamp01(x / .9f) * Mathf.Clamp01((v.dur - x) / 1.2f);
                    s = v.lp * env * v.amp;
                    break;
                }
            case PLUCK:
                {
                    float o = v.ks[v.ksI];
                    int n = v.ksI + 1; if (n >= v.ksLen) n = 0;
                    v.ks[v.ksI] = (o + v.ks[n]) * .5f * .996f;
                    v.ksI = n;
                    s = o * v.amp * 1.6f;
                    break;
                }
            case BELL:
                {
                    v.ph += v.f * dt; if (v.ph > 1) v.ph -= 1;
                    float p = TAU * v.ph;
                    float w = Mathf.Sin(p) + .4f * Mathf.Sin(p * 2.76f) * Mathf.Exp(-x * 4f) + .2f * Mathf.Sin(p * 5.4f) * Mathf.Exp(-x * 9f);
                    s = w * Mathf.Min(1f, x / .002f) * Mathf.Exp(-x * v.cut) * v.amp;
                    break;
                }
            case SWEEP:
                {
                    float f = v.f * Mathf.Pow(v.f1 / v.f, x / v.dur);
                    v.ph += f * dt; if (v.ph > 1) v.ph -= 1;
                    float w;
                    switch (v.wave)
                    {
                        case 1: w = v.ph < .5f ? 1 : -1; break;
                        case 2: w = 2f * v.ph - 1f; break;
                        case 3: w = 1f - 4f * Mathf.Abs(v.ph - .5f); break;
                        default: w = Mathf.Sin(TAU * v.ph); break;
                    }
                    float env = Mathf.Min(1f, x / .004f) * (1f - x / v.dur);
                    s = w * env * v.amp;
                    break;
                }
            case NOISE:
                {
                    float n = Noise();
                    float cut = Mathf.Lerp(v.f, v.f1, x / v.dur);
                    v.lp += (n - v.lp) * Coef(cut);
                    float k = 1f - x / v.dur;
                    s = (v.wave == 1 ? n - v.lp : v.lp) * k * k * v.amp;
                    break;
                }
        }
        return s;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        while (queue.TryDequeue(out var e)) Handle(e);

        bool raining = weather == 1;
        float musicLpCoef = Coef(raining ? 1800f : 18000f);
        bool isPaused = paused;
        float rs = rollSpeed01;
        bool rolling = grounded && !grinding && rs > .02f && !isPaused;
        bool grind = grinding && !isPaused;
        int w = weather, reg = region, sea = season;

        for (int i = 0; i < data.Length; i += channels)
        {
            // sequencer clock
            stepAcc += 1;
            if (stepAcc >= stepLen)
            {
                stepAcc -= stepLen;
                step = (step + 1) % 64;
                OnStep();
                stepLen = StepSamples(styles[curStyle], step);
            }
            clock += dt;

            float mL = 0, mR = 0, sL = 0, sR = 0;
            for (int v = 0; v < MaxVoices; v++)
            {
                var vo = voices[v];
                if (!vo.on) continue;
                float s = RenderVoice(vo);
                if (vo.music) { mL += s * vo.gl; mR += s * vo.gr; }
                else { sL += s * vo.gl; sR += s * vo.gr; }
            }

            // music bus: duck when paused, muffle when it rains
            musicDuck += ((isPaused ? .35f : 1f) - musicDuck) * .0002f;
            mlpL += (mL - mlpL) * musicLpCoef; mlpR += (mR - mlpR) * musicLpCoef;
            float L = mlpL * musicDuck, R = mlpR * musicDuck;

            if (!isPaused)
            {
                float amb = 0;
                // wheels on the ground
                if (rolling)
                {
                    rollLp += (Noise() - rollLp) * Coef(200 + rs * 900);
                    amb += rollLp * (.25f + .5f * rs) * .5f;
                    clackTimer -= dt;
                    if (clackTimer <= 0 && reg <= 1) { clackTimer = 1.6f / Mathf.Max(rs * 14f, 1f); NoiseBurst(.03f, .08f, 3000, 800, true); }
                }
                // grinding: bandpassed metal scrape
                if (grind)
                {
                    float n = Noise();
                    grindLp1 += (n - grindLp1) * Coef(4200);
                    grindLp2 += (n - grindLp2) * Coef(1400);
                    amb += (grindLp1 - grindLp2) * .35f + Mathf.Sin(Mathf.PI * 2f * 1760f * (float)clock) * .015f;
                }
                // wind (windy, snow, leaves) - gusty lowpassed noise
                if (w >= 2 || reg == 3)
                {
                    windPhase += dt * .23f;
                    gust = .5f + .5f * Mathf.Sin(windPhase * 6.28f) * Mathf.Sin(windPhase * 2.1f);
                    float strength = w == 2 ? .22f : w == 3 ? .1f : w == 4 ? .14f : .06f;
                    windLp += (Noise() - windLp) * Coef(180 + 700 * gust);
                    amb += windLp * strength * (.4f + gust);
                }
                // rain
                if (w == 1)
                {
                    rainLp += (Noise() - rainLp) * Coef(2600);
                    amb += rainLp * .13f;
                    if (Rand01() < .0009f) Bell(2000 + Rand01() * 2500, .02f, 40, false, Noise());
                }
                // city hum
                if (reg == 0)
                {
                    humLp += (Noise() - humLp) * Coef(110);
                    amb += humLp * .12f;
                }
                // birds in spring/summer outside the city
                if ((sea == 0 || sea == 1) && reg > 0 && w != 1)
                {
                    birdTimer -= dt;
                    if (birdTimer <= 0)
                    {
                        birdTimer = 1.5f + Rand01() * 4f;
                        float f0 = 2400 + Rand01() * 1600; float pan = Noise() * .8f;
                        int chirps = 2 + (int)(Rand01() * 3);
                        for (int c = 0; c < chirps; c++) Sweep(f0, f0 * 1.35f, .07f, .035f, 0, pan, c * .11f);
                    }
                }
                L += amb + sL; R += amb + sR;
            }
            else { L += sL; R += sR; }

            L = Soft(L * masterVolume); R = Soft(R * masterVolume);
            data[i] = L;
            if (channels > 1) data[i + 1] = R;
            for (int c = 2; c < channels; c++) data[i + c] = 0;
        }
    }

    static float Soft(float x) => x / (1f + Mathf.Abs(x));
}
