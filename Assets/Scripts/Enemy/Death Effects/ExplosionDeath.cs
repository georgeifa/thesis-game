using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Explosion Ragdoll Death", menuName = "Deaths/Ragdoll/Explosion Death")]
public class ExplosionDeath : RagdollDeathScriptableObject
{

    public int Damage;
    public float Radius;

    public float Delay = 1f;
    public LayerMask DamagableLayermask = -1;
    public PoolableObject ExplosionPrefab;

    public override void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        enemy.StartCoroutine(Explode(enemy,root,particleSystem));
    }

    IEnumerator Explode(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        yield return new WaitForSeconds(Delay);

        ObjectPool pool = ObjectPool.CreateInstance(ExplosionPrefab,5);
        PoolableObject instance = pool.GetObject();

        if(instance != null)
        {
            instance.transform.position = enemy.transform.position;

            ParticleSystem particle = instance.gameObject.GetComponent<ParticleSystem>();

            if(particle != null)
                particle.Play();

            Collider[] colliders = Physics.OverlapSphere(instance.transform.position, Radius,DamagableLayermask);

            foreach(Collider col in colliders)
            {
                if(col.TryGetComponent(out IDamagable damagable))
                {
                    damagable.TakeDamage(Damage);
                }
                else
                {
                    try{
                        damagable = col.GetComponentInParent<IDamagable>();
                        damagable.TakeDamage(Damage);
                    }
                    catch
                    {
                        Debug.LogError($"No IDamageable found on: {col.gameObject}");
                    }
                }
            }
        }

        if(enemy.AI_Combat.isUsingSkill)
            enemy.AI_Combat.OnSkillComplete();
            
        base.Die(enemy, root, particleSystem);


    }
}
