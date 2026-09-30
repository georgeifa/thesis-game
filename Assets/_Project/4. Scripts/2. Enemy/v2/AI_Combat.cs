using System.Collections;
using System.Collections.Generic;
using MyBox;
using UnityEngine;

/// <summary>
/// An enemy's attacking and skill use — the "what am I doing right now" half of
/// combat. The behavior graph decides WHETHER to attack; this decides how, and
/// owns the state while it happens.
///
/// ── TWO DESIGN DECISIONS WORTH KNOWING ──────────────────────────────────
///
/// 1. MELEE IS AN OVERLAP, NOT A TRIGGER COLLIDER.
///    An animation event at the contact frame opens a short window that samples
///    an OverlapSphere around the weapon each frame. The window closes on a
///    timer, so an enemy that dies or is interrupted mid-swing leaves nothing
///    behind. The old enable/disable collider pair depended on a SECOND
///    animation event that never fired when the clip was cut short.
///
/// 2. SKILLS RETURN THEIR COROUTINE, THEY DON'T START IT.
///    SkillsScriptableObject.Execute() is an IEnumerator that this class starts
///    and holds the handle to. A skill that started its own coroutine would be
///    unstoppable from outside — and stopping it is the entire point, because
///    that is what lets the player interrupt a reinforcement call by shooting
///    the caller.
///
/// ── WATCHDOGS ───────────────────────────────────────────────────────────
/// Attack and skill completion are driven by ANIMATION EVENTS, which do not
/// fire if a clip is interrupted or retimed. Both have a timeout that forces
/// completion, because the failure mode otherwise is an enemy that is alive,
/// chases you, and never acts again — silently, forever.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(AI_Locomotion))]
[RequireComponent(typeof(FieldOfView))]
public class AI_Combat : MonoBehaviour
{
    #region Inspector — Weapons

    [Header("Has To Be Initialized")]
    [Tooltip("Weapon objects — used as the ORIGIN of the melee overlap, not as " +
             "colliders. One entry per weapon; MeleeStrike takes the index.")]
    public GameObject[] WeaponObjects;

    #endregion

    #region Inspector — Animator

    [Header("Animator Settings")]
    [Tooltip("Whether attacks and skills move the enemy via root motion.")]
    public bool useRootMotion;
    [Tooltip("Int param selecting which attack plays. 0 = none.")]
    public string attackTrigger = "AttackNo";
    [Tooltip("How many attack animations this enemy has, for the random pick.")]
    public int attacksCount;
    [Tooltip("Int param selecting which skill plays. 0 = none.")]
    public string skillTrigger = "SkillNo";

    #endregion

    #region Inspector — Attacking

    [Header("Attack Settings")]
    [Tooltip("Set by the FOV routine: is the player inside attack range and angle?")]
    public bool EnemyIn;
    [Tooltip("Same, per skill that has its own range. Index matches " +
             "SkillsWithSeperateRange, not Skills.")]
    public List<bool> EnemyInSkill;
    [Tooltip("Declared for the Arachno Tank. Currently unused (Phase 10.3).")]
    public bool isRanged;
    public bool isAttacking;
    public int damage = 15;
    [Tooltip("For melee this should be the root motion travel distance +10-15%.")]
    public float attackRange = 1f;
    public float attackCooldown = 1.5f;
    [Tooltip("Cone the player must be inside for the enemy to consider attacking.")]
    public float attackAngle = 90f;
    [Tooltip("Disruption dealt by a melee hit, independent of damage.")]
    public float staggerForce = 15f;

    [Header("Melee Hit")]
    [Tooltip("Overlap radius around the weapon during the strike.")]
    public float hitRadius = 0.6f;
    [Tooltip("How long the strike stays live. Should cover the swing's contact " +
             "frames — too short and fast swings miss, too long and the enemy " +
             "hits you after you've dodged.")]
    public float hitWindowDuration = 0.15f;

    #endregion

    #region Inspector — Skills

    [Header("Skill Settings")]
    [Tooltip("Assigned from the Combat Config on spawn — do not rely on the " +
             "prefab's array at runtime.")]
    public SkillsScriptableObject[] Skills;
    [Tooltip("Parent for pooled skill VFX, and the origin point for thrown skills.")]
    public ParticleSystem SkillsVFX;

    #endregion

    #region Inspector — Watchdogs

    [Header("Watchdogs")]
    [Tooltip("Force-completes an attack if its animation event never fires.")]
    public float maxAttackDuration = 3f;
    [Tooltip("Force-completes a skill if it never reaches OnSkillComplete — an " +
             "early yield break, a retimed clip, a missing event. Set above the " +
             "longest skill this enemy has.")]
    public float maxSkillDuration = 10f;

    #endregion

    #region Inspector — References

    [Header("References")]
    [Tooltip("What melee overlaps and FOV checks look for.")]
    public LayerMask playerLayer;

    #endregion

    #region State

    private Enemy Enemy;
    private AI_Locomotion locomotion;
    private Animator animator;
    private FieldOfView FOV;
    private bool referencesCached;
    private HashSet<string> animatorParams;


    // Attack
    private float nextAttackTime;
    private float attackStartTime;
    private Coroutine hitWindowRoutine;

    // Skills
    public bool isUsingSkill = false;
    private SkillsScriptableObject skillInUse;
    private Coroutine skillRoutine;
    private float skillStartTime;

    // Per-skill cooldown stamps. Rebuilt from the CURRENT Skills array on
    // every spawn, because the Combat Config replaces that array at runtime.
    private Dictionary<SkillsScriptableObject, float> SkillsNextUseTimes;

    // Skills that poll their own range, and the matching "player in range" flags.
    private List<SkillsScriptableObject> SkillsWithSeperateRange;

    // Handles for the FOV polling coroutines, so they can be restarted without
    // a StopAllCoroutines that would also kill a running skill.
    private readonly List<Coroutine> fovRoutines = new();

    // Reused by the melee overlap. Sampling runs every frame of the hit window,
    // so a fresh array per frame per enemy is real garbage.
    private readonly Collider[] hitBuffer = new Collider[16];

    #endregion

    #region Lifecycle

    /// <summary>
    /// Idempotent reference caching. Called from Awake AND from every public
    /// entry point that can run early, because Unity does NOT guarantee Awake
    /// ordering across components — Enemy.OnEnable can run before this
    /// component's Awake, which is exactly how ResetCombatState used to throw
    /// a NullReferenceException during pool warm-up.
    /// </summary>
    private void CacheReferences()
    {
        if (referencesCached) return;
        referencesCached = true;

        locomotion = GetComponent<AI_Locomotion>();
        animator   = GetComponent<Animator>();
        FOV        = GetComponent<FieldOfView>();
        Enemy      = GetComponent<Enemy>();

        SkillsNextUseTimes      ??= new Dictionary<SkillsScriptableObject, float>();
        SkillsWithSeperateRange ??= new List<SkillsScriptableObject>();
        EnemyInSkill            ??= new List<bool>();

        animatorParams = new HashSet<string>();
        if (animator != null && animator.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in animator.parameters)
                animatorParams.Add(p.name);
    }

    private void Awake() => CacheReferences();

    /// <summary>
    /// Fallback for enemies placed directly in a scene rather than spawned.
    /// Spawned enemies get StartRoutine() from Setup_CombatConfig instead,
    /// which is the only point at which the Skills array is final.
    /// </summary>
    private void Start() => StartRoutine();

    private void Update()
    {
        // ── Attack watchdog ──────────────────────────────────────────────
        if (isAttacking && Time.time > attackStartTime + maxAttackDuration)
        {
            Debug.LogWarning($"{name}: attack never completed — forcing reset. " +
                             "Is the attack-complete animation event on the clip?");
            OnAttackComplete();
        }

        // ── Skill watchdog ───────────────────────────────────────────────
        // Without this, a skill whose Execute exits early (an exhausted pool,
        // a missing component) leaves isUsingSkill true and the enemy can
        // never attack or use a skill again.
        if (isUsingSkill && Time.time > skillStartTime + maxSkillDuration)
        {
            Debug.LogWarning($"{name}: skill '{skillInUse?.name}' never completed — " +
                             "forcing reset.");
            InterruptSkill(force: true);
        }
    }

    /// <summary>
    /// Full reset for pool reuse. Attack and skill state are normally cleared by
    /// animation events, which never fire if the enemy died mid-action.
    ///
    /// Called from BOTH Enemy.OnDisable (to stop coroutines while the object is
    /// still alive to stop them) and Enemy.ResetForSpawn (to assert the values).
    /// See Enemy_Spawn_Contract.md §3.
    /// </summary>
    public void ResetCombatState()
    {
        CacheReferences();

        StopHitWindow();

        if (skillRoutine != null)
        {
            StopCoroutine(skillRoutine);
            skillRoutine = null;
        }

        skillInUse     = null;
        isUsingSkill   = false;
        isAttacking    = false;
        nextAttackTime = 0f;

        EnemyIn = false;

        SetInt(attackTrigger, 0);
        SetInt(skillTrigger, 0);
    }

    #endregion

    #region Animator Guards

    private bool HasParam(string param) =>
        !string.IsNullOrEmpty(param) && animatorParams != null && animatorParams.Contains(param);

    private void SetInt(string param, int value)
    {
        if (HasParam(param)) animator.SetInteger(param, value);
    }

    #endregion

    #region FOV Polling

    /// <summary>
    /// Begins (or restarts) combat polling for this life. Must run AFTER the
    /// Combat Config has assigned Skills — it rebuilds all per-skill state from
    /// that array.
    ///
    /// ⚠ Deactivating a GameObject kills its coroutines permanently, so a
    /// pooled enemy has no FOV polling until this runs again. Call it from
    /// Setup_CombatConfig.
    /// </summary>
    public void StartRoutine()
    {
        CacheReferences();

        StopFOVRoutines();
        RebuildSkillState();

        // Attack range.
        fovRoutines.Add(StartCoroutine(FOV.FOVRoutine(
            .1f, true, transform, attackRange, playerLayer, attackAngle, FOV.obstructionMask,
            (result) => EnemyIn = result)));

        // One poll per skill that has its own range.
        for (int i = 0; i < SkillsWithSeperateRange.Count; i++)
        {
            int index = i;   // captured per iteration, not shared
            fovRoutines.Add(StartCoroutine(FOV.FOVRoutine(
                .2f, true, transform, SkillsWithSeperateRange[i].Range,
                playerLayer, attackAngle, FOV.obstructionMask,
                (result) => EnemyInSkill[index] = result)));
        }
    }

    /// <summary>
    /// Stops combat polling and releases whatever the enemy was doing.
    /// Interrupts FIRST, so a running skill gets its cleanup rather than being
    /// killed silently with its VFX still parented.
    /// </summary>
    public void StopRoutine()
    {
        InterruptAction();
        StopFOVRoutines();

        EnemyIn = false;
        for (int i = 0; i < EnemyInSkill.Count; i++) EnemyInSkill[i] = false;
    }

    private void StopFOVRoutines()
    {
        // Individually rather than StopAllCoroutines(): that sledgehammer would
        // also kill a running skill and the melee hit window, neither of which
        // belongs to FOV polling.
        foreach (Coroutine routine in fovRoutines)
            if (routine != null) StopCoroutine(routine);

        fovRoutines.Clear();
    }

    /// <summary>
    /// Rebuilds everything derived from the Skills array. Cooldowns start at
    /// one full cooldown from now, so an enemy can't open an encounter with a
    /// skill the instant it sees you.
    /// </summary>
    private void RebuildSkillState()
    {
        SkillsWithSeperateRange.Clear();
        EnemyInSkill.Clear();
        SkillsNextUseTimes.Clear();

        if (Skills == null) return;

        foreach (SkillsScriptableObject s in Skills)
        {
            if (s == null) continue;

            // Indexer, not Add: the same asset appearing twice in the array
            // would throw on a duplicate key.
            SkillsNextUseTimes[s] = Time.time + s.Cooldown;

            if (!s.hasSeperateFOV) continue;
            SkillsWithSeperateRange.Add(s);
            EnemyInSkill.Add(false);
        }
    }

    public SkillsScriptableObject GetSeparateSkill(int i)
    {
        if (i < 0 || i >= SkillsWithSeperateRange.Count) return null;
        return SkillsWithSeperateRange[i];
    }

    #endregion

    #region Attacking

    public bool CanAttack() => Time.time >= nextAttackTime && !isAttacking && !isUsingSkill;

    public void Attack()
    {
        if (!CanAttack()) return;

        isAttacking = true;
        attackStartTime = Time.time;

        // Root motion hands the transform to the animation — see
        // AI_Locomotion's Agent Sync region for why that needs coordinating.
        locomotion.SetRootMotionMode(useRootMotion);

        int id = Random.Range(1, attacksCount + 1);
        if (!HasParam(attackTrigger))
            Debug.LogWarning($"{name}: animator has no '{attackTrigger}' parameter — attack won't animate.");
        SetInt(attackTrigger, id);

        // Stamped on START, not on completion, so an interrupted attack still
        // respects the cooldown rather than letting the enemy swing instantly again.
        nextAttackTime = Time.time + attackCooldown;
    }

    /// <summary>Animation event at the end of the attack clip.</summary>
    public void OnAttackComplete()
    {
        isAttacking = false;
        locomotion.SetRootMotionMode(false);
        SetInt(attackTrigger, 0);
    }

    #endregion

    #region Melee Hit Detection

    /// <summary>
    /// Animation event at the swing's contact frame. Opens a short window that
    /// samples an overlap around the given weapon each frame, so the whole arc
    /// connects rather than a single instant. Each target is damaged once per swing.
    /// </summary>
    /// <param name="weaponIndex">Index into WeaponObjects, or -1 for all of them.</param>
    public void MeleeStrike(int weaponIndex)
    {
        StopHitWindow();
        hitWindowRoutine = StartCoroutine(HitWindow(weaponIndex));
    }

    private IEnumerator HitWindow(int weaponIndex)
    {
        // Tracked across the whole window so a target caught on several frames
        // takes damage once.
        HashSet<IDamagable> alreadyHit = new();

        for (float elapsed = 0f; elapsed < hitWindowDuration; elapsed += Time.deltaTime)
        {
            SampleHit(weaponIndex, alreadyHit);
            yield return null;
        }

        hitWindowRoutine = null;
    }

    /// <summary>
    /// Closes the window early. Called by InterruptAction, so staggering an
    /// enemy on its contact frame actually prevents the hit — otherwise the
    /// enemy freezes, plays the reaction, and damages you anyway, which reads
    /// as the stagger system not working.
    /// </summary>
    private void StopHitWindow()
    {
        if (hitWindowRoutine == null) return;
        StopCoroutine(hitWindowRoutine);
        hitWindowRoutine = null;
    }

    private void SampleHit(int weaponIndex, HashSet<IDamagable> alreadyHit)
    {
        if (WeaponObjects == null || WeaponObjects.Length == 0) return;

        if (weaponIndex == -1)
        {
            foreach (GameObject weapon in WeaponObjects)
                if (weapon != null) OverlapFrom(weapon.transform.position, alreadyHit);
        }
        else if (weaponIndex >= 0 && weaponIndex < WeaponObjects.Length)
        {
            GameObject weapon = WeaponObjects[weaponIndex];
            if (weapon != null) OverlapFrom(weapon.transform.position, alreadyHit);
        }
    }

    private void OverlapFrom(Vector3 origin, HashSet<IDamagable> alreadyHit)
    {
        int count = Physics.OverlapSphereNonAlloc(origin, hitRadius, hitBuffer, playerLayer);

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent checks the collider's own object first, so
            // this works whether IDamagable sits on the collider or above it.
            IDamagable target = hitBuffer[i].GetComponentInParent<IDamagable>();
            if (target == null || !alreadyHit.Add(target)) continue;

            // Source is the ENEMY's position, not the weapon's — "where did this
            // come from" is a question about the attacker, and the answer feeds
            // the target's hit direction and knockback.
            target.TakeDamage(damage, transform.position, staggerForce);
        }
    }

    #endregion

    #region Skills

    public bool CanUseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        if (Skill == null) return false;

        // TryGetValue rather than the indexer: Skills is reassigned by the
        // Combat Config at runtime, so a skill can exist that this dictionary
        // has never seen. Missing = treat as ready.
        float readyAt = SkillsNextUseTimes.TryGetValue(Skill, out float t) ? t : 0f;

        return Time.time >= readyAt
            && !isAttacking
            && !isUsingSkill
            && Skill.CanUseSkill(Enemy, Player);
    }

    public void UseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        CacheReferences();

        isUsingSkill = true;
        skillInUse = Skill;
        skillStartTime = Time.time;

        locomotion.SetRootMotionMode(useRootMotion);

        int index = Skills.IndexOfItem(Skill);
        if (index < 0)
            Debug.LogWarning($"{name}: skill '{Skill.name}' isn't in the Skills array — " +
                             "no animation will play.");
        if (!HasParam(skillTrigger))
            Debug.LogWarning($"{name}: animator has no '{skillTrigger}' parameter — attack won't animate.");
        SetInt(skillTrigger, index + 1);

        // Started HERE rather than inside the skill, so the handle lives on this
        // component and the skill can be stopped from outside.
        skillRoutine = StartCoroutine(Skill.Execute(Enemy, Player));
    }

    /// <summary>Animation event, or called by the skill itself when it finishes.</summary>
    public void OnSkillComplete()
    {
        SetInt(skillTrigger, 0);

        if (skillInUse != null)
            SkillsNextUseTimes[skillInUse] = Time.time + skillInUse.Cooldown;

        locomotion.SetRootMotionMode(false);

        skillInUse   = null;
        skillRoutine = null;
        isUsingSkill = false;
    }

    /// <summary>
    /// Cuts short whatever the enemy is doing. Called by stagger and by death.
    ///
    /// This is the method the whole stagger system exists to reach: it is what
    /// lets the player stop a reinforcement call by shooting the caller, and it
    /// is why a killed enemy drops immediately instead of finishing its swing.
    /// </summary>
    public void InterruptAction()
    {
        StopHitWindow();

        if (isAttacking)  OnAttackComplete();
        if (isUsingSkill) InterruptSkill();
    }

    /// <param name="force">
    /// Ignores Interruptible. Used only by the watchdog — a skill that has run
    /// past its ceiling is broken, and leaving it running forever is worse than
    /// cutting a committed action short.
    /// </param>
    private void InterruptSkill(bool force = false)
    {
        // A committed skill rides it out. This is what keeps the kamikaze from
        // being talked out of detonating.
        if (!force && skillInUse != null && !skillInUse.Interruptible) return;

        if (skillRoutine != null)
        {
            StopCoroutine(skillRoutine);
            skillRoutine = null;
        }

        // The skill releases its own resources — the interrupt code shouldn't
        // have to know what each one allocated. The default implementation
        // returns the enemy's registered pooled VFX, which covers most of them.
        skillInUse?.Interrupt(Enemy);

        OnSkillComplete();
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmosSelected()
    {
        if (WeaponObjects == null) return;

        Gizmos.color = Color.red;
        foreach (GameObject weapon in WeaponObjects)
            if (weapon != null)
                Gizmos.DrawWireSphere(weapon.transform.position, hitRadius);
    }

    #endregion
}