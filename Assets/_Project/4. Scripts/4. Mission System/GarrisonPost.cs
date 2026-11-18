using UnityEngine;

// A guard position. Holds a fixed number of one enemy type. GarrisonManager
// fills it at mission start and refills it slowly when guards die.
public class GarrisonPost : MonoBehaviour
{
    public EnemyScriptableObject enemyType;

    [Tooltip("How many stand here.")]
    public int slots = 1;

    [Tooltip("Scatter around the post so they don't spawn stacked on one point.")]
    public float spread = 3f;

    public Vector3 PickPoint()
    {
        Vector2 c = Random.insideUnitCircle * spread;
        return transform.position + new Vector3(c.x, 0f, c.y);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        GizmoDraw.FlatCircle(transform.position, spread);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2f);
    }
}