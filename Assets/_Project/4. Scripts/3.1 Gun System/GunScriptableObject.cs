using UnityEngine;

[CreateAssetMenu(fileName = "Gun", menuName = "Guns/Gun", order = 0)]
public class GunScriptableObject : ScriptableObject
{
    public ImpactType ImpactType;
    public GunType Type;
    public string Name;
    public GameObject GunPrefab;
    public GameObject DroppedMagPrefab;
    public float droppedMagLifetime = 3f;
    public Vector3 SpawnPoint;
    public Vector3 SpawnRotation;

    // How this weapon should sit when parented into the hand socket during a
    // scripted action (reload, switch, etc.). Calibrated once per weapon type
    // because the mesh pivot is not at the grip. Local to HandWeaponSocket.
    [Header("In-Hand Attach Pose")]
    public Vector3 HoldPositionOffset;
    public Vector3 HoldRotationOffset; // Euler angles

    public DamageConfigScriptableObject DamageConfig;
    public AmmoConfigScriptableObject AmmoConfig;
    public ShootConfigurationScriptableObject ShootConfig;
    public TrailConfigurationScriptableObject TrailConfig;
    public AudioConfigurationScriptableObject AudioConfig;
}