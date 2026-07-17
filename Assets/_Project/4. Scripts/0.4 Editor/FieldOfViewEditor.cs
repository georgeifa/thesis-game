using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FieldOfView))]
public class FieldOfViewEditor : Editor
{
    private void OnSceneGUI()
    {
        FieldOfView fov = (FieldOfView)target;
        Handles.color = Color.white;
        float Radius = Helpers.RangeWithColliderOffset(fov.gameObject, fov.radius);

        Handles.DrawWireArc(fov.transform.position, Vector3.up, Vector3.forward, 360, Radius);

        Vector3 viewAngle01 = DirectionFromAngle(fov.transform.eulerAngles.y, -fov.DetectionAngle / 2);
        Vector3 viewAngle02 = DirectionFromAngle(fov.transform.eulerAngles.y, fov.DetectionAngle / 2);

        Handles.color = Color.yellow;
        Handles.DrawLine(fov.transform.position, fov.transform.position + viewAngle01 * Radius);
        Handles.DrawLine(fov.transform.position, fov.transform.position + viewAngle02 * Radius);

        if (fov.playerDetected)
        {
            Handles.color = Color.green;
            Handles.DrawLine(fov.transform.position, fov.playerRef.transform.position);
        }
    }

    private Vector3 DirectionFromAngle(float eulerY, float angleInDegrees)
    {
        angleInDegrees += eulerY;

        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}
