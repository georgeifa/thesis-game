using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Breath Type Skill", menuName = "Skills/Combat Skills/Breath Type Skill")]
public class BreathTypeSkill : SkillsScriptableObject
{
    public float Duration = 3f;
    public float TickRate = .5f;

    public PoolableObject Prefab;

    public float WindUpTime = 1.5f;
    public PoolableObject windUpPrefab;

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        return base.CanUseSkill(enemy,player);
    }

    public override void UseSkill(Enemy enemy, GameObject player)
    {
        enemy.StartCoroutine(BreathSkill(enemy,player));
    }

    private IEnumerator BreathSkill(Enemy enemy, GameObject player)
    {
        yield return new WaitForSeconds(.5f); // give some time for the animation to play
        ParticleSystem skillsVFX = enemy.AI_Combat.SkillsVFX;

        ObjectPool pool = ObjectPool.CreateInstance(windUpPrefab,5);

        PoolableObject instance = pool.GetObject();
        ParticleSystem particle = null;
        if(instance != null)
        {
            particle = instance.gameObject.GetComponent<ParticleSystem>();

            instance.transform.SetParent(skillsVFX.transform,false);
        
            particle.Play();

            for(float time = 0; time < WindUpTime; time += Time.deltaTime)
            {
                enemy.transform.LookAt(player.transform.position);
                yield return null;    
            }

            
            pool.ResetParent(instance);
            instance.gameObject.SetActive(false);
        }



        pool = ObjectPool.CreateInstance(Prefab,5);

        instance = pool.GetObject();

        if(instance != null)
        {
            particle = instance.gameObject.GetComponent<ParticleSystem>();

            instance.transform.SetParent(skillsVFX.transform,false);
            AreaDamage areaDamage = instance.GetComponentInChildren<AreaDamage>();

            areaDamage.Damage = Damage;
            areaDamage.TickRate = TickRate;

            particle.Play();
        }

        for(float time = 0; time < Duration; time += Time.deltaTime)
        {
            //enemy.transform.LookAt(player.transform.position);
            yield return null;    
        }


        if(particle != null)
            particle.Stop();

        pool.ResetParent(instance);
        instance.gameObject.SetActive(false);


        enemy.AI_Combat.OnSkillComplete();
    }
}
