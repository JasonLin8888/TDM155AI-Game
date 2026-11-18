using UnityEngine;

/// Stylized low-poly Ollie the rat + his skateboard, built from primitives.
/// Hierarchy: root (ground-aligned) -> spin (air spins) -> board, rider.
public class OllieModel : MonoBehaviour
{
    public Transform align, spin, board, rider, tail, head;
    Transform[] tailSegs;

    static readonly Color Fur = Palette.Hex("#9a9aa6");
    static readonly Color FurDark = Palette.Hex("#7c7c8a");
    static readonly Color Belly = Palette.Hex("#d9d6dc");
    static readonly Color Pink = Palette.Hex("#f4a6b4");
    static readonly Color Hoodie = Palette.Hex("#ffb547");
    static readonly Color Phones = Palette.Hex("#3b6df2");
    static readonly Color Deck = Palette.Hex("#e9584f");
    static readonly Color Grip = Palette.Hex("#2b2b33");
    static readonly Color Wheel = Palette.Hex("#fff1c9");

    public static OllieModel Create(Transform parent)
    {
        var root = new GameObject("OllieModel").transform;
        root.SetParent(parent, false);
        var m = root.gameObject.AddComponent<OllieModel>();
        m.align = Build.Group(root, "Align", Vector3.zero);
        m.spin = Build.Group(m.align, "Spin", Vector3.zero);
        m.BuildBoard();
        m.BuildRat();
        return m;
    }

    void BuildBoard()
    {
        board = Build.Group(spin, "Board", new Vector3(0, .16f, 0));
        Build.Box(board, new Vector3(0, .05f, 0), new Vector3(.34f, .05f, 1.05f), Deck, false);
        Build.Box(board, new Vector3(0, .08f, 0), new Vector3(.32f, .012f, 1.0f), Grip, false);
        // kicked-up nose and tail
        Build.Prim(PrimitiveType.Cube, board, new Vector3(0, .09f, .57f), new Vector3(.34f, .05f, .16f), Palette.Get(Deck), false, Quaternion.Euler(-25, 0, 0));
        Build.Prim(PrimitiveType.Cube, board, new Vector3(0, .09f, -.57f), new Vector3(.34f, .05f, .16f), Palette.Get(Deck), false, Quaternion.Euler(25, 0, 0));
        foreach (float z in new[] { .36f, -.36f })
        {
            Build.Box(board, new Vector3(0, -.01f, z), new Vector3(.3f, .04f, .06f), Palette.Hex("#c9c9d1"), false);
            foreach (float x in new[] { .15f, -.15f })
                Build.Cyl(board, new Vector3(x, -.07f, z), new Vector3(.11f, .03f, .11f), Wheel, false, Quaternion.Euler(0, 0, 90));
        }
    }

    void BuildRat()
    {
        rider = Build.Group(spin, "Rider", new Vector3(0, .26f, 0));
        // feet on the board
        Build.Ball(rider, new Vector3(-.06f, .03f, .22f), new Vector3(.13f, .07f, .2f), Pink);
        Build.Ball(rider, new Vector3(.06f, .03f, -.2f), new Vector3(.13f, .07f, .2f), Pink);
        // legs
        Build.Cap(rider, new Vector3(-.05f, .2f, .16f), new Vector3(.13f, .17f, .13f), FurDark, false, Quaternion.Euler(-15, 0, 0));
        Build.Cap(rider, new Vector3(.05f, .2f, -.14f), new Vector3(.13f, .17f, .13f), FurDark, false, Quaternion.Euler(15, 0, 0));
        // body: pear shape with hoodie
        Build.Ball(rider, new Vector3(0, .5f, 0), new Vector3(.46f, .5f, .4f), Hoodie);
        Build.Ball(rider, new Vector3(0, .48f, .12f), new Vector3(.3f, .34f, .2f), Belly);
        Build.Box(rider, new Vector3(0, .38f, .19f), new Vector3(.2f, .1f, .04f), Palette.Hex("#e8962e"), false); // hoodie pocket
        // arms out for balance
        Build.Cap(rider, new Vector3(-.3f, .58f, .02f), new Vector3(.1f, .2f, .1f), Hoodie, false, Quaternion.Euler(0, 0, 65));
        Build.Cap(rider, new Vector3(.3f, .58f, -.02f), new Vector3(.1f, .2f, .1f), Hoodie, false, Quaternion.Euler(0, 0, -65));
        Build.Ball(rider, new Vector3(-.47f, .64f, .02f), new Vector3(.09f, .09f, .09f), Pink);
        Build.Ball(rider, new Vector3(.47f, .64f, -.02f), new Vector3(.09f, .09f, .09f), Pink);

        head = Build.Group(rider, "Head", new Vector3(0, .86f, .04f));
        Build.Ball(head, Vector3.zero, new Vector3(.36f, .32f, .36f), Fur);
        Build.Ball(head, new Vector3(0, -.03f, .2f), new Vector3(.2f, .17f, .3f), Fur);      // snout
        Build.Ball(head, new Vector3(0, -.01f, .35f), new Vector3(.07f, .06f, .06f), Pink);   // nose
        Build.Ball(head, new Vector3(-.08f, .07f, .15f), new Vector3(.065f, .075f, .05f), Color.black);
        Build.Ball(head, new Vector3(.08f, .07f, .15f), new Vector3(.065f, .075f, .05f), Color.black);
        Build.Ball(head, new Vector3(-.07f, .09f, .17f), new Vector3(.02f, .02f, .02f), Color.white);
        Build.Ball(head, new Vector3(.09f, .09f, .17f), new Vector3(.02f, .02f, .02f), Color.white);
        foreach (int s in new[] { -1, 1 })
        {
            // big round ears
            Build.Ball(head, new Vector3(s * .16f, .19f, -.02f), new Vector3(.2f, .2f, .05f), Fur);
            Build.Ball(head, new Vector3(s * .16f, .19f, .0f), new Vector3(.14f, .14f, .03f), Pink);
            // whiskers
            for (int w = 0; w < 2; w++)
                Build.Box(head, new Vector3(s * .12f, -.04f + w * .03f, .28f), new Vector3(.16f, .006f, .006f), FurDark, false, s * (10 + w * 12));
            // headphone cups
            Build.Cyl(head, new Vector3(s * .19f, .0f, 0), new Vector3(.13f, .04f, .13f), Phones, false, Quaternion.Euler(0, 0, 90));
        }
        // headphone band
        Build.Box(head, new Vector3(-.13f, .14f, 0), new Vector3(.04f, .14f, .05f), Phones, false, 0).transform.localRotation = Quaternion.Euler(0, 0, 35);
        Build.Box(head, new Vector3(.13f, .14f, 0), new Vector3(.04f, .14f, .05f), Phones, false, 0).transform.localRotation = Quaternion.Euler(0, 0, -35);
        Build.Box(head, new Vector3(0, .2f, 0), new Vector3(.16f, .04f, .05f), Phones, false);

        // long pink tail, segmented so it can wiggle
        tail = Build.Group(rider, "Tail", new Vector3(0, .3f, -.2f));
        tailSegs = new Transform[7];
        Transform parent = tail;
        for (int i = 0; i < tailSegs.Length; i++)
        {
            var seg = Build.Group(parent, "T" + i, i == 0 ? Vector3.zero : new Vector3(0, 0, -.11f));
            float w = .06f - i * .006f;
            Build.Cap(seg, new Vector3(0, 0, -.055f), new Vector3(w, .07f, w), Pink, false, Quaternion.Euler(90, 0, 0));
            tailSegs[i] = seg;
            parent = seg;
        }
    }

    public void AnimateTail(float speed01, float t)
    {
        if (tailSegs == null) return;
        for (int i = 0; i < tailSegs.Length; i++)
        {
            float wig = Mathf.Sin(t * (4 + speed01 * 6) - i * .7f) * (8 + speed01 * 6);
            tailSegs[i].localRotation = Quaternion.Euler(i == 0 ? -20 : 6 + speed01 * -4, wig, 0);
        }
    }
}
