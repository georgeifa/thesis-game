using System;
using System.Collections;
using FIMSpace.FProceduralAnimation;
using MyBox;
using Unity.Behavior;
using Unity.VisualScripting;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "Combat Config", menuName = "Enemy/Combat Config")]
public class CombatConfigurationScriptableObject : ScriptableObject
{
    [Header("Animator Settings")]
    public bool useRootMotion = false;
    public string attackTrigger = "AttackNo";
    [Tooltip("The number of different attack animations the enemy has")]
    public int attacksCount = 3;
    public string skillTrigger = "SkillNo";
    [Header("Attack Settings")]
    public bool isRanged = false;
    public int damage = 15;
    [Tooltip("For Melee Enemies: The maximum attack range for should be the distance of root motion movement +10-15%. (Use Debug Script to find the exact distance)")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public float attackAngle = 90f;
    
    [Header("Skill Settings")]    
    public SkillsScriptableObject[] Skills;
    
    [Header("References")]
    public LayerMask playerLayer;


    [Header("Ranged Attacks Properties")]
    [ConditionalField(nameof(isRanged))] public Vector3 ShootpointPosition;
    [ConditionalField(nameof(isRanged))] public bool hasLaserSight;

    [ConditionalField(nameof(hasLaserSight),nameof(isRanged))] public LaserSightConfigScriptableObject laserSightConfig;
    [ConditionalField(nameof(isRanged))] public PoolableObject ProjectilePrefab;


    public void Setup_CombatConfig(Enemy enemy)
    {
        enemy.AI_Combat.useRootMotion = useRootMotion;
        enemy.AI_Combat.attackTrigger = attackTrigger;
        enemy.AI_Combat.attacksCount = attacksCount;
        enemy.AI_Combat.skillTrigger = skillTrigger;


        enemy.AI_Combat.isRanged = isRanged;
        enemy.AI_Combat.damage = damage;
        enemy.AI_Combat.attackRange = attackRange;
        enemy.AI_Combat.attackCooldown = attackCooldown;
        enemy.AI_Combat.attackAngle = attackAngle;

        enemy.AI_Combat.Skills = Skills;

        enemy.AI_Combat.playerLayer = playerLayer;


/*
        if (isRanged)
        {
            GameObject Shootpoint = new GameObject();
            Shootpoint.name = "Shootpoint";
            Shootpoint.transform.parent = enemy.CombatHandler.WeaponParent.transform;
            Shootpoint.transform.localPosition = ShootpointPosition;
            Shootpoint.transform.localRotation = Quaternion.Euler(0f,-90f,0f);
            Shootpoint.transform.localScale = Vector3.one;


            if(hasLaserSight){
                LineRenderer laser = Shootpoint.AddComponent<LineRenderer>();
                laserSightConfig.SetupLaserSight(laser,Range);
            }

            Shootpoint.SetActive(false);

            enemy.CombatHandler.Shootpoint = Shootpoint;

        }


        if(isRanged)
            enemy.CombatHandler.PerformAttackAction += PerformRangedAttack;
        else
            enemy.CombatHandler.PerformAttackAction += PerformMeleeAttack;
*/

    }


/*
    private void PerformRangedAttack(EnemyCombatHandler combatHandler, GameObject Player)
    {

         combatHandler.StartCoroutine(Aim(combatHandler,Player));

        // Start Countdown

        //While countdown > 0 rotate towards player

        // When countdown = 0 shoot
            // Add forces from shot to whole body
            // Play recoil Animation
    }

    IEnumerator Aim(EnemyCombatHandler combat, GameObject player)
    {
        // Activate Laser
        combat.Shootpoint.SetActive(true);

        combat.animator.SetBool("isAiming",true);

        for(float time = 0; time < combat.AttackRate; time += Time.deltaTime)
        {
            if(!combat.isAttacking) break;
            combat.transform.LookAt(player.transform.position);
            yield return null;    
        }

        if(combat.isAttacking)
            Shoot(combat,player);  

        combat.animator.SetBool("isAiming",false);

        combat.Shootpoint.SetActive(false);

        combat.CompleteAttack();
    }

    private void Shoot(EnemyCombatHandler combat, GameObject player)
    {
        ObjectPool pool = ObjectPool.CreateInstance(ProjectilePrefab,5);

        PoolableObject instance = pool.GetObject();
        if(instance != null)
        {
            combat.animator.SetTrigger("Attack");

            instance.transform.position = combat.Shootpoint.transform.position;
            instance.transform.forward = combat.Shootpoint.transform.forward;

            instance.GetComponent<Rigidbody>().AddForce(instance.transform.forward * 10f, ForceMode.Impulse);


        }
        else
        {
            Debug.Log("No instance of projectilie found");
        }
    }

*/
}
