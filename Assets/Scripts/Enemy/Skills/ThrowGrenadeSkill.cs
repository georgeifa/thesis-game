using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "Throw Grenade Skill", menuName = "Skills/Combat Skills/Throw Grenade Skill")]
public class ThrowGrenadeSkill : SkillsScriptableObject
{
    [Header("Additional Range Properties")]
    public float minRange;
    [Header("Grenade Properties")]
    public PoolableObject grenadePrefab;
    public float grenadeExplodeAfter;
    public ExplosionScriptableObject ExplosionVFX;
    public LayerMask playerLayer;
    [Header("Throw Properties")]
    public float minArcHeight;
    public float maxArcHeight;

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        float distance = Vector3.Distance(enemy.transform.position,player.transform.position);

        return base.CanUseSkill(enemy,player) && distance >= minRange;
    }

    public override void UseSkill(Enemy enemy, GameObject player)
    {
        
        enemy.StartCoroutine(ThrowGrenade(enemy,player));
    }

    IEnumerator ThrowGrenade(Enemy enemy, GameObject player)
    {
        yield return new WaitForSeconds(.1f);

        Vector3 throwPoint = enemy.AI_Combat.SkillsVFX.transform.position;

        ObjectPool pool = ObjectPool.CreateInstance(grenadePrefab,5);

        PoolableObject grenade = pool.GetObject();
        grenade.transform.SetPositionAndRotation(throwPoint, Quaternion.identity);

                // Create grenade
        SetupGrenade(grenade);

        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        
        // Calculate velocity for arc
        Vector3 velocity = CalculateArcVelocity(throwPoint, player.transform.position);
        
        // Apply velocity
        rb.linearVelocity = velocity;
        
        // Add some spin (optional)
        rb.angularVelocity = new Vector3(Random.Range(1f, 7f), 0, 0);
    }

    private void SetupGrenade(PoolableObject grenadeOBJ)
    {
        Grenade grenade = grenadeOBJ.GetComponent<Grenade>();
        grenade.BlastRadius = Range;
        grenade.Damage = Damage;

        ObjectPool pool = ObjectPool.CreateInstance(ExplosionVFX.explosionPrefab,5);

        PoolableObject BlastVFX = pool.GetObject();
        BlastVFX.gameObject.SetActive(false);
        ExplosionVFX.SetupExplosion(BlastVFX.gameObject);
        
        grenade.BlastVFX = BlastVFX;
        grenade.ExplodeAfter = grenadeExplodeAfter;
        grenade.PlayerLayer = playerLayer;
    }
    
    private Vector3 CalculateArcVelocity(Vector3 start, Vector3 target)
    {
        // Get horizontal direction and distance
        Vector3 horizontalDirection = target - start;
        horizontalDirection.y = 0;  // Remove height difference
        float horizontalDistance = horizontalDirection.magnitude;
        
        // Normalize for direction
        horizontalDirection.Normalize();
        
        // Calculate time based on distance (faster for longer throws)
        float flightTime = Mathf.Sqrt(horizontalDistance) * 0.5f;
        
        // Calculate vertical and horizontal velocities
        // Random arc height
        float arcHeight = Random.Range(minArcHeight, maxArcHeight);
        float verticalVelocity = (arcHeight + (target.y - start.y)) / flightTime + 0.5f * Mathf.Abs(Physics.gravity.y) * flightTime;
        float horizontalVelocity = horizontalDistance / flightTime;
        
        // Combine into final velocity
        Vector3 velocity = (horizontalDirection * horizontalVelocity) + (Vector3.up * verticalVelocity);
        
        return velocity;
    }
}
