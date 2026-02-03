using UnityEditor.EditorTools;
using UnityEngine;

[CreateAssetMenu(fileName = "Shoot Config", menuName = "Guns/Shoot Configuration", order = 2)]
public class ShootConfigurationScriptableObject : ScriptableObject
{
    public LayerMask HitMask;
    [Space]
    [Header("Spread Settings")]
    [Tooltip("The time it takes for the spread to return to minimum")]
    public float SpreadRecoverySpeed = 1f;
    [Tooltip("The time when the spread is at max effect")]
    public float MaxSpreadTime = 1f;
    [Tooltip("The maximum values the spread can reach from the center")]
    public Vector3 Spread = new Vector3(.1f, .1f, .1f);
    [Header("Spring Recoil Settings")]
    [Tooltip("Z should always be the largest to make more natural animation (knockback from the gun firing). X and Y are to make the animation more natural & random")]
    public Vector3 recoilStrenth = new Vector3(.1f, .1f, .1f);
    [Tooltip("Value for how fast gun returns to original position")]
    public float springSpeed = 8f;
    public float damping = 0.7f;
    [Space]
    public float FireRate = .25f;

    public Vector3 GetSpread(float ShootTime = 0)
    {
        Vector3 spread = Vector3.Lerp(
            Vector3.zero,
            new Vector3(
                    Random.Range(
                        -Spread.x,
                        Spread.x),
                    Random.Range(
                        -Spread.y,
                        Spread.y),
                    Random.Range(
                        -Spread.z,
                        Spread.z)
                ),
            Mathf.Clamp01(ShootTime / MaxSpreadTime)
                );

        return spread;
    }

    //Adds a little randomness to the recoil animation to make it more natural
    public Vector3 GetRecoilStrength()
    {
        return new Vector3(
                    Random.Range(
                        -recoilStrenth.x,
                        recoilStrenth.x),
                    Random.Range(
                        0,
                        recoilStrenth.y),
                    Random.Range(
                        recoilStrenth.z / 2,
                        recoilStrenth.z)
                );
    }
}
