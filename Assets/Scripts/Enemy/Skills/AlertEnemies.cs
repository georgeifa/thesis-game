using System.Collections;
using System.Collections.Generic;
using Unity.Behavior;
using UnityEngine;

[CreateAssetMenu(fileName = "AlertEnemies", menuName = "Skills/Utility Skills/AlertEnemies")]
public class AlertEnemies : SkillsScriptableObject
{
    [SerializeField] private LayerMask TargetLayer;
    [Tooltip("Time between the start of the animation and the time the enemies start getting alerted")]
    public float ReactionTime = 1f;

    private Collider[] Colliders = new Collider[50]; 

    public override void UseSkill(Enemy enemy, GameObject player)
    {
        base.UseSkill(enemy,player);
        enemy.StartCoroutine(AlertEnemiesInRadius(enemy));
    }

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        return !enemy.IsUsingSkill;
    }

    IEnumerator AlertEnemiesInRadius(Enemy enemy)
    {
        enemy.GetComponent<Animator>().SetTrigger("Alert");

        yield return new WaitForSeconds(ReactionTime);

        int count = Physics.OverlapSphereNonAlloc(
            enemy.transform.position, Range, Colliders, TargetLayer);
        

        for (int i = 0; i < count; i++)
        {
            var agent = Colliders[i].GetComponent<BehaviorGraphAgent>();
            if (agent != null)
            {
                agent.SetVariableValue("Alerted", true);
            }

            yield return null;
        }
    }
}
