using MyBox;
using UnityEngine;

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
    public float Range = 3f;
    [Tooltip("All basic attacks have a priority of 5, lower number means higher probability to be selected over basic attacks")]
    [Range(0,5)]
    public int Priority = 1;
    public bool hasSeperateFOV;

    
    public virtual void UseSkill(Enemy enemy, GameObject player)
    {
    }

    public virtual bool CanUseSkill(Enemy enemy, GameObject player)
    {
        float distance = Vector3.Distance(enemy.transform.position,player.transform.position);
        return !enemy.IsUsingSkill
        && distance <= Helpers.RangeWithColliderOffset(enemy.gameObject,Range);
    }
}
