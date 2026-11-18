using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Shared material cache. Everything in the game is flat-colored primitives,
/// so one URP Lit base material is cloned per color.
public static class Palette
{
    static Material baseMat, particleMat;
    static readonly Dictionary<Color32, Material> cache = new Dictionary<Color32, Material>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { cache.Clear(); baseMat = null; particleMat = null; }

    public static Material Base
    {
        get
        {
            if (baseMat == null)
            {
                baseMat = Resources.Load<Material>("BaseLit");
                if (baseMat == null) baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            }
            return baseMat;
        }
    }

    public static Material Particle
    {
        get
        {
            if (particleMat == null)
            {
                var asset = Resources.Load<Material>("ParticleMat");
                particleMat = asset != null ? new Material(asset) : new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                particleMat.SetTexture("_BaseMap", SoftDot());
            }
            return particleMat;
        }
    }

    public static Material Get(Color c)
    {
        Color32 k = c;
        if (!cache.TryGetValue(k, out var m) || m == null)
        {
            m = NewMat(c);
            cache[k] = m;
        }
        return m;
    }

    public static Material NewMat(Color c)
    {
        var m = new Material(Base);
        m.color = c;
        return m;
    }

    public static Material Emissive(Color c, float intensity)
    {
        var m = NewMat(c);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * intensity);
        return m;
    }

    static Texture2D SoftDot()
    {
        const int n = 32;
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1, 1, 1, a * a * (3 - 2 * a)));
            }
        t.Apply();
        return t;
    }

    public static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
}

/// Primitive builders.
public static class Build
{
    public static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material m, bool collider = true, Quaternion? rot = null, string name = null)
    {
        var g = GameObject.CreatePrimitive(t);
        if (name != null) g.name = name;
        g.transform.SetParent(parent, false);
        g.transform.localPosition = pos;
        g.transform.localRotation = rot ?? Quaternion.identity;
        g.transform.localScale = scale;
        g.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.Destroy(g.GetComponent<Collider>());
        return g;
    }

    public static GameObject Box(Transform p, Vector3 pos, Vector3 size, Color c, bool col = true, float yaw = 0)
        => Prim(PrimitiveType.Cube, p, pos, size, Palette.Get(c), col, Quaternion.Euler(0, yaw, 0));
    public static GameObject Box(Transform p, Vector3 pos, Vector3 size, Material m, bool col = true, float yaw = 0)
        => Prim(PrimitiveType.Cube, p, pos, size, m, col, Quaternion.Euler(0, yaw, 0));
    public static GameObject Ball(Transform p, Vector3 pos, Vector3 size, Color c, bool col = false)
        => Prim(PrimitiveType.Sphere, p, pos, size, Palette.Get(c), col);
    public static GameObject Ball(Transform p, Vector3 pos, Vector3 size, Material m, bool col = false)
        => Prim(PrimitiveType.Sphere, p, pos, size, m, col);
    public static GameObject Cyl(Transform p, Vector3 pos, Vector3 size, Color c, bool col = false, Quaternion? rot = null)
        => Prim(PrimitiveType.Cylinder, p, pos, size, Palette.Get(c), col, rot);
    public static GameObject Cap(Transform p, Vector3 pos, Vector3 size, Color c, bool col = false, Quaternion? rot = null)
        => Prim(PrimitiveType.Capsule, p, pos, size, Palette.Get(c), col, rot);

    public static Transform Group(Transform parent, string name, Vector3 pos, float yaw = 0)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = pos;
        g.localRotation = Quaternion.Euler(0, yaw, 0);
        return g;
    }

    /// A ramp whose top surface rises from (local z=0, y=0) to (z=depth, y=height).
    /// Built as a thick slab that is half buried so it is solid from every side.
    public static GameObject Ramp(Transform p, Vector3 pos, float yaw, float width, float depth, float height, Color c)
    {
        var g = Group(p, "Ramp", pos, yaw);
        float ang = Mathf.Atan2(height, depth);
        float len = Mathf.Sqrt(depth * depth + height * height);
        float thick = 3f;
        var normal = new Vector3(0, Mathf.Cos(ang), -Mathf.Sin(ang));
        var topCenter = new Vector3(0, height / 2f, depth / 2f);
        Prim(PrimitiveType.Cube, g, topCenter - normal * thick / 2f, new Vector3(width, thick, len), Palette.Get(c), true,
            Quaternion.Euler(-ang * Mathf.Rad2Deg, 0, 0), "Slope");
        return g.gameObject;
    }
}

/// Keyboard + gamepad input in one place (project uses the new Input System only).
public static class Inp
{
    static Keyboard K => Keyboard.current;
    static Gamepad G => Gamepad.current;

    public static bool Held(Key k) => K != null && K[k].isPressed;
    public static bool Down(Key k) => K != null && K[k].wasPressedThisFrame;

    public static float Steer
    {
        get
        {
            float v = 0;
            if (Held(Key.A) || Held(Key.LeftArrow)) v -= 1;
            if (Held(Key.D) || Held(Key.RightArrow)) v += 1;
            if (G != null) v += G.leftStick.x.ReadValue();
            return Mathf.Clamp(v, -1, 1);
        }
    }
    public static bool Push => Held(Key.W) || Held(Key.UpArrow) || (G != null && (G.rightTrigger.isPressed || G.leftStick.y.ReadValue() > .5f));
    public static bool Brake => Held(Key.S) || Held(Key.DownArrow) || (G != null && (G.leftTrigger.isPressed || G.leftStick.y.ReadValue() < -.5f));
    public static bool Ollie => Down(Key.Space) || (G != null && G.buttonSouth.wasPressedThisFrame);
    public static bool Kickflip => Down(Key.J) || (G != null && G.buttonWest.wasPressedThisFrame);
    public static bool Heelflip => Down(Key.K) || (G != null && G.buttonNorth.wasPressedThisFrame);
    public static bool ShoveIt => Down(Key.L) || (G != null && G.buttonEast.wasPressedThisFrame);
    public static bool Manual => Held(Key.LeftShift) || Held(Key.RightShift) || (G != null && G.rightShoulder.isPressed);
    public static bool Confirm => Down(Key.Enter) || Down(Key.Space) || Down(Key.E) || (G != null && G.buttonSouth.wasPressedThisFrame);
    public static bool Pause => Down(Key.Escape) || Down(Key.P) || (G != null && G.startButton.wasPressedThisFrame);
    public static bool Advance => Down(Key.E) || Down(Key.Enter) || (G != null && G.selectButton.wasPressedThisFrame);
}
