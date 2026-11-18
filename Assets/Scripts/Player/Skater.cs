using UnityEngine;

public enum TrickType { None, Kickflip, Heelflip, ShoveIt }

/// Kinematic skateboard controller: pushing, carving, slopes, ollies,
/// flip tricks, air spins, grinds, manuals and bails.
public class Skater : MonoBehaviour
{
    // tuning
    public float gravity = 20f, jumpVel = 7.2f;
    public float pushImpulse = 2.6f, pushSpeedCap = 10.5f, maxSpeed = 18f;
    public float brakeDecel = 10f, rollFriction = .35f;
    public float spinRate = 540f;

    public OllieModel model;
    public float Speed => grinding ? speed : (grounded ? Mathf.Abs(speed) : new Vector3(airVel.x, 0, airVel.z).magnitude);
    public float Speed01 => Mathf.Clamp01(Speed / 14f);
    public float Yaw => yaw;
    public bool Grounded => grounded;
    public bool Grinding => grinding;
    public bool Bailed => bailTimer > 0;
    public bool InputEnabled = true;

    float yaw, speed;
    bool grounded = true;
    Vector3 groundNormal = Vector3.up;
    Vector3 airVel;
    float noGroundTimer, pushCooldown, bailTimer, airTime;
    bool manual; float manualTime;

    // tricks
    TrickType trick; float trickT; const float TrickDur = .42f;
    float spinAngle;           // visual air spin, degrees
    int airTricks;             // tricks done this air
    bool clearedObstacle;
    Obstacle lastCleared;

    // grind
    bool grinding; Rail rail; float railT; float railDir; float grindTime;

    // respawn
    Vector3 checkpoint; float checkpointYaw;

    public event System.Action<string, int> OnTrick;   // name, base points
    public event System.Action OnBail;

    public void Place(Vector3 pos, float yawDeg)
    {
        transform.position = pos;
        yaw = yawDeg; speed = 0; airVel = Vector3.zero; grounded = true; grinding = false; bailTimer = 0;
        checkpoint = pos; checkpointYaw = yawDeg;
    }

    public void SetCheckpoint(Vector3 p, float y) { checkpoint = p; checkpointYaw = y; }

    void Update()
    {
        if (GameManager.I != null && GameManager.I.State == GameState.Paused) return;
        Step(Mathf.Min(Time.deltaTime, 1f / 30f));
    }

    // Debug/testing: simulated inputs used by Step when set.
    [System.NonSerialized] public bool simOllie, simPush; [System.NonSerialized] public TrickType simTrick;

    bool WantOllie() { if (simOllie) { simOllie = false; return true; } return Inp.Ollie; }
    bool WantTrick(TrickType t, bool real) { if (simTrick == t) { simTrick = TrickType.None; return true; } return real; }

    public void Step(float dt)
    {
        if (bailTimer > 0) { UpdateBail(dt); return; }
        if (grinding) UpdateGrind(dt);
        else if (grounded) UpdateGround(dt);
        else UpdateAir(dt);

        if (transform.position.y < -15f) Respawn();
        CheckObstacles();
        UpdateVisual(dt);
    }

    Vector3 FlatFwd => Quaternion.Euler(0, yaw, 0) * Vector3.forward;

    // ---------------- ground ----------------
    void UpdateGround(float dt)
    {
        var fwdSlope = Vector3.ProjectOnPlane(FlatFwd, groundNormal).normalized;

        // gravity along slope
        speed += -gravity * fwdSlope.y * dt;
        // rolling friction
        speed = Mathf.MoveTowards(speed, 0, rollFriction * dt);

        bool inputOn = InputEnabled;
        manual = inputOn && Inp.Manual && Mathf.Abs(speed) > 1.5f;
        if (manual) manualTime += dt; else if (manualTime > 0) EndManual();

        pushCooldown -= dt;
        if (inputOn && (Inp.Push || simPush) && !manual && pushCooldown <= 0 && speed < pushSpeedCap)
        {
            speed = Mathf.Min(speed + pushImpulse, pushSpeedCap);
            pushCooldown = .45f;
            Synth.Sfx(SfxType.Push);
        }
        if (inputOn && Inp.Brake) speed = Mathf.MoveTowards(speed, 0, brakeDecel * dt);

        float turnRate = Mathf.Lerp(150f, 85f, Mathf.Abs(speed) / maxSpeed);
        if (inputOn) yaw += Inp.Steer * turnRate * dt;

        // rolled backwards on a slope: revert to face downhill
        if (speed < -.2f) { yaw += 180f; speed = -speed; }
        speed = Mathf.Min(speed, maxSpeed);

        fwdSlope = Vector3.ProjectOnPlane(FlatFwd, groundNormal).normalized;
        var vel = fwdSlope * speed;

        if (inputOn && WantOllie())
        {
            if (manual) EndManual();
            airVel = vel + Vector3.up * jumpVel;
            LeaveGround(true);
            Synth.Sfx(SfxType.Pop);
            return;
        }

        var pos = transform.position;
        var next = pos + vel * dt;
        if (BlockedByWall(pos, vel * dt, out var wallHit))
        {
            if (speed > 6.5f && wallHit.collider.GetComponentInParent<Creature>() == null) { Bail(); return; }
            speed = 0; next = pos;
            Synth.Sfx(SfxType.Bonk);
        }

        if (Physics.Raycast(next + Vector3.up * 1.2f, Vector3.down, out var hit, 1.2f + .45f + Mathf.Abs(vel.y) * dt, ~0, QueryTriggerInteraction.Ignore)
            && hit.normal.y > .55f)
        {
            next.y = hit.point.y;
            groundNormal = Vector3.Slerp(groundNormal, hit.normal, .5f);
            transform.position = next;
        }
        else
        {
            transform.position = next;
            airVel = vel;
            LeaveGround(false);
        }
    }

    void LeaveGround(bool jumped)
    {
        grounded = false;
        noGroundTimer = jumped ? .12f : .03f;
        airTime = 0; airTricks = 0; spinAngle = 0; trick = TrickType.None; clearedObstacle = false; lastCleared = null;
    }

    bool BlockedByWall(Vector3 pos, Vector3 delta, out RaycastHit hit)
    {
        float dist = delta.magnitude + .35f;
        var dir = new Vector3(delta.x, 0, delta.z);
        if (dir.sqrMagnitude < 1e-6f) { hit = default; return false; }
        dir.Normalize();
        if (Physics.SphereCast(pos + Vector3.up * .75f, .28f, dir, out hit, dist, ~0, QueryTriggerInteraction.Ignore))
            return hit.normal.y < .45f;
        return false;
    }

    // ---------------- air ----------------
    void UpdateAir(float dt)
    {
        airTime += dt;
        noGroundTimer -= dt;
        airVel.y -= gravity * dt;

        if (InputEnabled)
        {
            // A/D spin the rider; flips on J/K/L
            spinAngle += Inp.Steer * spinRate * dt;
            if (trick == TrickType.None)
            {
                if (WantTrick(TrickType.Kickflip, Inp.Kickflip)) StartTrick(TrickType.Kickflip);
                else if (WantTrick(TrickType.Heelflip, Inp.Heelflip)) StartTrick(TrickType.Heelflip);
                else if (WantTrick(TrickType.ShoveIt, Inp.ShoveIt)) StartTrick(TrickType.ShoveIt);
            }
        }
        if (trick != TrickType.None)
        {
            trickT += dt / TrickDur;
            if (trickT >= 1f) CompleteTrick();
        }

        var pos = transform.position;
        var delta = airVel * dt;
        if (BlockedByWall(pos, delta, out _))
        {
            airVel.x *= -.2f; airVel.z *= -.2f; delta = airVel * dt;
            Synth.Sfx(SfxType.Bonk);
        }
        var next = pos + delta;

        // jumped over an obstacle?
        if (!clearedObstacle && Physics.Raycast(next + Vector3.up * .2f, Vector3.down, out var below, 3f, ~0, QueryTriggerInteraction.Ignore))
        {
            var ob = below.collider.GetComponentInParent<Obstacle>();
            if (ob != null && ob.jumpable) { clearedObstacle = true; lastCleared = ob; }
        }

        // rails
        if (airVel.y < 1.5f && TryStartGrind(next)) return;

        // landing
        if (noGroundTimer <= 0 && airVel.y <= 0 &&
            Physics.Raycast(next + Vector3.up * .6f, Vector3.down, out var hit, .6f + Mathf.Abs(delta.y) + .05f, ~0, QueryTriggerInteraction.Ignore)
            && hit.normal.y > .55f)
        {
            next.y = hit.point.y;
            transform.position = next;
            Land(hit.normal);
            return;
        }
        transform.position = next;
    }

    void StartTrick(TrickType t)
    {
        trick = t; trickT = 0;
        Synth.Sfx(SfxType.Flip);
    }

    void CompleteTrick()
    {
        airTricks++;
        string name = trick == TrickType.Kickflip ? "Kickflip" : trick == TrickType.Heelflip ? "Heelflip" : "Shove-it";
        int pts = trick == TrickType.ShoveIt ? 75 : 100;
        Synth.TrickNote(airTricks);
        OnTrick?.Invoke(name, pts);
        trick = TrickType.None; trickT = 0;
    }

    void Land(Vector3 normal)
    {
        groundNormal = normal;
        var fwdSlope = Vector3.ProjectOnPlane(FlatFwd, normal).normalized;
        speed = Vector3.Dot(airVel, fwdSlope);
        if (speed < 0) { yaw += 180; speed = -speed; spinAngle += 180; }
        grounded = true;

        // sketchy landings bail
        float off = Mathf.Abs(Mathf.DeltaAngle(spinAngle, Mathf.Round(spinAngle / 180f) * 180f));
        bool trickUnfinished = trick != TrickType.None && trickT < .7f;
        if (trickUnfinished || off > 55f) { Bail(); return; }
        if (trick != TrickType.None) CompleteTrick();

        int halfTurns = Mathf.RoundToInt(Mathf.Abs(spinAngle) / 180f);
        if (halfTurns > 0)
        {
            OnTrick?.Invoke(halfTurns * 180 + " spin", halfTurns * 60);
            Synth.TrickNote(airTricks + halfTurns);
        }
        spinAngle = 0;

        if (clearedObstacle && lastCleared != null)
            OnTrick?.Invoke("Hop over " + ObstacleName(lastCleared.kind), 50);
        if (airTime > 1.1f) OnTrick?.Invoke("Big air", Mathf.RoundToInt(airTime * 40));

        Synth.Sfx(SfxType.Land, Mathf.Clamp01(-airVel.y / 14f));
    }

    static string ObstacleName(ObstacleKind k)
    {
        switch (k)
        {
            case ObstacleKind.Log: return "the log";
            case ObstacleKind.Cone: return "the cone";
            case ObstacleKind.HayBale: return "the hay bale";
            case ObstacleKind.Bin: return "the bin";
            case ObstacleKind.Barrier: return "the barrier";
            default: return "it";
        }
    }

    // ---------------- manual ----------------
    void EndManual()
    {
        if (manualTime > .8f) OnTrick?.Invoke("Manual", Mathf.RoundToInt(manualTime * 40));
        manualTime = 0; manual = false;
    }

    // ---------------- grind ----------------
    bool TryStartGrind(Vector3 feet)
    {
        var hv = new Vector3(airVel.x, 0, airVel.z);
        if (hv.sqrMagnitude < 1f) return false;
        foreach (var r in Rail.All)
        {
            var c = r.Closest(feet, out float t);
            var d = feet - c;
            float dy = d.y; d.y = 0;
            if (d.magnitude > .75f || dy < -.4f || dy > .7f) continue;
            if (t <= 0.001f || t >= .999f) continue;
            float align = Vector3.Dot(hv.normalized, r.Dir);
            if (Mathf.Abs(align) < .5f) continue;
            rail = r; railT = t; railDir = Mathf.Sign(align);
            speed = Mathf.Max(hv.magnitude, 6.5f);
            grinding = true; grindTime = 0;
            var dir = r.Dir * railDir;
            yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (trick != TrickType.None) CompleteTrick();
            spinAngle = 0;
            transform.position = c;
            Synth.Sfx(SfxType.GrindStart);
            return true;
        }
        return false;
    }

    void UpdateGrind(float dt)
    {
        grindTime += dt;
        speed = Mathf.Max(speed - .6f * dt, 5f);
        railT += railDir * speed * dt / rail.Length;
        var dir = rail.Dir * railDir;
        if (railT <= 0 || railT >= 1 || (InputEnabled && WantOllie()))
        {
            bool jumped = railT > 0 && railT < 1;
            EndGrind();
            airVel = dir * speed + Vector3.up * (jumped ? jumpVel : 3.5f);
            LeaveGround(true);
            if (jumped) Synth.Sfx(SfxType.Pop);
            return;
        }
        transform.position = Vector3.Lerp(rail.a, rail.b, railT);
        groundNormal = Vector3.up;
    }

    void EndGrind()
    {
        grinding = false;
        if (grindTime > .25f) OnTrick?.Invoke("50-50 grind", Mathf.RoundToInt(40 + grindTime * 80));
        rail = null;
    }

    // ---------------- obstacles & bails ----------------
    void CheckObstacles()
    {
        if (bailTimer > 0) return;
        var p = transform.position;
        var hits = Physics.OverlapCapsule(p + Vector3.up * .35f, p + Vector3.up * 1.0f, .3f, ~0, QueryTriggerInteraction.Collide);
        foreach (var h in hits)
        {
            var ob = h.GetComponentInParent<Obstacle>();
            if (ob == null) continue;
            // standing on top of a jumpable obstacle is fine
            if (grounded && ob.jumpable && p.y > h.bounds.max.y - .1f) continue;
            Bail();
            return;
        }
    }

    public void Bail()
    {
        if (grinding) { grinding = false; rail = null; }
        bailTimer = 1.4f;
        grounded = true;
        speed = 0;
        trick = TrickType.None; spinAngle = 0; manualTime = 0;
        // knock back a little and settle on the ground
        var back = -FlatFwd * 1.2f;
        var p = transform.position + back;
        if (Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
        transform.position = p;
        groundNormal = Vector3.up;
        Synth.Sfx(SfxType.Bail);
        OnBail?.Invoke();
    }

    void UpdateBail(float dt)
    {
        bailTimer -= dt;
        float k = 1f - bailTimer / 1.4f;
        if (model != null)
        {
            model.rider.localRotation = Quaternion.Euler(Mathf.Sin(k * Mathf.PI) * 80f, k * 720f, 0);
            model.rider.localPosition = new Vector3(0, .26f + Mathf.Sin(k * Mathf.PI) * .8f, 0);
            model.board.localRotation = Quaternion.Euler(0, 0, k * 360f);
        }
        if (bailTimer <= 0 && model != null)
        {
            model.rider.localRotation = Quaternion.identity;
            model.rider.localPosition = new Vector3(0, .26f, 0);
            model.board.localRotation = Quaternion.identity;
        }
    }

    void Respawn()
    {
        Place(checkpoint, checkpointYaw);
        Synth.Sfx(SfxType.Bail);
        OnBail?.Invoke();
    }

    // ---------------- visuals ----------------
    float lean, crouch;
    void UpdateVisual(float dt)
    {
        if (model == null) return;
        transform.rotation = Quaternion.Euler(0, yaw, 0);

        // align board to slope
        var targetUp = grounded || grinding ? groundNormal : Vector3.up;
        var localUp = Quaternion.Inverse(transform.rotation) * targetUp;
        var align = Quaternion.FromToRotation(Vector3.up, localUp);
        model.align.localRotation = Quaternion.Slerp(model.align.localRotation, align, dt * 12f);

        model.spin.localRotation = Quaternion.Euler(0, spinAngle, 0);

        // board tricks
        var boardRot = Quaternion.identity;
        if (trick == TrickType.Kickflip) boardRot = Quaternion.Euler(0, 0, trickT * 360f);
        else if (trick == TrickType.Heelflip) boardRot = Quaternion.Euler(0, 0, -trickT * 360f);
        else if (trick == TrickType.ShoveIt) boardRot = Quaternion.Euler(0, trickT * 180f, 0);
        if (manual) boardRot = Quaternion.Euler(-14f, 0, 0);
        if (grinding) boardRot = Quaternion.Euler(0, 0, Mathf.Sin(grindTime * 30f) * 2f);
        model.board.localRotation = boardRot;
        float boardLift = trick != TrickType.None ? Mathf.Sin(trickT * Mathf.PI) * .35f : 0f;
        model.board.localPosition = new Vector3(0, .16f + boardLift, 0);

        // rider lean into turns, crouch in air
        lean = Mathf.Lerp(lean, grounded ? -Inp.Steer * Speed01 * 18f : 0, dt * 6f);
        crouch = Mathf.Lerp(crouch, grounded && !grinding ? 0f : .12f, dt * 10f);
        model.rider.localRotation = Quaternion.Euler(manual ? -12f : 0, 0, lean);
        model.rider.localPosition = new Vector3(0, .26f + boardLift * .9f - crouch, 0);
        model.AnimateTail(Speed01, Time.time);
    }
}
