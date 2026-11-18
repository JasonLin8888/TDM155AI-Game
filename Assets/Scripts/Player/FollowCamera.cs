using UnityEngine;

/// Over-the-shoulder chase camera. Orbits slowly on the title screen.
public class FollowCamera : MonoBehaviour
{
    public Skater target;
    public bool titleOrbit = true;
    public Vector3 shoulder = new Vector3(.9f, 2.4f, -5.2f);
    Camera cam;
    float orbit;
    Vector3 vel;
    Vector3 lookPoint;

    void Awake() { cam = GetComponent<Camera>(); }

    void LateUpdate()
    {
        if (target == null) return;
        var tp = target.transform.position;
        var rot = Quaternion.Euler(0, target.Yaw, 0);
        Vector3 desired; Vector3 look;

        if (titleOrbit)
        {
            orbit += Time.deltaTime * 8f;
            var r = Quaternion.Euler(0, target.Yaw + 150f + orbit, 0);
            desired = tp + r * new Vector3(0, 1.6f, 4.2f);
            look = tp + Vector3.up * .9f;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, .35f);
            lookPoint = Vector3.Lerp(lookPoint, look, Time.deltaTime * 6f);
            transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
            if (cam) cam.fieldOfView = 55f;
            return;
        }

        float s = target.Speed01;
        var off = shoulder; off.z -= s * 1.4f; off.y += s * .3f;
        desired = tp + rot * off;
        // keep the camera above the ground
        if (Physics.Raycast(desired + Vector3.up * 5f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
            desired.y = Mathf.Max(desired.y, hit.point.y + .8f);
        look = tp + Vector3.up * 1.1f + rot * new Vector3(.3f, 0, 4.5f);

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref vel, target.Bailed ? .5f : .14f);
        lookPoint = Vector3.Lerp(lookPoint, look, Time.deltaTime * 10f);
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
        if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + s * 14f, Time.deltaTime * 3f);
    }

    public void Snap()
    {
        if (target == null) return;
        var rot = Quaternion.Euler(0, target.Yaw, 0);
        transform.position = target.transform.position + rot * shoulder;
        lookPoint = target.transform.position + Vector3.up * 1.1f + rot * new Vector3(.3f, 0, 4.5f);
        transform.rotation = Quaternion.LookRotation(lookPoint - transform.position);
        vel = Vector3.zero;
    }
}
