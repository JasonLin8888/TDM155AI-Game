using UnityEngine;

/// Builds the whole journey at startup: City -> Suburbs -> Farms -> Forest & Mountains -> Nana's cabin.
/// The road runs along +Z. Each region is RegionLen long.
public class WorldBuilder : MonoBehaviour
{
    public const float RegionLen = 420f;
    public const float EndZ = RegionLen * 4f;
    public const float NanaZ = EndZ - 40f;
    public const float StreamZ = RegionLen * 3 + 150f;
    public static readonly string[] RegionNames = { "The City", "The Suburbs", "The Farms", "Forest & Mountains" };

    SeasonWeather sw;
    Transform root;
    System.Random rnd = new System.Random(7);

    float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
    Color Pick(params string[] hex) => Palette.Hex(hex[rnd.Next(hex.Length)]);
    static float Z0(int region) => region * RegionLen;

    public static int RegionAt(float z) => Mathf.Clamp(Mathf.FloorToInt(z / RegionLen), 0, 3);

    public void Generate(SeasonWeather seasons)
    {
        sw = seasons;
        root = new GameObject("World").transform;
        Bounds();
        City();
        Suburbs();
        Farms();
        Forest();
        for (int i = 1; i < 4; i++) Boundary(i);
    }

    // ------------------------------------------------------------------ shared
    void Bounds()
    {
        void Wall(Vector3 c, Vector3 s) { var g = new GameObject("Bound"); g.transform.SetParent(root); g.transform.position = c; g.AddComponent<BoxCollider>().size = s; }
        Wall(new Vector3(-75, 5, EndZ / 2), new Vector3(2, 30, EndZ + 40));
        Wall(new Vector3(75, 5, EndZ / 2), new Vector3(2, 30, EndZ + 40));
        Wall(new Vector3(0, 5, -12), new Vector3(160, 30, 2));
        Wall(new Vector3(0, 5, EndZ + 8), new Vector3(160, 30, 2));
    }

    void Ground(int region, Material groundMat, Color road, float roadW, bool dashes)
    {
        float z0 = Z0(region), mid = z0 + RegionLen / 2f;
        float extra = region == 3 ? 20f : 0f;
        var g = Build.Box(root, new Vector3(0, -.52f, mid + extra / 2), new Vector3(160, 1, RegionLen + extra), groundMat);
        g.name = "Ground" + region;
        Build.Box(root, new Vector3(0, -.5f, mid + extra / 2), new Vector3(roadW, 1, RegionLen + extra), road).name = "Road" + region;
        if (dashes)
            for (float z = z0 + 4; z < z0 + RegionLen; z += 8)
                Build.Box(root, new Vector3(0, .005f, z), new Vector3(.18f, .01f, 3.2f), Palette.Hex("#f4f1e6"), false);
        if (region == 0) Build.Box(root, new Vector3(0, -.48f, -6), new Vector3(160, 1, 12), groundMat); // behind start
    }

    Transform Tree(Transform p, int region, Vector3 pos, float s)
    {
        var t = Build.Group(p, "Tree", pos);
        Build.Cyl(t, new Vector3(0, 1.2f * s, 0), new Vector3(.28f, 1.2f, .28f) * s, Palette.Hex("#8a5a3c"), true);
        var fa = sw.Mat(region, Role.FoliageA);
        var fb = sw.Mat(region, Role.FoliageB);
        Build.Ball(t, new Vector3(0, 2.9f * s, 0), new Vector3(2.4f, 2.1f, 2.4f) * s, fa);
        Build.Ball(t, new Vector3(.6f * s, 3.5f * s, .3f * s), new Vector3(1.5f, 1.3f, 1.5f) * s, rnd.NextDouble() < .5 ? fb : fa);
        Build.Ball(t, new Vector3(-.6f * s, 2.6f * s, -.3f * s), new Vector3(1.4f, 1.2f, 1.4f) * s, fa);
        return t;
    }

    Transform Pine(Transform p, int region, Vector3 pos, float s)
    {
        var t = Build.Group(p, "Pine", pos, R(0, 90));
        Build.Cyl(t, new Vector3(0, .8f * s, 0), new Vector3(.3f, .8f, .3f) * s, Palette.Hex("#6b4a33"), true);
        var fb = sw.Mat(region, Role.FoliageB);
        var snowy = sw.Mat(region, Role.FoliageA);
        for (int i = 0; i < 3; i++)
        {
            float w = (2.6f - i * .75f) * s;
            var tier = Build.Prim(PrimitiveType.Cube, t, new Vector3(0, (1.8f + i * 1.25f) * s, 0), new Vector3(w * .7f, w * .7f, w * .7f), i == 2 ? snowy : fb, false,
                Quaternion.Euler(45, 0, 45));
            tier.transform.localScale = new Vector3(w * .72f, w * .5f, w * .72f);
        }
        return t;
    }

    Transform Roof(Transform p, Vector3 pos, float width, float length, Color c, float yaw = 0)
    {
        float d = width / Mathf.Sqrt(2f);
        var r = Build.Prim(PrimitiveType.Cube, p, pos, new Vector3(d, d, length), Palette.Get(c), true, Quaternion.Euler(0, yaw, 45));
        return r.transform;
    }

    void Lamp(Transform p, Vector3 pos)
    {
        Build.Cyl(p, pos + new Vector3(0, 2.2f, 0), new Vector3(.12f, 2.2f, .12f), Palette.Hex("#3d4250"), true);
        Build.Ball(p, pos + new Vector3(0, 4.5f, 0), Vector3.one * .45f, Palette.Emissive(Palette.Hex("#fff2b3"), 1.5f));
    }

    /// Grindable rail on short posts. Points are at the rail top, in world space.
    void GrindRail(Transform p, Vector3 a, Vector3 b, Color c, bool posts = true)
    {
        var g = new GameObject("Rail");
        g.transform.SetParent(p, false);
        var rail = g.AddComponent<Rail>(); rail.a = a; rail.b = b;
        var mid = (a + b) / 2f; var dir = b - a;
        var bar = Build.Prim(PrimitiveType.Cylinder, g.transform, mid - Vector3.up * .04f, new Vector3(.09f, dir.magnitude / 2f, .09f), Palette.Get(c), false,
            Quaternion.LookRotation(dir) * Quaternion.Euler(90, 0, 0));
        if (!posts) return;
        int n = Mathf.Max(2, Mathf.CeilToInt(dir.magnitude / 3f) + 1);
        for (int i = 0; i < n; i++)
        {
            var pp = Vector3.Lerp(a, b, i / (float)(n - 1));
            Build.Cyl(g.transform, new Vector3(pp.x, pp.y / 2f, pp.z), new Vector3(.07f, pp.y / 2f, .07f), c);
        }
    }

    /// Fence that doubles as a grind rail.
    void Fence(Transform p, Vector3 a, Vector3 b, Color c, float h = .7f)
    {
        var dir = (b - a);
        int n = Mathf.Max(2, Mathf.CeilToInt(dir.magnitude / 2.5f) + 1);
        for (int i = 0; i < n; i++)
        {
            var pp = Vector3.Lerp(a, b, i / (float)(n - 1));
            Build.Box(p, pp + Vector3.up * h / 2f, new Vector3(.12f, h, .12f), c, false);
        }
        Build.Box(p, (a + b) / 2f + Vector3.up * h * .45f, new Vector3(.05f, .08f, dir.magnitude), c, false, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
        var g = new GameObject("FenceRail"); g.transform.SetParent(p, false);
        var r = g.AddComponent<Rail>(); r.a = a + Vector3.up * h; r.b = b + Vector3.up * h;
        Build.Box(g.transform, (a + b) / 2f + Vector3.up * (h - .03f), new Vector3(.14f, .06f, dir.magnitude), c, false, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg);
    }

    GameObject Car(Transform p, Vector3 pos, float yaw, Color c)
    {
        var t = Build.Group(p, "Car", pos, yaw);
        var body = Build.Box(t, new Vector3(0, .65f, 0), new Vector3(1.9f, .8f, 3.8f), c);
        Build.Box(t, new Vector3(0, 1.3f, -.2f), new Vector3(1.7f, .6f, 2.1f), Color.Lerp(c, Color.white, .25f), false);
        Build.Box(t, new Vector3(0, 1.3f, .87f), new Vector3(1.55f, .45f, .04f), Palette.Hex("#bfe6ff"), false);
        foreach (float x in new[] { -.92f, .92f }) foreach (float z in new[] { -1.2f, 1.2f })
                Build.Cyl(t, new Vector3(x, .35f, z), new Vector3(.62f, .14f, .62f), Palette.Hex("#2b2b33"), false, Quaternion.Euler(0, 0, 90));
        Build.Ball(t, new Vector3(.6f, .7f, 1.9f), new Vector3(.3f, .2f, .1f), Palette.Emissive(Palette.Hex("#fff6c2"), 2f));
        Build.Ball(t, new Vector3(-.6f, .7f, 1.9f), new Vector3(.3f, .2f, .1f), Palette.Emissive(Palette.Hex("#fff6c2"), 2f));
        return t.gameObject;
    }

    GameObject Person(Transform p, Vector3 pos, Color shirt, bool bike)
    {
        var t = Build.Group(p, bike ? "Biker" : "Person", pos);
        var skin = Pick("#f2c9a0", "#c98d5e", "#8a5a3c", "#f7d9c4");
        Build.Cap(t, new Vector3(-.12f, .4f, 0), new Vector3(.18f, .4f, .18f), Palette.Hex("#3d4a6b"));
        Build.Cap(t, new Vector3(.12f, .4f, 0), new Vector3(.18f, .4f, .18f), Palette.Hex("#3d4a6b"));
        var body = Build.Cap(t, new Vector3(0, 1.15f, 0), new Vector3(.55f, .45f, .35f), shirt, true);
        Build.Ball(t, new Vector3(0, 1.75f, 0), Vector3.one * .38f, skin);
        Build.Ball(t, new Vector3(0, 1.88f, -.04f), new Vector3(.4f, .2f, .4f), Pick("#3b2a20", "#f2d16b", "#1e1e24", "#b5533c"));
        if (bike)
        {
            t.localScale = Vector3.one * .75f;
            foreach (float z in new[] { -.6f, .6f })
                Build.Cyl(t, new Vector3(0, .35f, z), new Vector3(.7f, .04f, .7f), Palette.Hex("#2b2b33"), false, Quaternion.Euler(0, 0, 90));
            Build.Box(t, new Vector3(0, .6f, 0), new Vector3(.08f, .08f, 1.2f), Pick("#e9584f", "#3b6df2", "#57c27a"), false);
        }
        return t.gameObject;
    }

    Obstacle MakeObstacle(GameObject g, ObstacleKind k, bool jumpable, Vector3? moveTo = null, float speed = 0, float pause = 1.5f)
    {
        var o = g.AddComponent<Obstacle>();
        o.kind = k; o.jumpable = jumpable;
        o.moveA = g.transform.position; o.moveB = moveTo ?? g.transform.position;
        o.moveSpeed = speed; o.pause = pause;
        return o;
    }

    void Cone(Vector3 pos)
    {
        var t = Build.Group(root, "Cone", pos);
        Build.Box(t, new Vector3(0, .03f, 0), new Vector3(.6f, .06f, .6f), Palette.Hex("#ff7a2e"));
        Build.Cyl(t, new Vector3(0, .35f, 0), new Vector3(.38f, .3f, .38f), Palette.Hex("#ff7a2e"), true);
        Build.Cyl(t, new Vector3(0, .4f, 0), new Vector3(.4f, .06f, .4f), Color.white);
        MakeObstacle(t.gameObject, ObstacleKind.Cone, true);
    }

    void Log(Vector3 pos, float length)
    {
        var t = Build.Group(root, "Log", pos);
        Build.Cyl(t, new Vector3(0, .38f, 0), new Vector3(.75f, length / 2f, .75f), Palette.Hex("#7a5236"), true, Quaternion.Euler(0, 0, 90));
        Build.Cyl(t, new Vector3(length / 2f + .01f, .38f, 0), new Vector3(.62f, .02f, .62f), Palette.Hex("#d9b48a"), false, Quaternion.Euler(0, 0, 90));
        Build.Ball(t, new Vector3(1.5f, .8f, 0), new Vector3(.5f, .3f, .5f), sw.Mat(3, Role.FoliageA));
        MakeObstacle(t.gameObject, ObstacleKind.Log, true);
    }

    void HayBale(Vector3 pos, bool obstacle = true)
    {
        var t = Build.Group(root, "Hay", pos, R(-10, 10));
        Build.Box(t, new Vector3(0, .35f, 0), new Vector3(1.6f, .7f, .9f), Palette.Hex("#e8c25a"));
        Build.Box(t, new Vector3(0, .35f, 0), new Vector3(1.62f, .72f, .08f), Palette.Hex("#c99a3a"), false);
        if (obstacle) MakeObstacle(t.gameObject, ObstacleKind.HayBale, true);
    }

    void Bin(Vector3 pos)
    {
        var t = Build.Group(root, "Bin", pos);
        Build.Box(t, new Vector3(0, .45f, 0), new Vector3(.7f, .9f, .7f), Pick("#3a8f5c", "#3b6df2", "#6b6f78"));
        Build.Box(t, new Vector3(0, .93f, 0), new Vector3(.76f, .08f, .76f), Palette.Hex("#2b2b33"), false);
        MakeObstacle(t.gameObject, ObstacleKind.Bin, true);
    }

    void Arch(int region, float z, Color c, string flavor)
    {
        var t = Build.Group(root, "Arch_" + flavor, new Vector3(0, 0, z));
        Build.Box(t, new Vector3(-6.5f, 3, 0), new Vector3(.6f, 6, .6f), c);
        Build.Box(t, new Vector3(6.5f, 3, 0), new Vector3(.6f, 6, .6f), c);
        Build.Box(t, new Vector3(0, 6.2f, 0), new Vector3(14, 1.2f, .6f), c, false);
        for (int i = 0; i < 7; i++)
            Build.Ball(t, new Vector3(-5.4f + i * 1.8f, 6.2f, -.35f), Vector3.one * .5f, Palette.Emissive(Palette.Hex("#fff2b3"), 1.2f));
    }

    // ------------------------------------------------------------------ skateparks
    void Park(int region, Vector3 origin, Color pad, Color ramp, Color rail, Color accent)
    {
        var t = Build.Group(root, "Skatepark_" + region, origin);
        Build.Box(t, new Vector3(0, -.49f, 30), new Vector3(34, 1, 64), pad).name = "ParkPad";
        // entry ramps from the road side are flat (pad is flush with ground)
        Build.Ramp(t, new Vector3(6, 0, 6), 0, 3, 2.2f, .7f, ramp);
        Build.Ramp(t, new Vector3(-7, 0, 8), 0, 3.5f, 3f, 1.3f, ramp);
        Build.Ramp(t, new Vector3(-7, 0, 16.5f), 180, 3.5f, 3f, 1.1f, ramp); // landing ramp

        // funbox with a rail on top
        Build.Ramp(t, new Vector3(0, 0, 22), 0, 4, 2.6f, 1f, ramp);
        Build.Box(t, new Vector3(0, .5f, 26.6f), new Vector3(4, 1, 4), ramp);
        Build.Ramp(t, new Vector3(0, 0, 31.2f), 180, 4, 2.6f, 1f, ramp);
        GrindRail(t, origin + new Vector3(1.4f, 1.55f, 24.8f), origin + new Vector3(1.4f, 1.55f, 28.4f), rail);

        // ledge with a grindable edge
        Build.Box(t, new Vector3(10, .3f, 26), new Vector3(1.2f, .6f, 12), accent);
        var g = new GameObject("LedgeRail"); g.transform.SetParent(t, false);
        var r = g.AddComponent<Rail>(); r.a = origin + new Vector3(10, .6f, 20.2f); r.b = origin + new Vector3(10, .6f, 31.8f);

        // flat rails
        GrindRail(t, origin + new Vector3(-12, .55f, 8), origin + new Vector3(-12, .55f, 24), rail);
        GrindRail(t, origin + new Vector3(13.5f, .7f, 6), origin + new Vector3(13.5f, .7f, 18), rail);

        // mini half-pipe: two 45 degree walls facing each other, with decks
        Build.Ramp(t, new Vector3(0, 0, 50), 0, 14, 2.6f, 2.6f, ramp);
        Build.Box(t, new Vector3(0, 1.3f, 54.1f), new Vector3(14, 2.6f, 3), ramp);
        GrindRail(t, origin + new Vector3(-7, 2.66f, 52.65f), origin + new Vector3(7, 2.66f, 52.65f), rail, false);
        Build.Ramp(t, new Vector3(0, 0, 40), 180, 14, 2.6f, 2.6f, ramp);
        Build.Box(t, new Vector3(0, 1.3f, 35.9f), new Vector3(14, 2.6f, 3), ramp);
        GrindRail(t, origin + new Vector3(-7, 2.66f, 37.35f), origin + new Vector3(7, 2.66f, 37.35f), rail, false);

        // little sign posts in the region accent color
        for (int i = 0; i < 4; i++)
            Build.Box(t, new Vector3(-16.5f, 1.2f, 4 + i * 16), new Vector3(.3f, 2.4f, .3f), accent);
    }

    // ------------------------------------------------------------------ regions
    void City()
    {
        const int r = 0;
        var grey = Palette.Get(Palette.Hex("#c9c4c0"));
        Ground(r, grey, Palette.Hex("#5b5f6b"), 10, true);
        var t = Build.Group(root, "City", Vector3.zero);
        float[] crossings = { 75, 155, 300, 380 };
        bool Gap(float z, int side)
        {
            foreach (var c in crossings) if (Mathf.Abs(z - c) < 8) return true;
            if (side < 0 && z > 158 && z < 192) return true;      // fountain plaza
            if (side > 0 && z > 212 && z < 290) return true;      // skatepark
            return false;
        }
        string[] pastel = { "#f7b2a8", "#f5d58a", "#a8d8c9", "#b7c4f2", "#e3b7f0", "#f2c6a0", "#c8e6a0" };
        foreach (int side in new[] { -1, 1 })
        {
            // sidewalk curb
            Build.Box(t, new Vector3(side * 7.5f, .075f, RegionLen / 2), new Vector3(5, .15f, RegionLen), Palette.Hex("#dcd6cf"));
            float z = 4;
            while (z < RegionLen - 10)
            {
                float w = R(10, 16);
                if (!Gap(z + w / 2, side))
                {
                    float h = R(8, 26), d = R(10, 16);
                    var c = Pick(pastel);
                    var b = Build.Group(t, "Building", new Vector3(side * (10.5f + d / 2), 0, z + w / 2));
                    Build.Box(b, new Vector3(0, h / 2, 0), new Vector3(d, h, w - 1), c);
                    Build.Box(b, new Vector3(0, h + .2f, 0), new Vector3(d + .3f, .4f, w - .7f), Color.Lerp(c, Color.black, .15f), false);
                    float face = -side * (d / 2 + .02f);
                    for (float fy = 3.5f; fy < h - 1.5f; fy += 3.2f)
                        for (float fz = -w / 2 + 2; fz < w / 2 - 1.5f; fz += 2.6f)
                            Build.Box(b, new Vector3(face, fy, fz), new Vector3(.05f, 1.6f, 1.3f), Palette.Hex("#5f7fa8"), false);
                    // shop awning
                    var aw = Build.Box(b, new Vector3(face - side * .7f, 2.9f, 0), new Vector3(1.4f, .12f, Mathf.Min(6f, w - 3f)), Pick("#e9584f", "#3b6df2", "#57c27a", "#f2b84b"), false);
                    aw.transform.localRotation = Quaternion.Euler(0, 0, side * 18f);
                }
                z += w;
            }
            for (float lz = 12; lz < RegionLen; lz += 30) if (!Gap(lz, side)) Lamp(t, new Vector3(side * 5.5f, .15f, lz));
            for (float tz = 24; tz < RegionLen; tz += 36)
                if (!Gap(tz, side))
                {
                    Build.Box(t, new Vector3(side * 8.4f, .3f, tz), new Vector3(1.6f, .3f, 1.6f), sw.Mat(r, Role.Grass));
                    Tree(t, r, new Vector3(side * 8.4f, .3f, tz), .7f);
                }
            // handrails along the sidewalk edge (grindable)
            GrindRail(t, new Vector3(side * 9.6f, .75f, 20), new Vector3(side * 9.6f, .75f, 50), Palette.Hex("#b9c0cc"));
            GrindRail(t, new Vector3(side * 9.6f, .75f, 330), new Vector3(side * 9.6f, .75f, 360), Palette.Hex("#b9c0cc"));
        }
        // crosswalk stripes + crossing traffic
        foreach (var cz in crossings)
            for (int i = -4; i <= 4; i++) Build.Box(t, new Vector3(i * 1.1f, .006f, cz), new Vector3(.6f, .01f, 3.5f), Color.white, false);
        string[] carCols = { "#e9584f", "#3b6df2", "#f2b84b", "#57c27a", "#9b6bd6", "#f7f3ea" };
        MakeObstacle(Car(root, new Vector3(-34, 0, 77), 90, Pick(carCols)), ObstacleKind.Car, false, new Vector3(34, 0, 77), 9, 2.5f);
        MakeObstacle(Car(root, new Vector3(34, 0, 153), -90, Pick(carCols)), ObstacleKind.Car, false, new Vector3(-34, 0, 153), 8, 3f);
        MakeObstacle(Car(root, new Vector3(-34, 0, 302), 90, Pick(carCols)), ObstacleKind.Car, false, new Vector3(34, 0, 302), 10, 2f);
        MakeObstacle(Car(root, new Vector3(34, 0, 378), -90, Pick(carCols)), ObstacleKind.Car, false, new Vector3(-34, 0, 378), 9, 2.5f);
        MakeObstacle(Person(root, new Vector3(-8, 0, 120), Pick("#e9584f", "#57c27a"), false), ObstacleKind.Person, false, new Vector3(8, 0, 120), 1.6f, 2f);
        MakeObstacle(Person(root, new Vector3(8, 0, 340), Pick("#3b6df2", "#f2b84b"), false), ObstacleKind.Person, false, new Vector3(-8, 0, 340), 1.8f, 2f);
        Cone(new Vector3(1.5f, 0, 42)); Cone(new Vector3(-2, 0, 100)); Cone(new Vector3(2.2f, 0, 250));
        var barrier = Build.Group(root, "Barrier", new Vector3(0, 0, 200));
        Build.Box(barrier, new Vector3(0, .35f, 0), new Vector3(3.2f, .7f, .4f), Palette.Hex("#f2b84b"));
        for (int i = -1; i <= 1; i++) Build.Box(barrier, new Vector3(i * 1.05f, .35f, -.21f), new Vector3(.5f, .72f, .02f), Palette.Hex("#2b2b33"), false);
        MakeObstacle(barrier.gameObject, ObstacleKind.Barrier, true);

        // fountain plaza
        var f = Build.Group(t, "Fountain", new Vector3(-18, 0, 175));
        Build.Box(f, new Vector3(0, -.48f, 0), new Vector3(20, 1, 30), Palette.Hex("#e6ddd3"));
        Build.Cyl(f, new Vector3(0, .4f, 0), new Vector3(9, .4f, 9), Palette.Hex("#d6cfc6"), true);
        Build.Prim(PrimitiveType.Cylinder, f, new Vector3(0, .6f, 0), new Vector3(8, .25f, 8), sw.Mat(r, Role.Water), false);
        Build.Cyl(f, new Vector3(0, 1.6f, 0), new Vector3(.8f, 1.2f, .8f), Palette.Hex("#d6cfc6"));

        Park(r, new Vector3(28, 0, 220), Palette.Hex("#bdb8b2"), Palette.Hex("#a7a29c"), Palette.Hex("#e9584f"), Palette.Hex("#3b6df2"));

        // friends
        var dkGrey = Palette.Hex("#5a5f6e");
        CreatureFactory.Bird(root, new Vector3(-6.5f, .15f, 58), 90, "Gus", r, 1.3f, new CreatureFactory.B
        {
            bodyS = .55f, legL = .25f, neckL = .05f, beakL = .75f, beakW = .22f, col = Palette.Hex("#8d93a6"), wing = dkGrey,
            beak = Palette.Hex("#ff9f1c"), beakTip = Color.black, legs = Palette.Hex("#f4a6b4"), round = true, scale = 1.3f
        }, Lines.Gus).gameObject.AddComponent<CallTag>().call = SfxType.Squeak;
        CreatureFactory.Quadruped(root, new Vector3(7, .15f, 128), -90, "Rocco", r, 1.1f, new CreatureFactory.Q
        {
            bodyL = .9f, bodyH = .55f, bodyW = .55f, legL = .25f, legW = .1f, neckL = .05f, neckTilt = 20, headS = .45f, snoutL = .12f,
            col = Palette.Hex("#8e8e96"), belly = Palette.Hex("#c9c9cf"), dark = Palette.Hex("#3e3e46"), ears = 3, tail = 5, scale = 1.2f
        }, Lines.Rocco).gameObject.AddComponent<CallTag>().call = SfxType.Squeak;
        Build.Box(root, new Vector3(8.8f, .6f, 131), new Vector3(.9f, 1.2f, .9f), Palette.Hex("#3a8f5c"));
        Build.Box(root, new Vector3(8.8f, .6f, 132.2f), new Vector3(.9f, 1.2f, .9f), Palette.Hex("#6b6f78"));
        CreatureFactory.Fish(root, new Vector3(-18, 1.1f, 175), 90, "Big Wanda", r, .7f, Lines.Wanda).gameObject.AddComponent<CallTag>().call = SfxType.Splash;
        CreatureFactory.Quadruped(root, new Vector3(6.5f, .15f, 362), -90, "Biscuit", r, 1.2f, new CreatureFactory.Q
        {
            bodyL = .8f, bodyH = .45f, bodyW = .4f, legL = .3f, legW = .08f, neckL = .12f, neckTilt = 15, headS = .42f, snoutL = 0,
            col = Palette.Hex("#f2a24b"), belly = Palette.Hex("#fbe3c4"), dark = Palette.Hex("#c66f1f"), ears = 3, tail = 1, pattern = 1, scale = 1.2f
        }, Lines.Biscuit).gameObject.AddComponent<CallTag>().call = SfxType.Woof;
    }

    void Suburbs()
    {
        const int r = 1;
        float z0 = Z0(r);
        Ground(r, sw.Mat(r, Role.Grass), Palette.Hex("#6b6f78"), 9, true);
        var t = Build.Group(root, "Suburbs", Vector3.zero);
        string[] houseCols = { "#f7d9a8", "#c9e4f2", "#f2c4c4", "#d6efc7", "#fff3d6", "#e3d3f2" };
        string[] roofCols = { "#b5533c", "#5a6b8a", "#7a5236", "#3d6b5a" };
        foreach (int side in new[] { -1, 1 })
        {
            Build.Box(t, new Vector3(side * 5.8f, .06f, z0 + RegionLen / 2), new Vector3(2.4f, .12f, RegionLen), Palette.Hex("#d9d4cc"));
            for (float z = z0 + 14; z < z0 + RegionLen - 10; z += 30)
            {
                bool park = side < 0 && z > z0 + 165 && z < z0 + 260;
                if (park) continue;
                var c = Pick(houseCols);
                var h = Build.Group(t, "House", new Vector3(side * 21, 0, z));
                Build.Box(h, new Vector3(0, 2, 0), new Vector3(8, 4, 10), c);
                Roof(h, new Vector3(0, 4, 0), 8.6f, 10.6f, Pick(roofCols), 0).localRotation = Quaternion.Euler(0, 0, 45);
                Build.Box(h, new Vector3(-side * 4.02f, 1.1f, 0), new Vector3(.05f, 2.2f, 1.2f), Palette.Hex("#7a5236"), false);
                Build.Box(h, new Vector3(-side * 4.02f, 2.4f, 2.8f), new Vector3(.05f, 1.2f, 1.6f), Palette.Hex("#9fc7e8"), false);
                Build.Box(h, new Vector3(-side * 4.02f, 2.4f, -2.8f), new Vector3(.05f, 1.2f, 1.6f), Palette.Hex("#9fc7e8"), false);
                // driveway + fence with a gap
                Build.Box(t, new Vector3(side * 12.5f, .02f, z + 7), new Vector3(9, .04f, 3.4f), Palette.Hex("#c9c4bd"), false);
                Fence(t, new Vector3(side * 9.5f, 0, z - 12), new Vector3(side * 9.5f, 0, z + 4.8f), Color.white);
                Tree(t, r, new Vector3(side * R(12, 15), 0, z - R(3, 8)), R(.8f, 1.1f));
                Build.Box(t, new Vector3(side * 8.2f, .55f, z + 4), new Vector3(.4f, .4f, .6f), Pick("#3b6df2", "#e9584f", "#2b2b33"), true); // mailbox
                Build.Box(t, new Vector3(side * 8.2f, .2f, z + 4), new Vector3(.08f, .5f, .08f), Palette.Hex("#7a5236"));
            }
        }
        Park(r, new Vector3(-28, 0, z0 + 170), Palette.Hex("#cfc6b8"), Palette.Hex("#c89f6a"), Palette.Hex("#3b6df2"), Palette.Hex("#57c27a"));

        Bin(new Vector3(-1.8f, 0, z0 + 50)); Bin(new Vector3(2, 0, z0 + 230)); Bin(new Vector3(0, 0, z0 + 330));
        MakeObstacle(Person(root, new Vector3(-12, 0, z0 + 100), Palette.Hex("#57c27a"), true), ObstacleKind.Person, false, new Vector3(12, 0, z0 + 100), 4.5f, 2.5f);
        MakeObstacle(Person(root, new Vector3(12, 0, z0 + 300), Palette.Hex("#e9584f"), true), ObstacleKind.Person, false, new Vector3(-12, 0, z0 + 300), 4f, 3f);
        MakeObstacle(Car(root, new Vector3(3, 0, z0 + 150), 0, Palette.Hex("#9b6bd6")), ObstacleKind.Car, false);
        Cone(new Vector3(-2.5f, 0, z0 + 190));

        CreatureFactory.Quadruped(root, new Vector3(-7.2f, .12f, z0 + 60), 90, "Frank", r, 1.0f, new CreatureFactory.Q
        {
            bodyL = 1.6f, bodyH = .45f, bodyW = .42f, legL = .22f, legW = .09f, neckL = 1.6f, neckTilt = 8, headS = .4f, snoutL = .2f,
            col = Palette.Hex("#a8643a"), belly = Palette.Hex("#d99d6c"), dark = Palette.Hex("#5e3420"), ears = 4, tail = 1, scale = 1.2f
        }, Lines.Frank).gameObject.AddComponent<CallTag>().call = SfxType.Woof;
        CreatureFactory.Quadruped(root, new Vector3(7.5f, 0, z0 + 140), -90, "Clover", r, 1.6f, new CreatureFactory.Q
        {
            bodyL = .6f, bodyH = .5f, bodyW = .5f, legL = .12f, legW = .1f, neckL = .02f, neckTilt = 0, headS = .45f, snoutL = 0,
            col = Palette.Hex("#f4efe8"), belly = Color.white, dark = Palette.Hex("#c9b8a8"), ears = 5, tail = 2, scale = 1.1f
        }, Lines.Clover).gameObject.AddComponent<CallTag>().call = SfxType.Squeak;
        CreatureFactory.Bird(root, new Vector3(-7f, .12f, z0 + 270), 90, "Gertrude", r, 1.4f, new CreatureFactory.B
        {
            bodyS = .5f, legL = .18f, neckL = .3f, beakL = .25f, beakW = .14f, col = Color.white, wing = Palette.Hex("#e6e6ea"),
            beak = Palette.Hex("#ff9f1c"), legs = Palette.Hex("#ff9f1c"), scale = 1.1f
        }, Lines.Gertrude).gameObject.AddComponent<CallTag>().call = SfxType.Quack;
        CreatureFactory.Lizard(root, new Vector3(7.2f, .12f, z0 + 370), -90, "Shelly", r, 1.2f, Lines.Shelly).gameObject.AddComponent<CallTag>().call = SfxType.Squeak;
    }

    void Farms()
    {
        const int r = 2;
        float z0 = Z0(r);
        Ground(r, sw.Mat(r, Role.Grass), Palette.Hex("#b58a5a"), 8, false);
        var t = Build.Group(root, "Farms", Vector3.zero);
        var crop = sw.Mat(r, Role.Crop);
        var soil = Palette.Get(Palette.Hex("#8a6142"));
        foreach (int side in new[] { -1, 1 })
        {
            for (float fz = z0 + 10; fz < z0 + RegionLen - 30; fz += 70)
            {
                if (side > 0 && fz > z0 + 140 && fz < z0 + 260) continue; // skatepark
                var field = Build.Group(t, "Field", new Vector3(side * 34, 0, fz + 28));
                Build.Box(field, new Vector3(0, -.47f, 0), new Vector3(44, 1, 56), soil);
                for (int i = 0; i < 9; i++)
                    Build.Box(field, new Vector3(-20 + i * 5, .25f, 0), new Vector3(2.2f, .5f, 52), crop, false);
            }
            Fence(t, new Vector3(side * 6, 0, z0 + 20), new Vector3(side * 6, 0, z0 + 120), Palette.Hex("#8a5a3c"), .8f);
            Fence(t, new Vector3(side * 6, 0, z0 + 280), new Vector3(side * 6, 0, z0 + 400), Palette.Hex("#8a5a3c"), .8f);
        }
        // barn + silo
        var barn = Build.Group(t, "Barn", new Vector3(-28, 0, z0 + 200));
        Build.Box(barn, new Vector3(0, 4, 0), new Vector3(12, 8, 16), Palette.Hex("#c8443a"));
        Roof(barn, new Vector3(0, 8, 0), 13, 16.5f, Palette.Hex("#5a3a2e"));
        Build.Box(barn, new Vector3(6.02f, 2.5f, 0), new Vector3(.06f, 5, 5), Palette.Hex("#f4efe8"), false);
        Build.Box(barn, new Vector3(6.05f, 2.5f, 0), new Vector3(.06f, 5, .3f), Palette.Hex("#c8443a"), false);
        Build.Cyl(barn, new Vector3(-2, 6, 11), new Vector3(4, 6, 4), Palette.Hex("#d6d9de"), true);
        Build.Ball(barn, new Vector3(-2, 12, 11), new Vector3(4, 2.5f, 4), Palette.Hex("#8f97a3"));
        // windmill
        var wm = Build.Group(t, "Windmill", new Vector3(30, 0, z0 + 340));
        Build.Cyl(wm, new Vector3(0, 5, 0), new Vector3(1.2f, 5, 1.2f), Palette.Hex("#f4efe8"), true);
        var hub = Build.Group(wm, "Hub", new Vector3(-.8f, 9.5f, 0), 90);
        hub.gameObject.AddComponent<Spinner>().speed = 50;
        for (int i = 0; i < 4; i++)
            Build.Box(hub, Quaternion.Euler(0, 0, i * 90) * new Vector3(0, 2.2f, 0), new Vector3(.6f, 4.4f, .08f), Palette.Hex("#f4efe8"), false, 0).transform.localRotation = Quaternion.Euler(0, 0, i * 90);

        Park(r, new Vector3(28, 0, z0 + 160), Palette.Hex("#c9b08a"), Palette.Hex("#a8643a"), Palette.Hex("#f2b84b"), Palette.Hex("#c8443a"));
        foreach (var hz in new[] { 30f, 95f, 250f, 330f }) HayBale(new Vector3(R(-2.5f, 2.5f), 0, z0 + hz));
        for (int i = 0; i < 6; i++) HayBale(new Vector3(28 + R(-14, 14), 0, z0 + 160 + R(2, 60)), false);

        var tractor = Build.Group(root, "Tractor", new Vector3(-2.2f, 0, z0 + 270));
        Build.Box(tractor, new Vector3(0, 1.1f, .6f), new Vector3(1.6f, 1.2f, 2.4f), Palette.Hex("#3a9a4a"));
        Build.Box(tractor, new Vector3(0, 2.1f, -.4f), new Vector3(1.4f, 1.2f, 1.2f), Palette.Hex("#3a9a4a"));
        Build.Box(tractor, new Vector3(0, 2.75f, -.4f), new Vector3(1.6f, .1f, 1.4f), Palette.Hex("#f2d16b"), false);
        foreach (float x in new[] { -1f, 1f })
        {
            Build.Cyl(tractor, new Vector3(x * .9f, .8f, -.6f), new Vector3(1.6f, .25f, 1.6f), Palette.Hex("#2b2b33"), false, Quaternion.Euler(0, 0, 90));
            Build.Cyl(tractor, new Vector3(x * .85f, .45f, 1.4f), new Vector3(.9f, .2f, .9f), Palette.Hex("#2b2b33"), false, Quaternion.Euler(0, 0, 90));
        }
        MakeObstacle(tractor.gameObject, ObstacleKind.Tractor, false, new Vector3(-2.2f, 0, z0 + 400), 3f, 3f);

        CreatureFactory.Quadruped(root, new Vector3(-10, 0, z0 + 60), 90, "Moo-reen", r, .7f, new CreatureFactory.Q
        {
            bodyL = 2f, bodyH = 1.1f, bodyW = 1f, legL = .8f, legW = .16f, neckL = .3f, neckTilt = 45, headS = .7f, snoutL = .2f,
            col = Color.white, belly = Palette.Hex("#f6d6dc"), dark = Palette.Hex("#26262e"), accent = Palette.Hex("#e8dcc0"),
            ears = 4, tail = 4, pattern = 1, horns = 1, scale = 1
        }, Lines.Moo).gameObject.AddComponent<CallTag>().call = SfxType.Moo;
        CreatureFactory.Bird(root, new Vector3(7.5f, 0, z0 + 130), -90, "Henrietta", r, 1.5f, new CreatureFactory.B
        {
            bodyS = .7f, legL = 1.6f, neckL = .25f, beakL = .18f, beakW = .14f, col = Palette.Hex("#f4efe8"), wing = Palette.Hex("#e8dcc0"),
            beak = Palette.Hex("#f2b84b"), legs = Palette.Hex("#f2b84b"), crest = true, scale = 1
        }, Lines.Henrietta).gameObject.AddComponent<CallTag>().call = SfxType.Cluck;
        CreatureFactory.Quadruped(root, new Vector3(-9, 0, z0 + 290), 90, "Porkchop", r, .9f, new CreatureFactory.Q
        {
            bodyL = 1.4f, bodyH = .8f, bodyW = .8f, legL = .3f, legW = .13f, neckL = .02f, neckTilt = 0, headS = .6f, snoutL = .15f,
            col = Palette.Hex("#f7b5c0"), belly = Palette.Hex("#fbd0d6"), dark = Palette.Hex("#d97a8a"), accent = Palette.Hex("#fff6e0"),
            ears = 4, tail = 3, horns = 3, scale = 1
        }, Lines.Porkchop).gameObject.AddComponent<CallTag>().call = SfxType.Oink;
        Build.Box(root, new Vector3(-10, .02f, z0 + 292), new Vector3(5, .04f, 4), Palette.Hex("#6b4a33"), false); // mud
        CreatureFactory.Quadruped(root, new Vector3(9, 0, z0 + 365), -90, "Barry", r, 1.1f, new CreatureFactory.Q
        {
            bodyL = 1.1f, bodyH = .6f, bodyW = .55f, legL = .6f, legW = .09f, neckL = .25f, neckTilt = 35, headS = .45f, snoutL = .1f,
            col = Palette.Hex("#f2d4c4"), belly = Palette.Hex("#f7e3d6"), dark = Palette.Hex("#3e3436"), accent = Palette.Hex("#d6c4a0"),
            ears = 4, tail = 3, scale = 1
        }, Lines.Barry).gameObject.AddComponent<CallTag>().call = SfxType.Baa;
    }

    void Forest()
    {
        const int r = 3;
        float z0 = Z0(r);
        Ground(r, sw.Mat(r, Role.Grass), Palette.Hex("#8a6a4a"), 7, false);
        var t = Build.Group(root, "Forest", Vector3.zero);
        foreach (int side in new[] { -1, 1 })
        {
            for (int i = 0; i < 110; i++)
            {
                float z = z0 + R(0, RegionLen + 10);
                float x = side * R(6.5f, 70);
                if (side < 0 && x > -46 && z > z0 + 90 && z < z0 + 160) continue; // skatepark
                if (Mathf.Abs(z - NanaZ) < 22 && Mathf.Abs(x) < 16) continue;    // cabin clearing
                if (rnd.NextDouble() < .55) Pine(t, r, new Vector3(x, 0, z), R(1, 1.8f));
                else Tree(t, r, new Vector3(x, 0, z), R(.9f, 1.4f));
            }
            // mountain ridges on both sides and at the end
            for (int m = 0; m < 4; m++)
            {
                float mz = z0 + 40 + m * 110;
                float h = R(45, 70);
                Build.Prim(PrimitiveType.Cube, t, new Vector3(side * R(95, 120), -h * .2f, mz), new Vector3(h, h, R(80, 120)), Palette.Get(Palette.Hex("#8c98a8")), false, Quaternion.Euler(0, R(-10, 10), 45));
                Build.Prim(PrimitiveType.Cube, t, new Vector3(side * R(95, 120), h * .38f, mz), new Vector3(h * .35f, h * .35f, 40), sw.Mat(r, Role.FoliageA), false, Quaternion.Euler(0, 0, 45));
            }
            for (int i = 0; i < 10; i++)
            {
                var rock = Build.Ball(root, new Vector3(side * R(3.8f, 5), .2f, z0 + R(20, RegionLen - 60)), new Vector3(R(.8f, 1.4f), R(.6f, 1f), R(.8f, 1.4f)), Palette.Hex("#9a9aa0"), true);
                MakeObstacle(rock, ObstacleKind.Rock, false);
            }
        }
        Build.Prim(PrimitiveType.Cube, t, new Vector3(0, -20, EndZ + 120), new Vector3(110, 110, 60), Palette.Get(Palette.Hex("#7d889a")), false, Quaternion.Euler(0, 0, 45));
        Build.Prim(PrimitiveType.Cube, t, new Vector3(0, 40, EndZ + 120), new Vector3(30, 30, 30), sw.Mat(r, Role.FoliageA), false, Quaternion.Euler(0, 0, 45));

        // a stream across the trail
        Build.Box(t, new Vector3(0, .02f, StreamZ), new Vector3(160, .04f, 4), sw.Mat(r, Role.Water), false);

        Park(r, new Vector3(-28, 0, z0 + 92), Palette.Hex("#9aa08a"), Palette.Hex("#8c8f96"), Palette.Hex("#7a5236"), Palette.Hex("#4f7a6a"));
        foreach (var lz in new[] { 40f, 190f, 260f, 330f }) Log(new Vector3(0, 0, z0 + lz), 7.5f);

        CreatureFactory.Quadruped(root, new Vector3(8, 0, z0 + 50), -90, "Geoffrey", r, .9f, new CreatureFactory.Q
        {
            bodyL = 1.8f, bodyH = 1f, bodyW = .8f, legL = 1.3f, legW = .12f, neckL = .45f, neckTilt = 40, headS = .6f, snoutL = .3f,
            col = Palette.Hex("#f2c45a"), belly = Palette.Hex("#f7dc96"), dark = Palette.Hex("#b5733a"), ears = 3, tail = 4, pattern = 3, horns = 4, mane = true, scale = 1
        }, Lines.Geoffrey).gameObject.AddComponent<CallTag>().call = SfxType.Squeak;
        var stump = Build.Cyl(root, new Vector3(-7, .5f, z0 + 120), new Vector3(1.2f, .5f, 1.2f), Palette.Hex("#7a5236"), true);
        CreatureFactory.Bird(root, new Vector3(-7, 1f, z0 + 120), 90, "Hoot", r, 1.0f, new CreatureFactory.B
        {
            bodyS = .7f, legL = .1f, neckL = 0, beakL = .1f, beakW = .1f, col = Palette.Hex("#a8825a"), wing = Palette.Hex("#7a5a3c"),
            beak = Palette.Hex("#f2b84b"), legs = Palette.Hex("#f2b84b"), bigEyes = true, round = true, crest = true, scale = 1
        }, Lines.Hoot).gameObject.AddComponent<CallTag>().call = SfxType.Hoot;
        Build.Ball(root, new Vector3(8.5f, .5f, z0 + 280), new Vector3(2.4f, 1.4f, 2.4f), Palette.Hex("#9a9aa0"), true);
        CreatureFactory.Quadruped(root, new Vector3(8.5f, 1.1f, z0 + 280), -90, "Billy", r, 1.2f, new CreatureFactory.Q
        {
            bodyL = 1.1f, bodyH = .65f, bodyW = .55f, legL = .55f, legW = .09f, neckL = .3f, neckTilt = 30, headS = .45f, snoutL = .15f,
            col = Palette.Hex("#f4efe8"), belly = Color.white, dark = Palette.Hex("#8a8a92"), accent = Palette.Hex("#8a5a3c"), ears = 3, tail = 3, horns = 2, scale = 1
        }, Lines.Billy).gameObject.AddComponent<CallTag>().call = SfxType.Baa;
        CreatureFactory.Quadruped(root, new Vector3(-9, 0, z0 + 330), 90, "Snowball", r, .6f, new CreatureFactory.Q
        {
            bodyL = 2f, bodyH = 1.3f, bodyW = 1.3f, legL = .55f, legW = .25f, neckL = .2f, neckTilt = 50, headS = .8f, snoutL = .25f,
            col = Palette.Hex("#8a5a3c"), belly = Palette.Hex("#a8744e"), dark = Palette.Hex("#3e2a1e"), ears = 1, tail = 3, pattern = 3, scale = 1
        }, Lines.Snowball).gameObject.AddComponent<CallTag>().call = SfxType.Moo;

        // Nana's cabin
        var cabin = Build.Group(t, "Cabin", new Vector3(0, 0, NanaZ + 14));
        Build.Box(cabin, new Vector3(0, 2.5f, 0), new Vector3(12, 5, 9), Palette.Hex("#9a6a44"));
        for (float y = .5f; y < 5; y += .9f) Build.Box(cabin, new Vector3(0, y, -4.52f), new Vector3(12.2f, .12f, .05f), Palette.Hex("#7a5236"), false);
        Roof(cabin, new Vector3(0, 5, 0), 10, 12.8f, Palette.Hex("#c8443a"), 90);
        Build.Box(cabin, new Vector3(0, 1.3f, -4.55f), new Vector3(1.6f, 2.6f, .1f), Palette.Hex("#5a3a2e"), false);
        Build.Box(cabin, new Vector3(-3.5f, 2.6f, -4.55f), new Vector3(1.6f, 1.3f, .1f), Palette.Emissive(Palette.Hex("#ffd27a"), 1.2f), false);
        Build.Box(cabin, new Vector3(3.5f, 2.6f, -4.55f), new Vector3(1.6f, 1.3f, .1f), Palette.Emissive(Palette.Hex("#ffd27a"), 1.2f), false);
        Build.Box(cabin, new Vector3(4, 7, 1), new Vector3(1.2f, 4, 1.2f), Palette.Hex("#8c8f96"));
        for (int i = 0; i < 6; i++) Build.Box(cabin, new Vector3(-4 + i * 1.6f, .1f, -6.5f), new Vector3(1.2f, .2f, 3.5f), Palette.Hex("#c8a27a"), true); // porch
        CreatureFactory.Nana(root, new Vector3(0, 0, NanaZ), 180, Lines.Nana);
    }

    void Boundary(int region)
    {
        float z = Z0(region);
        Color[] archCol = { default, Palette.Hex("#57c27a"), Palette.Hex("#c8443a"), Palette.Hex("#4f7a6a") };
        Arch(region, z, archCol[region], RegionNames[region]);
        var c = Palette.Hex("#b9c0cc");
        GrindRail(root, new Vector3(-4.6f, .6f, z - 34), new Vector3(-4.6f, .6f, z - 8), c);
        GrindRail(root, new Vector3(4.6f, .6f, z - 30), new Vector3(4.6f, .6f, z - 4), c);
        GrindRail(root, new Vector3(-2.2f, .9f, z + 10), new Vector3(-2.2f, .9f, z + 28), Palette.Hex("#e9584f"));
    }
}

/// Which animal sound plays when a friend starts talking.
public class CallTag : MonoBehaviour { public SfxType call = SfxType.Squeak; }
