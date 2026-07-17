using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using MyBox;
using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AI_Locomotion))]
[RequireComponent(typeof(FieldOfView))]
public class AI_Combat : MonoBehaviour
{
    [Header("Has To Be Initialized")]
    public GameObject[] WeaponObjects;
    [Space]
    [Header("Animator Settings")]
    public bool useRootMotion;
    public string attackTrigger = "AttackNo";
    public int attacksCount;
    public string skillTrigger = "SkillNo";

    [Header("Attack Settings")]
    public bool EnemyIn;
    public List<bool> EnemyInSkill;
    public bool isRanged;
    public bool isAttacking;
    public int damage = 15;
    [Tooltip("For Melee Enemies: The maximum attack range for should be the distance of root motion movement +10-15%. (Use Debug Script to find the exact distance)")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public float attackAngle = 90f;
    public bool skillsUnlocked = false;
    
    [Header("Skill Settings")]    
    public SkillsScriptableObject[] Skills;
    public ParticleSystem SkillsVFX;
    
    
    [Header("References")]
    public LayerMask playerLayer;
    
    private List<Collider> weaponsCol;
    private Enemy Enemy;
    private List<EnemyWeapon> enemyWeapons;
    private AI_Locomotion locomotion;
    private Animator animator;
    private FieldOfView FOV;
    private float nextAttackTime = 0f;
    public bool isUsingSkill = false;
    private SkillsScriptableObject skillInUse;
    private Dictionary<SkillsScriptableObject,float> SkillsNextUseTimes;


    private List<SkillsScriptableObject> SkillsWithSeperateRange;
    void Awake()
    {
        locomotion = GetComponent<AI_Locomotion>();
        animator = GetComponent<Animator>();
        FOV = GetComponent<FieldOfView>();
        Enemy = GetComponent<Enemy>();
        SkillsNextUseTimes = new Dictionary<SkillsScriptableObject, float>();
        weaponsCol = new List<Collider>();
        enemyWeapons = new List<EnemyWeapon>(); 
        SkillsWithSeperateRange = new List<SkillsScriptableObject>(); 
        EnemyInSkill = new List<bool>();
    }

    void Start()
    {
        foreach(GameObject weapon in WeaponObjects)
        {
            weaponsCol.Add(weapon.GetComponent<Collider>());
            enemyWeapons.Add(weapon.GetComponent<EnemyWeapon>());
        }

        foreach(SkillsScriptableObject s in Skills)
        {
            SkillsNextUseTimes.Add(s,0f);
            if(s.hasSeperateFOV){
                SkillsWithSeperateRange.Add(s);
                EnemyInSkill.Add(false);
            }
        }

        foreach(EnemyWeapon enemyWeapon in enemyWeapons)
        {
            enemyWeapon.Damage = damage;
            enemyWeapon.playerLayer = playerLayer;
        }

        StartRoutine();
    }

    public void StopRoutine()
    {
        StopAllCoroutines();
        EnemyInSkill.Clear();
    }

    public void StartRoutine()
    {
        StartCoroutine(FOV.FOVRoutine(.1f,true, transform, attackRange, playerLayer, attackAngle, (result) => EnemyIn = result));
        for(int i=0; i<SkillsWithSeperateRange.Count;i++){ 
            int index = i;
            StartCoroutine(FOV.FOVRoutine(.2f,true, transform, SkillsWithSeperateRange[i].Range, playerLayer, attackAngle, (result) => EnemyInSkill[index] = result));
        };
    }

    public SkillsScriptableObject GetSeparateSkill(int i)
    {
        return SkillsWithSeperateRange[i];
    }

    public bool CanAttack()
    {
        return Time.time >= nextAttackTime && !isAttacking && !isUsingSkill;
    }
    
    public void Attack()
    {
        if (!CanAttack()) return;
        
        isAttacking = true;
        locomotion.SetRootMotionMode(useRootMotion);
        int id = UnityEngine.Random.Range(1,attacksCount+1); //select random animation from the attack animations
        animator.SetInteger(attackTrigger,id);
        
        nextAttackTime = Time.time + attackCooldown;
    }

        // Animation Event: Called when attack animation ends
    public void OnAttackComplete()
    {
        isAttacking = false;
        locomotion.SetRootMotionMode(false);
        animator.SetInteger(attackTrigger,0);

    }

    public void EnableCollider(int ColId)
    {
        if(ColId == -1){
            foreach(Collider weaponCol in weaponsCol)
            {
                weaponCol.isTrigger = true;
                weaponCol.includeLayers = playerLayer;
            }
        }
        else
        {
            weaponsCol[ColId].isTrigger = true;
            weaponsCol[ColId].includeLayers = playerLayer;
        }
    }

    public void DisableCollider(int ColId)
    {
        if(ColId == -1){
            foreach(Collider weaponCol in weaponsCol)
            {
                weaponCol.isTrigger = false;
                weaponCol.includeLayers = 0;
            }
        }
        else
        {
            weaponsCol[ColId].isTrigger = true;
            weaponsCol[ColId].includeLayers = playerLayer;
        }
    }

    public bool CanUseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        return skillsUnlocked && Time.time >= SkillsNextUseTimes[Skill] && !isAttacking && !isUsingSkill && Skill.CanUseSkill(Enemy,Player);
    }

    public void UseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        isUsingSkill = true;
        skillInUse = Skill;

        animator.SetInteger(skillTrigger,Skills.IndexOfItem(Skill)+1);

        Skill.UseSkill(Enemy,Player);

    }

    public void OnSkillComplete()
    {
        animator.SetInteger(skillTrigger,0);
        SkillsNextUseTimes[skillInUse] = Time.time + skillInUse.Cooldown;
        skillInUse = null;
        isUsingSkill = false;
    }
}