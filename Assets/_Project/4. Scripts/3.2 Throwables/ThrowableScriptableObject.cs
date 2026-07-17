using UnityEngine;

[CreateAssetMenu(fileName = "Grenade", menuName = "Throwables/Grenade", order = 0)]
public class ThrowableScriptableObject : ScriptableObject
{
    public string Name;

    [Header("Prefab & VFX")]
    public PoolableObject GrenadePrefab;
    public GameObject ThrowableModel;
    public ExplosionScriptableObject ExplosionVFX;

    [Header("Explosion")]
    public int   Damage       = 120;
    public float BlastRadius   = 4f;
    public float ExplodeAfter  = 2.5f;
    public LayerMask TargetLayer;

    [Header("Throw Arc")]
    public float MinArcHeight = 2.5f;
    public float MaxArcHeight = 3.5f;
}