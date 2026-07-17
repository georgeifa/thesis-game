using System.Data;
using JetBrains.Annotations;
using MyBox;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem.UI;

[BlackboardEnum]
public enum AIState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    UsingSkill
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BehaviorGraphAgent),typeof(NavMeshAgent),typeof(FieldOfView))]
public class Enemy : PoolableObject, IDamagable
{
    public AIState CurrentState;
    public int Health = 100;
    [Space]
    [SerializeField]
    private Transform root;
    [Space]
    [Header("Animator Settings")]
    public string ResetParam = "Reset";
    public string HitDirectionParam = "HitDirection";

    [Space]
    [Header("Components")]
    public BehaviorGraphAgent behavior;
    public NavMeshAgent Agent;
    public Animator Animator;
    public AI_Locomotion AI_Locomotion;
    public FieldOfView FOV;
    public AI_Combat AI_Combat;

    public event IDamagable.TakeDamageEvent OnTakeDamage;
    public event IDamagable.DeathEvent OnDeath;

    [Space]
    [Header("Utility Skills")]
    public bool hasUtilitySkill;
    public SkillsScriptableObject UtilitySkill;
    public bool IsUsingSkill;
    private float UseTime;

    [Space]
    [Header("Death Components")]
    [SerializeField]
    private ParticleSystem deathParticle;
    public DeathScriptableObject DeathSO;

    public int CurrentHealth { get => Health; private set => Health = value; }
    public int MaxHealth { get => Health; private set => Health = value; }



    void OnEnable()
    {
        OnDeath += Die;
        Animator.SetTrigger(ResetParam);
    }

    void Awake()
    {
        if(behavior == null) behavior = GetComponent<BehaviorGraphAgent>();
        if(Agent == null) Agent = GetComponent<NavMeshAgent>();
        if(Animator == null) Animator = GetComponent<Animator>();
        if(FOV == null) FOV = GetComponent<FieldOfView>();
        if(AI_Combat == null) AI_Combat = GetComponent<AI_Combat>();
        if(AI_Locomotion == null) AI_Locomotion = GetComponent<AI_Locomotion>();

    }

    public override void OnDisable()
    {
        UseTime = 0f;
        IsUsingSkill = false;
        OnDeath -= Die;

        base.OnDisable();
    }

    public void TakeDamage(int Damage)
    {
        int damageTaken = Mathf.Clamp(Damage, 0, CurrentHealth);

        CurrentHealth -= damageTaken;

        if (damageTaken != 0)
        {
            OnTakeDamage?.Invoke(damageTaken);
        }
        
        if(CurrentHealth == 0 && damageTaken != 0)
        {
            OnDeath?.Invoke();
        }

        Animator.SetInteger(HitDirectionParam,0);

    }

    public void GetHitDirection(Vector3 hitPoint)
    {
        Vector3 localPoint = transform.InverseTransformPoint(hitPoint);

        // Hit on front or back
        if(localPoint.z > 0)
            Animator.SetInteger(HitDirectionParam,1);
        else
            Animator.SetInteger(HitDirectionParam,2);
    }

    public bool CanUseSkill(GameObject Player)
    {
        return UtilitySkill.CanUseSkill(this,Player) && UseTime + UtilitySkill.Cooldown < Time.time;
    }

    public void UseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        IsUsingSkill = true;
        Skill.UseSkill(this,Player);
    }

    public void CompleteUtillitySkill()
    {
        UseTime = Time.time;
        IsUsingSkill = false;
    }


    public void Die()
    {
        AI_Locomotion.Stop();

        Health = 0;
        DeathSO.Die(this,root, deathParticle);
    }

}
