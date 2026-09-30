using UnityEngine;

public enum StaggerTier
{
    None,
    Flinch,     // small knockback, no animation — reads as disruption
    Stagger     // reaction animation, interrupts the current action
}
