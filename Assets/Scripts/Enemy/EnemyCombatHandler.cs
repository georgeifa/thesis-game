using System;
using System.Collections;
using MyBox;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

public class EnemyCombatHandler : MonoBehaviour
{
    public GameObject WeaponParent;

    public bool isRanged;

    public int Damage;
    public float Range;
    public float AttackRate;

    public bool ReadyToAttack;
    public bool isAttacking = false;
    public SkillsScriptableObject Skill;
    public ParticleSystem SkillsVFX;

    public BehaviorGraphAgent behavior;
    public Animator animator;

    private NavMeshAgent agent;



    [Header("Ranged Attacks Properties")]
    [ConditionalField(nameof(isRanged))] public GameObject Shootpoint;

    public event Action<EnemyCombatHandler,GameObject> PerformAttackAction;

    public Coroutine currentShootCoroutine;

    void Awake()
    {

        behavior = GetComponent<BehaviorGraphAgent>();
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        animator.applyRootMotion = true;
        if(SkillsVFX != null)
            SkillsVFX.Play();

    }

    public void Attack(GameObject Player)
    {
        ReadyToAttack = false;
        isAttacking = true;
        animator.SetTrigger("Attack"); //deprecated
        PerformAttack(Player);

    }

    void OnAnimatorMove()
    {
        Vector3 rootPostion = animator.rootPosition;

        rootPostion.y = agent.nextPosition.y;
        transform.position = rootPostion;
        agent.nextPosition = rootPostion;

    }

    public void PerformAttack(GameObject player)
    {
        PerformAttackAction?.Invoke(this,player);
    }

    public void CompleteAttack()
    {
        isAttacking = false;
        behavior.SetVariableValue("AttackComplete",true);

        StartCoroutine(AttackRateTimer());
    }

    private IEnumerator AttackRateTimer()
    {
        //When enemy is ranged we use 0 attack rate because aiming is in the action
        //Instead we use the attack rate as the time needed to shoot after they start aiming
        if(!isRanged)
            yield return new WaitForSeconds(AttackRate);

        ReadyToAttack = true;
 
    }

    public void StopSkill()
    {
        SkillsVFX.Stop();
    }
}
