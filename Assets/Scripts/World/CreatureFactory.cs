using UnityEngine;

/// Builds the "so like a ___, but it's a ___" creatures out of primitives.
public static class CreatureFactory
{
    public struct Q
    {
        public float bodyL, bodyH, bodyW, legL, legW, neckL, neckTilt, headS, snoutL, scale;
        public Color col, belly, dark, accent;
        public int ears;     // 0 none, 1 round, 2 tall, 3 pointy, 4 floppy, 5 tiny
        public int tail;     // 0 none, 1 thin, 2 fluffy, 3 curly, 4 tuft, 5 ringed
        public int pattern;  // 0 none, 1 stripes, 2 spots, 3 patches
        public int horns;    // 0 none, 1 horns, 2 antlers, 3 tusks, 4 ossicones
        public bool mane;
    }

    static Creature Root(Transform parent, Vector3 pos, float yaw, string name, int region, float pitch, string[] lines)
    {
        var go = new GameObject("Creature_" + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        var c = go.AddComponent<Creature>();
        c.displayName = name; c.region = region; c.voicePitch = pitch; c.lines = lines;
        return c;
    }

    public static Creature Quadruped(Transform parent, Vector3 pos, float yaw, string name, int region, float pitch, Q q, string[] lines)
    {
        var c = Root(parent, pos, yaw, name, region, pitch, lines);
        var t = Build.Group(c.transform, "Body", Vector3.zero);
        t.localScale = Vector3.one * (q.scale <= 0 ? 1 : q.scale);
        float bodyY = q.legL + q.bodyH * .4f;

        // legs
        foreach (float x in new[] { -1f, 1f })
            foreach (float z in new[] { -1f, 1f })
            {
                Build.Cyl(t, new Vector3(x * q.bodyW * .3f, q.legL / 2f, z * q.bodyL * .32f), new Vector3(q.legW, q.legL / 2f, q.legW), q.col);
                Build.Ball(t, new Vector3(x * q.bodyW * .3f, .03f, z * q.bodyL * .32f + .02f), new Vector3(q.legW * 1.3f, q.legW * .7f, q.legW * 1.5f), q.dark);
            }
        // body
        Build.Ball(t, new Vector3(0, bodyY, 0), new Vector3(q.bodyW, q.bodyH, q.bodyL), q.col);
        Build.Ball(t, new Vector3(0, bodyY - q.bodyH * .18f, 0), new Vector3(q.bodyW * .8f, q.bodyH * .7f, q.bodyL * .82f), q.belly);

        // pattern
        if (q.pattern == 1)
            for (int i = -3; i <= 3; i++)
            {
                float zf = i / 4.2f;
                float k = Mathf.Sqrt(Mathf.Max(0, 1 - zf * zf));
                Build.Ball(t, new Vector3(0, bodyY + .01f, zf * q.bodyL * .5f), new Vector3(q.bodyW * k * 1.04f, q.bodyH * k * 1.04f, .07f * q.bodyL), q.dark);
            }
        else if (q.pattern == 2 || q.pattern == 3)
        {
            var rnd = new System.Random(name.GetHashCode());
            int n = q.pattern == 2 ? 14 : 5;
            for (int i = 0; i < n; i++)
            {
                var d = new Vector3((float)rnd.NextDouble() * 2 - 1, (float)rnd.NextDouble() * .9f + .1f, (float)rnd.NextDouble() * 2 - 1).normalized;
                var p = new Vector3(d.x * q.bodyW * .49f, bodyY + d.y * q.bodyH * .49f, d.z * q.bodyL * .49f);
                float s = q.pattern == 2 ? .12f * q.bodyW : .35f * q.bodyW;
                Build.Ball(t, p, new Vector3(s, s * .7f, s * 1.2f), q.dark);
            }
        }

        // neck + head
        var neck = Build.Group(t, "Neck", new Vector3(0, bodyY + q.bodyH * .2f, q.bodyL * .38f));
        neck.localRotation = Quaternion.Euler(q.neckTilt, 0, 0);
        if (q.neckL > .05f) Build.Cyl(neck, new Vector3(0, q.neckL / 2f, 0), new Vector3(q.headS * .45f, q.neckL / 2f, q.headS * .45f), q.col);
        if (q.mane) Build.Box(neck, new Vector3(0, q.neckL * .5f, -q.headS * .22f), new Vector3(.06f, q.neckL * .95f + .1f, .12f), q.dark, false);
        if (q.pattern == 2 && q.neckL > .3f)
            for (int i = 0; i < 4; i++) Build.Ball(neck, new Vector3((i % 2 == 0 ? .1f : -.1f) * q.headS, q.neckL * (.2f + i * .2f), .1f * q.headS), new Vector3(.12f, .14f, .1f) * q.headS * 1.5f, q.dark);

        var head = Build.Group(neck, "Head", new Vector3(0, q.neckL, 0));
        head.localRotation = Quaternion.Euler(-q.neckTilt, 0, 0);
        Build.Ball(head, new Vector3(0, 0, q.headS * .1f), Vector3.one * q.headS, q.col);
        if (q.snoutL > 0)
        {
            Build.Ball(head, new Vector3(0, -q.headS * .15f, q.headS * .35f + q.snoutL * .4f), new Vector3(q.headS * .6f, q.headS * .5f, q.headS * .5f + q.snoutL), q.belly);
            Build.Ball(head, new Vector3(0, -q.headS * .05f, q.headS * .55f + q.snoutL * .9f), Vector3.one * q.headS * .16f, q.dark);
        }
        Eyes(head, q.headS, q.headS * .42f);

        switch (q.ears)
        {
            case 1: foreach (int s in new[] { -1, 1 }) Build.Ball(head, new Vector3(s * q.headS * .38f, q.headS * .42f, 0), new Vector3(.32f, .32f, .1f) * q.headS, q.col); break;
            case 2: foreach (int s in new[] { -1, 1 }) Build.Cap(head, new Vector3(s * q.headS * .2f, q.headS * .75f, -q.headS * .05f), new Vector3(.16f, .35f, .1f) * q.headS * 1.4f, q.col, false, Quaternion.Euler(0, 0, s * -12)); break;
            case 3: foreach (int s in new[] { -1, 1 }) Build.Prim(PrimitiveType.Cube, head, new Vector3(s * q.headS * .3f, q.headS * .45f, 0), new Vector3(.18f, .18f, .06f) * q.headS * 1.3f, Palette.Get(q.col), false, Quaternion.Euler(0, 0, 45)); break;
            case 4: foreach (int s in new[] { -1, 1 }) Build.Cap(head, new Vector3(s * q.headS * .48f, -q.headS * .05f, 0), new Vector3(.15f, .3f, .12f) * q.headS * 1.5f, q.dark, false, Quaternion.Euler(0, 0, s * 20)); break;
            case 5: foreach (int s in new[] { -1, 1 }) Build.Ball(head, new Vector3(s * q.headS * .25f, q.headS * .48f, 0), new Vector3(.12f, .14f, .06f) * q.headS, q.col); break;
        }
        switch (q.horns)
        {
            case 1: foreach (int s in new[] { -1, 1 }) Build.Cap(head, new Vector3(s * q.headS * .18f, q.headS * .55f, -q.headS * .1f), new Vector3(.08f, .2f, .08f) * q.headS * 1.5f, q.accent, false, Quaternion.Euler(-25, 0, s * -15)); break;
            case 2:
                foreach (int s in new[] { -1, 1 })
                {
                    var a = Build.Group(head, "Antler", new Vector3(s * q.headS * .2f, q.headS * .5f, -q.headS * .05f));
                    a.localRotation = Quaternion.Euler(-15, 0, s * -30);
                    Build.Cyl(a, new Vector3(0, .25f, 0) * q.headS, new Vector3(.06f, .25f, .06f) * q.headS, q.accent);
                    Build.Cyl(a, new Vector3(s * .1f, .4f, .05f) * q.headS, new Vector3(.05f, .14f, .05f) * q.headS, q.accent, false, Quaternion.Euler(0, 0, s * -50));
                    Build.Cyl(a, new Vector3(-s * .06f, .55f, 0) * q.headS, new Vector3(.05f, .12f, .05f) * q.headS, q.accent, false, Quaternion.Euler(0, 0, s * 35));
                }
                break;
            case 3: foreach (int s in new[] { -1, 1 }) Build.Cap(head, new Vector3(s * q.headS * .2f, -q.headS * .15f, q.headS * .5f + q.snoutL), new Vector3(.06f, .16f, .06f) * q.headS * 1.5f, q.accent, false, Quaternion.Euler(0, 0, s * -20)); break;
            case 4: foreach (int s in new[] { -1, 1 }) { Build.Cyl(head, new Vector3(s * q.headS * .15f, q.headS * .6f, 0), new Vector3(.07f, .14f, .07f) * q.headS, q.col); Build.Ball(head, new Vector3(s * q.headS * .15f, q.headS * .76f, 0), Vector3.one * q.headS * .11f, q.dark); } break;
        }

        // tail
        var tail = Build.Group(t, "Tail", new Vector3(0, bodyY + q.bodyH * .15f, -q.bodyL * .48f));
        switch (q.tail)
        {
            case 1: tail.localRotation = Quaternion.Euler(-30, 0, 0); Build.Cyl(tail, new Vector3(0, 0, -.25f) * q.bodyH, new Vector3(.05f, .25f, .05f) * q.bodyH, q.col, false, Quaternion.Euler(90, 0, 0)); break;
            case 2: Build.Ball(tail, new Vector3(0, q.bodyH * .35f, -q.bodyH * .2f), new Vector3(.45f, .8f, .45f) * q.bodyH, q.col); break;
            case 3: Build.Ball(tail, Vector3.zero, Vector3.one * q.bodyH * .18f, q.accent); break;
            case 4: tail.localRotation = Quaternion.Euler(20, 0, 0); Build.Cyl(tail, new Vector3(0, -.2f, -.05f) * q.bodyH, new Vector3(.04f, .2f, .04f) * q.bodyH, q.col); Build.Ball(tail, new Vector3(0, -.45f, -.08f) * q.bodyH, Vector3.one * q.bodyH * .12f, q.dark); break;
            case 5:
                tail.localRotation = Quaternion.Euler(-35, 0, 0);
                for (int i = 0; i < 5; i++)
                    Build.Ball(tail, new Vector3(0, 0, -.12f - i * .12f) * q.bodyH * 1.6f, new Vector3(.2f, .2f, .22f) * q.bodyH * (1.2f - i * .08f), i % 2 == 0 ? q.col : q.dark);
                break;
        }
        return c;
    }

    public struct B
    {
        public float bodyS, legL, neckL, beakL, beakW, scale;
        public Color col, wing, beak, beakTip, legs;
        public bool crest, bigEyes, round;
    }

    public static Creature Bird(Transform parent, Vector3 pos, float yaw, string name, int region, float pitch, B b, string[] lines)
    {
        var c = Root(parent, pos, yaw, name, region, pitch, lines);
        var t = Build.Group(c.transform, "Body", Vector3.zero);
        t.localScale = Vector3.one * (b.scale <= 0 ? 1 : b.scale);
        foreach (int s in new[] { -1, 1 })
        {
            Build.Cyl(t, new Vector3(s * b.bodyS * .18f, b.legL / 2f, 0), new Vector3(.05f * Mathf.Max(1, b.bodyS), b.legL / 2f, .05f * Mathf.Max(1, b.bodyS)), b.legs);
            Build.Box(t, new Vector3(s * b.bodyS * .18f, .02f, .06f), new Vector3(.12f, .03f, .18f) * Mathf.Max(1, b.bodyS * .8f), b.legs, false);
        }
        float bodyY = b.legL + b.bodyS * .45f;
        Build.Ball(t, new Vector3(0, bodyY, 0), b.round ? Vector3.one * b.bodyS : new Vector3(b.bodyS * .8f, b.bodyS * .8f, b.bodyS * 1.15f), b.col);
        foreach (int s in new[] { -1, 1 })
            Build.Ball(t, new Vector3(s * b.bodyS * .4f, bodyY + .02f, -b.bodyS * .05f), new Vector3(b.bodyS * .18f, b.bodyS * .55f, b.bodyS * .8f), b.wing);
        Build.Box(t, new Vector3(0, bodyY + b.bodyS * .1f, -b.bodyS * .55f), new Vector3(b.bodyS * .4f, .04f, b.bodyS * .4f), b.wing, false);

        var head = Build.Group(t, "Head", new Vector3(0, bodyY + b.bodyS * .35f + b.neckL, b.bodyS * .3f));
        if (b.neckL > .05f) Build.Cyl(t, new Vector3(0, bodyY + b.bodyS * .3f + b.neckL / 2f, b.bodyS * .3f), new Vector3(.12f * Mathf.Max(1, b.bodyS), b.neckL / 2f, .12f * Mathf.Max(1, b.bodyS)), b.col);
        float hs = b.bodyS * (b.round ? .75f : .55f);
        Build.Ball(head, Vector3.zero, Vector3.one * hs, b.col);
        Eyes(head, hs, hs * .44f, b.bigEyes ? 2.2f : 1f);
        // beak: a long tapered capsule, optional dark tip
        Build.Cap(head, new Vector3(0, -hs * .05f, hs * .4f + b.beakL * .5f), new Vector3(b.beakW, b.beakL * .5f, b.beakW * .9f), b.beak, false, Quaternion.Euler(95, 0, 0));
        if (b.beakTip != default) Build.Ball(head, new Vector3(0, -hs * .1f, hs * .4f + b.beakL * .95f), new Vector3(b.beakW * .7f, b.beakW * .6f, b.beakW * .7f), b.beakTip);
        if (b.crest) Build.Ball(head, new Vector3(0, hs * .5f, -hs * .1f), new Vector3(.08f, .3f, .25f) * hs * 2f, b.wing);
        return c;
    }

    static void Eyes(Transform head, float headS, float spread, float size = 1f)
    {
        foreach (int s in new[] { -1, 1 })
        {
            var e = s * spread * .62f;
            float sz = headS * .16f * size;
            if (size > 1.5f) Build.Ball(head, new Vector3(e, headS * .12f, headS * .38f), Vector3.one * sz * 1.25f, Color.white);
            Build.Ball(head, new Vector3(e, headS * .12f, headS * .42f), new Vector3(sz, sz * 1.1f, sz * .6f), Color.black);
            Build.Ball(head, new Vector3(e + sz * .15f, headS * .12f + sz * .2f, headS * .45f), Vector3.one * sz * .3f, Color.white);
        }
    }

    public static Creature Fish(Transform parent, Vector3 pos, float yaw, string name, int region, float pitch, string[] lines)
    {
        var c = Root(parent, pos, yaw, name, region, pitch, lines);
        c.floats = true;
        var t = Build.Group(c.transform, "Body", Vector3.zero);
        var o = Palette.Hex("#ff8c2a");
        Build.Ball(t, Vector3.zero, new Vector3(1.1f, 1.3f, 2f), o);
        Build.Ball(t, new Vector3(0, -.25f, .1f), new Vector3(.9f, .8f, 1.6f), Palette.Hex("#ffd27a"));
        Build.Box(t, new Vector3(0, 0, -1.3f), new Vector3(.08f, 1.2f, .9f), o, false, 0).transform.localRotation = Quaternion.Euler(0, 0, 0);
        Build.Box(t, new Vector3(0, .7f, -.2f), new Vector3(.06f, .5f, .9f), o, false);
        Eyes(t, 1.2f, .9f, 2f);
        Build.Ball(t, new Vector3(0, -.15f, .98f), new Vector3(.35f, .2f, .1f), Palette.Hex("#d9531e"));
        return c;
    }

    public static Creature Lizard(Transform parent, Vector3 pos, float yaw, string name, int region, float pitch, string[] lines)
    {
        var c = Root(parent, pos, yaw, name, region, pitch, lines);
        var t = Build.Group(c.transform, "Body", Vector3.zero);
        var g = Palette.Hex("#7fbf6a"); var d = Palette.Hex("#5d9a4c");
        Build.Ball(t, new Vector3(0, .3f, 0), new Vector3(.6f, .35f, 1.1f), g);
        Build.Ball(t, new Vector3(0, .26f, 0), new Vector3(.55f, .3f, 1f), Palette.Hex("#e7e3a4"));
        foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                Build.Cap(t, new Vector3(x * .32f, .12f, z * .35f), new Vector3(.14f, .14f, .14f), g, false, Quaternion.Euler(0, 0, x * 60));
        var head = Build.Group(t, "Head", new Vector3(0, .42f, .62f));
        Build.Ball(head, Vector3.zero, new Vector3(.42f, .36f, .5f), g);
        Eyes(head, .42f, .3f, 1.6f);
        Build.Cap(t, new Vector3(0, .25f, -.75f), new Vector3(.12f, .3f, .12f), d, false, Quaternion.Euler(80, 0, 0));
        // a tiny towel, because she's shell-less and shy
        Build.Box(t, new Vector3(0, .5f, -.05f), new Vector3(.62f, .05f, .7f), Palette.Hex("#ff9ec7"), false);
        return c;
    }

    /// Upright rat (used for Nana). Bushy squirrel tail, glasses, shawl.
    public static Creature Nana(Transform parent, Vector3 pos, float yaw, string[] lines)
    {
        var c = Root(parent, pos, yaw, "Nana Nibbles", 3, .8f, lines);
        c.isGrandma = true;
        var t = Build.Group(c.transform, "Body", Vector3.zero);
        t.localScale = Vector3.one * 1.1f;
        var fur = Palette.Hex("#b7a9a0"); var pink = Palette.Hex("#f4a6b4");
        Build.Ball(t, new Vector3(-.12f, .05f, .08f), new Vector3(.16f, .08f, .24f), pink);
        Build.Ball(t, new Vector3(.12f, .05f, .08f), new Vector3(.16f, .08f, .24f), pink);
        Build.Ball(t, new Vector3(0, .5f, 0), new Vector3(.62f, .8f, .55f), Palette.Hex("#8e6bbf"));   // dress
        Build.Ball(t, new Vector3(0, .75f, .02f), new Vector3(.66f, .3f, .58f), Palette.Hex("#f2c14e")); // shawl
        Build.Ball(t, new Vector3(-.36f, .62f, .1f), new Vector3(.12f, .12f, .12f), pink);
        Build.Ball(t, new Vector3(.36f, .62f, .1f), new Vector3(.12f, .12f, .12f), pink);
        var head = Build.Group(t, "Head", new Vector3(0, 1.08f, .02f));
        Build.Ball(head, Vector3.zero, new Vector3(.42f, .38f, .42f), fur);
        Build.Ball(head, new Vector3(0, -.04f, .24f), new Vector3(.22f, .2f, .32f), fur);
        Build.Ball(head, new Vector3(0, -.01f, .4f), Vector3.one * .07f, pink);
        Eyes(head, .42f, .3f);
        foreach (int s in new[] { -1, 1 })
        {
            Build.Ball(head, new Vector3(s * .19f, .22f, -.02f), new Vector3(.22f, .22f, .05f), fur);
            Build.Ball(head, new Vector3(s * .19f, .22f, 0), new Vector3(.15f, .15f, .03f), pink);
            Build.Cyl(head, new Vector3(s * .085f, .05f, .22f), new Vector3(.11f, .01f, .11f), Palette.Hex("#c9a227"), false, Quaternion.Euler(90, 0, 0)); // glasses
        }
        Build.Box(head, new Vector3(0, .05f, .22f), new Vector3(.06f, .015f, .015f), Palette.Hex("#c9a227"), false);
        Build.Ball(head, new Vector3(0, .2f, -.12f), new Vector3(.3f, .2f, .26f), Palette.Hex("#e8e3ea")); // grey bun
        // the big fluffy tail that makes everyone think she's a squirrel
        var tail = Build.Group(t, "Tail", new Vector3(0, .35f, -.35f));
        Build.Ball(tail, new Vector3(0, .35f, -.2f), new Vector3(.5f, .9f, .45f), Palette.Hex("#c98b5a"));
        Build.Ball(tail, new Vector3(0, .85f, -.05f), new Vector3(.42f, .5f, .42f), Palette.Hex("#d99d6c"));
        return c;
    }
}
