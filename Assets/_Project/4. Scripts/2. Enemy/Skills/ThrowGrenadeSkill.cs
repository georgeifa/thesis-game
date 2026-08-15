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

        Vector3 velocity = Helpers.CalculateArcVelocity(
            throwPoint, player.transform.position, minArcHeight, maxArcHeight);
 
        // Launch() also clears hasFuseStarted — a pooled grenade would
        // otherwise detonate on its first frame after reuse.
        grenade.GetComponent<Grenade>().Launch(velocity);
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
        grenade.TargetLayer = playerLayer;
    }
}
