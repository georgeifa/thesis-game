using MyBox;
using UnityEngine;
using System.Collections;

public enum SkillType
{
    Utility,
    Combat
}
public class SkillsScriptableObject : ScriptableObject
{
    public SkillType skillType;
    public float Cooldown = 10f;
    public int Damage = 5;
    [Tooltip("Stagger dealt by this skill.")]
    public float StaggerForce = 20f;
    [Tooltip("Can this be cut short by stagger? Killing an alerter mid-call is the point. A committed kamikaze is not.")]
    public bool Interruptible = true;
    public float Range = 3f;
    [Tooltip("All basic attacks have a priority of 5, lower number means higher probability to be selected over basic attacks")]
    [Range(0,5)]
    public int Priority = 1;
    public bool hasSeperateFOV;

    
    public virtual void UseSkill(Enemy enemy, GameObject player)
    {
        Debug.Log($"[SHIM] {name} went through the old UseSkill path");
        enemy.StartCoroutine(Execute(enemy, player));
    }

    /// <summary>
    /// The skill's body. RETURNED rather than started, so AI_Combat owns the
    /// handle and can stop it. Override this instead of UseSkill.
    /// </summary>
    public virtual IEnumerator Execute(Enemy enemy, GameObject player)
    {
        yield break;
    }

    /// <summary>
    /// Release anything Execute left behind — pooled VFX, parented objects.
    /// Called when the skill is cut short. Must be safe at ANY point in the
    /// coroutine, including before it ever started.
    /// </summary>
    public virtual void Interrupt(Enemy enemy)
    {
        enemy.ReleaseActiveSkillInstance();
    }

    public virtual bool CanUseSkill(Enemy enemy, GameObject player)
    {
        float distance = Vector3.Distance(enemy.transform.position,player.transform.position);
        return !enemy.IsUsingSkill
        && distance <= Helpers.RangeWithColliderOffset(enemy.gameObject,Range);
    }
}
