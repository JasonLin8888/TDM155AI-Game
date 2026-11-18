using System.Collections.Generic;
using UnityEngine;

/// A grindable line. a/b are world-space points on the top of the rail.
public class Rail : MonoBehaviour
{
    public static readonly List<Rail> All = new List<Rail>();
    public Vector3 a, b;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public Vector3 Dir => (b - a).normalized;
    public float Length => (b - a).magnitude;
    public Vector3 Closest(Vector3 p, out float t)
    {
        var ab = b - a;
        t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude);
        return a + ab * t;
    }
}

public enum ObstacleKind { Car, Person, Tractor, Log, Cone, HayBale, Bin, Rock, Barrier }

/// Something Ollie bails on if he hits it. Jumpable ones award a bonus when cleared.
public class Obstacle : MonoBehaviour
{
    public ObstacleKind kind;
    public bool jumpable;
    public Vector3 moveA, moveB;   // world positions; equal = static
    public float moveSpeed;
    public float pause = 1.5f;
    float t, wait, dir = 1, bob;
    float honkCooldown;

    public bool Moving => moveSpeed > 0 && moveA != moveB;

    void Update()
    {
        if (GameManager.I != null && GameManager.I.State == GameState.Paused) return;
        honkCooldown -= Time.deltaTime;
        if (!Moving) return;
        if (wait > 0) { wait -= Time.deltaTime; return; }
        float len = Vector3.Distance(moveA, moveB);
        t += dir * moveSpeed * Time.deltaTime / len;
        if (t >= 1 || t <= 0) { t = Mathf.Clamp01(t); dir = -dir; wait = pause; }
        var p = Vector3.Lerp(moveA, moveB, t);
        var fwd = (moveB - moveA).normalized * dir;
        if (kind == ObstacleKind.Person) { bob += Time.deltaTime * 9; p.y += Mathf.Abs(Mathf.Sin(bob)) * .12f; }
        transform.position = p;
        if (fwd.sqrMagnitude > 0) transform.rotation = Quaternion.LookRotation(fwd);

        // Cars and tractors honk when Ollie is close.
        if ((kind == ObstacleKind.Car || kind == ObstacleKind.Tractor) && honkCooldown <= 0 && GameManager.I != null && GameManager.I.skater != null)
        {
            if (Vector3.Distance(GameManager.I.skater.transform.position, transform.position) < 9f)
            {
                Synth.Sfx(kind == ObstacleKind.Car ? SfxType.Honk : SfxType.TractorHorn);
                honkCooldown = 6f;
            }
        }
        if (kind == ObstacleKind.Person && honkCooldown <= 0 && GameManager.I != null && GameManager.I.skater != null &&
            Vector3.Distance(GameManager.I.skater.transform.position, transform.position) < 6f)
        {
            Synth.Sfx(SfxType.BikeBell);
            honkCooldown = 8f;
        }
    }
}

/// A friend to meet. Dialogue lines are "Speaker|text".
public class Creature : MonoBehaviour
{
    public static readonly List<Creature> All = new List<Creature>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => All.Clear();

    public string displayName;
    public string[] lines;
    public float voicePitch = 1f;
    public bool met, isGrandma, floats;
    public int region;
    Vector3 basePos;
    float phase;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
    void Start() { basePos = transform.position; phase = Random.value * 10f; }

    void Update()
    {
        phase += Time.deltaTime;
        var p = basePos;
        p.y += floats ? Mathf.Sin(phase * 1.5f) * .15f : Mathf.Abs(Mathf.Sin(phase * 2.2f)) * .06f;
        transform.position = p;
        var sk = GameManager.I != null ? GameManager.I.skater : null;
        if (sk != null)
        {
            var to = sk.transform.position - transform.position; to.y = 0;
            if (to.magnitude < 14f && to.sqrMagnitude > .01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), Time.deltaTime * 3f);
        }
    }
}

public class Spinner : MonoBehaviour
{
    public Vector3 axis = Vector3.forward;
    public float speed = 60;
    void Update() => transform.Rotate(axis, speed * Time.deltaTime, Space.Self);
}
