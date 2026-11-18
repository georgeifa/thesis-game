using UnityEngine;
using UnityEngine.AI;

// An authored area reinforcements can be dropped into. Knows whether it's
// usable right now, which is mostly about not spawning things in the player's
// face.
public class SpawnZone : MonoBehaviour
{
    public enum Validity { Invalid, Acceptable, Ideal }

    [SerializeField] float radius = 8f;
    [Tooltip("Random points tried before giving up on this zone this time.")]
    [SerializeField] int samplesPerPick = 6;

    public float Radius => radius;

    // Last result, only so the gizmo can colour it in play mode.
    Validity lastValidity = Validity.Ideal;

    // Judged on the zone's centre, not the point that ends up chosen. A zone is
    // ~16m across and the isometric camera sees ~40m, so the error is small -
    // and it keeps the tier readable on the gizmo while you're placing them.
    public Validity Evaluate(Vector3 playerPos, float minDistance, Camera cam)
    {
        Vector3 d = transform.position - playerPos;
        d.y = 0f;

        if (d.sqrMagnitude < minDistance * minDistance)
            lastValidity = Validity.Invalid;
        else
            lastValidity = IsOnScreen(cam) ? Validity.Acceptable : Validity.Ideal;

        return lastValidity;
    }

    bool IsOnScreen(Camera cam)
    {
        if (cam == null) return false;

        Vector3 v = cam.WorldToViewportPoint(transform.position);

        // A little margin past the edges, so a zone just off-frame still
        // counts as visible - an enemy appearing one step off-screen reads as
        // a pop-in the moment the camera drifts.
        return v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
    }

    public bool TryGetPoint(out Vector3 point)
    {
        for (int i = 0; i < samplesPerPick; i++)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 guess = transform.position + new Vector3(c.x, 0f, c.y);

            if (NavMesh.SamplePosition(guess, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }

        point = Vector3.zero;
        return false;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = !Application.isPlaying       ? new Color(0.3f, 0.7f, 1f, 0.7f)
                     : lastValidity == Validity.Ideal      ? Color.green
                     : lastValidity == Validity.Acceptable ? Color.yellow
                                                           : Color.red;

        GizmoDraw.FlatCircle(transform.position, radius);
    }
}