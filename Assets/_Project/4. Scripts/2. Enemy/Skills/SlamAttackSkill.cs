using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Slam Attack Skill", menuName = "Skills/Combat Skills/Slam Attack Skill")]
public class SlamAttackSkill : SkillsScriptableObject
{
    public PoolableObject VFX_Prefab;
    public float WindUpTime;
    public Vector3 SpawnOffset;
    public LayerMask PlayerLayerMask;

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        return base.CanUseSkill(enemy,player);
    }

    public override void UseSkill(Enemy enemy, GameObject player)
    {
        enemy.StartCoroutine(Slam(enemy,player));
    }

    private IEnumerator Slam(Enemy enemy, GameObject player)
    {
        yield return new WaitForSeconds(WindUpTime);
        ObjectPool pool = ObjectPool.CreateInstance(VFX_Prefab,5);
        PoolableObject instance = pool.GetObject();

        if(instance != null)
        {
            instance.transform.SetParent(enemy.transform,false);

            instance.transform.localPosition += SpawnOffset;
            instance.transform.localScale = new Vector3(Range,Range,Range);
        }

        Collider[] colliders = new Collider[4];

        int numColliders = Physics.OverlapSphereNonAlloc(enemy.transform.position, Range,colliders,PlayerLayerMask);

        if (numColliders > 0)
        {
            player.GetComponent<IDamagable>().TakeDamage(Damage);
        }

        while(enemy.AI_Combat.isUsingSkill)
            yield return null;

        pool.ResetParent(instance);
        instance.gameObject.SetActive(false);

    }
}
