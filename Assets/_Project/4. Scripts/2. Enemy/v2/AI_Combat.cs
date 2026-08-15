using System.Collections;
using System.Collections.Generic;
using MyBox;
using UnityEngine;

/// <summary>
/// An enemy's attacking and skill use.
///
/// Melee damage is applied by sampling an overlap around the weapon for a short
/// window at the strike frame, rather than by enabling a trigger collider. The
/// window closes on a timer, so an enemy that dies or is interrupted mid-swing
/// leaves nothing behind — the old enable/disable pair depended on a second
/// animation event that would never fire.
/// </summary>
[RequireComponent(typeof(AI_Locomotion))]
[RequireComponent(typeof(FieldOfView))]
public class AI_Combat : MonoBehaviour
{
    #region Inspector

    [Header("Has To Be Initialized")]
    [Tooltip("Weapon objects — used as the ORIGIN of the melee overlap.")]
    public GameObject[] WeaponObjects;

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
    [Tooltip("For melee: root motion travel distance +10-15%.")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    public float attackAngle = 90f;
    public bool skillsUnlocked = false;

    [Header("Melee Hit")]
    [Tooltip("Overlap radius around the weapon during the strike.")]
    public float hitRadius = 0.6f;
    [Tooltip("How long the strike stays live. Should cover the swing's contact frames.")]
    public float hitWindowDuration = 0.15f;
    [Tooltip("Safety net: force-completes an attack if its end event never fires.")]
    public float maxAttackDuration = 3f;

    [Header("Skill Settings")]
    public SkillsScriptableObject[] Skills;
    public ParticleSystem SkillsVFX;

    [Header("References")]
    public LayerMask playerLayer;

    #endregion

    #region State

    private Enemy Enemy;
    private AI_Locomotion locomotion;
    private Animator animator;
    private FieldOfView FOV;

    private float nextAttackTime;
    private float attackStartTime;

    public bool isUsingSkill = false;
    private SkillsScriptableObject skillInUse;
    private Dictionary<SkillsScriptableObject, float> SkillsNextUseTimes;
    private List<SkillsScriptableObject> SkillsWithSeperateRange;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        locomotion = GetComponent<AI_Locomotion>();
        animator   = GetComponent<Animator>();
        FOV        = GetComponent<FieldOfView>();
        Enemy      = GetComponent<Enemy>();

        SkillsNextUseTimes      = new Dictionary<SkillsScriptableObject, float>();
        SkillsWithSeperateRange = new List<SkillsScriptableObject>();
        EnemyInSkill            = new List<bool>();
    }

    private void Start()
    {
        foreach (SkillsScriptableObject s in Skills)
            SkillsNextUseTimes.Add(s, 0f);

        StartRoutine();
    }

    private void Update()
    {
        // If the attack-complete animation event never fires (interrupted clip,
        // retimed animation), the enemy would be locked out of attacking
        // forever. Time-limited so it recovers on its own.
        if (isAttacking && Time.time > attackStartTime + maxAttackDuration)
        {
            Debug.LogWarning($"{name}: attack never completed — forcing reset.");
            OnAttackComplete();
        }
    }

    #endregion

    #region FOV Routines

    public void StartRoutine()
    {
        // Rebuilt here rather than in Start so StopRoutine → StartRoutine is safe.
        SkillsWithSeperateRange.Clear();
        EnemyInSkill.Clear();

        foreach (SkillsScriptableObject s in Skills)
        {
            if (!s.hasSeperateFOV) continue;
            SkillsWithSeperateRange.Add(s);
            EnemyInSkill.Add(false);
        }

        StartCoroutine(FOV.FOVRoutine(.1f, true, transform, attackRange,
            playerLayer, attackAngle, (result) => EnemyIn = result));

        for (int i = 0; i < SkillsWithSeperateRange.Count; i++)
        {
            int index = i;
            StartCoroutine(FOV.FOVRoutine(.2f, true, transform,
                SkillsWithSeperateRange[i].Range, playerLayer, attackAngle,
                (result) => EnemyInSkill[index] = result));
        }
    }

    public void StopRoutine()
    {
        StopAllCoroutines();
        EnemyInSkill.Clear();
    }

    public SkillsScriptableObject GetSeparateSkill(int i) => SkillsWithSeperateRange[i];

    #endregion

    #region Attacking

    public bool CanAttack() => Time.time >= nextAttackTime && !isAttacking && !isUsingSkill;

    public void Attack()
    {
        if (!CanAttack()) return;

        isAttacking = true;
        attackStartTime = Time.time;
        locomotion.SetRootMotionMode(useRootMotion);

        int id = Random.Range(1, attacksCount + 1);   // random attack animation
        animator.SetInteger(attackTrigger, id);

        nextAttackTime = Time.time + attackCooldown;
    }

    /// <summary>Animation event at the end of the attack clip.</summary>
    public void OnAttackComplete()
    {
        isAttacking = false;
        locomotion.SetRootMotionMode(false);
        animator.SetInteger(attackTrigger, 0);
    }

    #endregion

    #region Melee Hit Detection

    /// <summary>
    /// Animation event at the swing's contact frame. Opens a short hit window
    /// that samples an overlap around the given weapon each frame, so the whole
    /// arc connects rather than a single instant. Each target is damaged once.
    /// Pass the index into WeaponObjects (-1 for all weapons).
    /// </summary>
    public void MeleeStrike(int weaponIndex)
    {
        StartCoroutine(HitWindow(weaponIndex));
    }

    private IEnumerator HitWindow(int weaponIndex)
    {
        // Tracked across the window so a target caught on several frames only
        // takes damage once per swing.
        HashSet<IDamagable> alreadyHit = new();

        for (float elapsed = 0f; elapsed < hitWindowDuration; elapsed += Time.deltaTime)
        {
            SampleHit(weaponIndex, alreadyHit);
            yield return null;
        }
    }

    private void SampleHit(int weaponIndex, HashSet<IDamagable> alreadyHit)
    {
        if (WeaponObjects == null || WeaponObjects.Length == 0) return;

        if (weaponIndex == -1)
        {
            foreach (GameObject weapon in WeaponObjects)
                OverlapFrom(weapon.transform.position, alreadyHit);
        }
        else if (weaponIndex >= 0 && weaponIndex < WeaponObjects.Length)
        {
            OverlapFrom(WeaponObjects[weaponIndex].transform.position, alreadyHit);
        }
    }

    private void OverlapFrom(Vector3 origin, HashSet<IDamagable> alreadyHit)
    {
        Collider[] hits = Physics.OverlapSphere(origin, hitRadius, playerLayer);

        foreach (Collider col in hits)
        {
            // GetComponentInParent checks the object itself first, so this
            // works whether IDamagable sits on the collider or an ancestor.
            IDamagable target = col.GetComponentInParent<IDamagable>();
            if (target == null || !alreadyHit.Add(target)) continue;

            // Direction first — TakeDamage clears the animator's hit-direction.
            target.GetHitDirection(transform.position);
            target.TakeDamage(damage);
        }
    }

    #endregion

    #region Skills

    public bool CanUseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        return skillsUnlocked
            && Time.time >= SkillsNextUseTimes[Skill]
            && !isAttacking
            && !isUsingSkill
            && Skill.CanUseSkill(Enemy, Player);
    }

    public void UseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        isUsingSkill = true;
        skillInUse = Skill;

        animator.SetInteger(skillTrigger, Skills.IndexOfItem(Skill) + 1);
        Skill.UseSkill(Enemy, Player);
    }

    public void OnSkillComplete()
    {
        animator.SetInteger(skillTrigger, 0);

        if (skillInUse != null)
            SkillsNextUseTimes[skillInUse] = Time.time + skillInUse.Cooldown;

        skillInUse = null;
        isUsingSkill = false;
    }

    #endregion

    private void OnDrawGizmosSelected()
    {
        if (WeaponObjects == null) return;

        Gizmos.color = Color.red;
        foreach (GameObject weapon in WeaponObjects)
            if (weapon != null)
                Gizmos.DrawWireSphere(weapon.transform.position, hitRadius);
    }
}