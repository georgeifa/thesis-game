using UnityEngine;

// Shared gizmo bits. Third thing needing a flat ring is when it stops being
// a private method on MissionObjective and gets its own home.
public static class GizmoDraw
{
    // A ring on the XZ plane. Rolled by hand instead of Handles.DrawWireDisc,
    // which needs a UnityEditor using - and a stray one of those in a runtime
    // script already cost us a broken build once.
    public static void FlatCircle(Vector3 centre, float r, int steps = 40)
    {
        Vector3 prev = centre + new Vector3(r, 0f, 0f);

        for (int i = 1; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}