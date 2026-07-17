using Unity.VisualScripting;
using UnityEngine;

[CreateAssetMenu(fileName = "Laser Sight Config", menuName = "Enemy/Ranged Specific/Laser Sight Config")]
public class LaserSightConfigScriptableObject : ScriptableObject
{
    public Material material;

    public void SetupLaserSight(LineRenderer lineRenderer,float Range){

        lineRenderer.material = material;
        lineRenderer.useWorldSpace = false;

        LaserSight laser = lineRenderer.AddComponent<LaserSight>();
        laser.MaxRange = Range;
    }
}
