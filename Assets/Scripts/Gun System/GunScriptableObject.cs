using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Pool;

[CreateAssetMenu(fileName = "Gun", menuName = "Guns/Gun", order = 0)]
public class GunScriptableObject : ScriptableObject
{
    public ImpactType ImpactType;
    public GunType Type;
    public string Name;
    public GameObject ModelPrefab;
    public Vector3 SpawnPoint;
    public Vector3 SpawnRotation;

    public DamageConfigScriptableObject DamageConfig;
    public AmmoConfigScriptableObject AmmoConfig;
    public ShootConfigurationScriptableObject ShootConfig;
    public TrailConfigurationScriptableObject TrailConfig;
    public AudioConfigurationScriptableObject AudioConfig;


    private MonoBehaviour ActiveMonoBehaviour;
    private GameObject Model;
    private AudioSource ShootingAudioSource;

    //fire rate parameters
    private float LastShootTime;
    private float InitialClickTime;
    private float StopShootingTime;
    private bool LastFrameWantedToShoot;

    //recoil parameters
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private bool applyRecoil;

    private ParticleSystem ShootSystem;
    private ObjectPool TrailPool;

    public void Spawn(Transform GunParent,Transform MagParent, MonoBehaviour ActiveMonoBehaviour)
    {
        this.ActiveMonoBehaviour = ActiveMonoBehaviour;
        LastShootTime = 0; // in editor this will not be properly reset, in build it's fine
        TrailPool = ObjectPool.CreateInstance(CreateTrail(),100);

        Model = Instantiate(ModelPrefab);
        Model.transform.SetParent(GunParent, false);
        Model.transform.localPosition = SpawnPoint;
        Model.transform.localRotation = Quaternion.Euler(SpawnRotation);


        Transform Magazine = Model.GetComponent<PlayerWeapon>().Magazine_Model.transform;
        MagParent.transform.parent.localPosition = Magazine.transform.position;
        Magazine.transform.SetParent(MagParent,this);

        targetPosition = Model.transform.localPosition;
        
        AmmoConfig.InitializeAmmo();

        ShootSystem = Model.GetComponentInChildren<ParticleSystem>();
        ShootingAudioSource = Model.GetComponentInChildren<AudioSource>();

    }

    public GameObject GetInGameModel()
    {
        return Model;
    }

    public void TryToShoot()
    {
        if (Time.time - LastShootTime - ShootConfig.FireRate > Time.deltaTime)
        {
            float lastDuration = Mathf.Clamp(
                0, StopShootingTime - InitialClickTime, ShootConfig.MaxSpreadTime
            );
            float lerpTime = (ShootConfig.SpreadRecoverySpeed - (Time.time - StopShootingTime)) / ShootConfig.SpreadRecoverySpeed;


            InitialClickTime = Time.time - Mathf.Lerp(0, lastDuration, Mathf.Clamp01(lerpTime));
        }


        if (Time.time > ShootConfig.FireRate + LastShootTime)
        {
            LastShootTime = Time.time;

            if (AmmoConfig.CurrentClipAmmo == 0)
            {
                AudioConfig.PlayOutOfAmmoClip(ShootingAudioSource);
                return;
            }
            
            ShootSystem.Play();
            AudioConfig.PlayShootingClip(ShootingAudioSource, AmmoConfig.CurrentClipAmmo == 1);

            Vector3 spreadAmount = ShootConfig.GetSpread(Time.time - InitialClickTime);

            TriggerRecoil();

            Vector3 shootDirection = ShootSystem.transform.forward + spreadAmount;

            AmmoConfig.CurrentClipAmmo--;

            if (Physics.Raycast(
                    ShootSystem.transform.position,
                    shootDirection,
                    out RaycastHit hit,
                    float.MaxValue,
                    ShootConfig.HitMask
            ))
            {
                ActiveMonoBehaviour.StartCoroutine(PlayTrail(ShootSystem.transform.position, hit.point, hit));
            }
            else
            {
                ActiveMonoBehaviour.StartCoroutine(PlayTrail(ShootSystem.transform.position, ShootSystem.transform.position + (shootDirection * TrailConfig.MissDistance), new RaycastHit()));
            }

        }
    }
    
    public void StartReloading()
    {
        AudioConfig.PlayReloadClip(ShootingAudioSource);
    }

    public bool CanReload()
    {
        return AmmoConfig.CanReload();
    }

    public void EndReload()
    {
        AmmoConfig.Reload();
    }

    private void ApplyRecoil()
    {
        if (applyRecoil)
        {
            // Spring physics for smooth recoil
            Model.transform.localPosition = Vector3.SmoothDamp(
                Model.transform.localPosition,
                targetPosition,
                ref currentVelocity,
                1f / ShootConfig.springSpeed
            );

            // Check if we've returned close enough to target
            if (Vector3.Distance(Model.transform.localPosition, targetPosition) < 0.001f)
            {
                applyRecoil = false;
                currentVelocity = Vector3.zero;
            }
        }
    }
    
    private void TriggerRecoil()
    {
        Vector3 recoilStrength = ShootConfig.GetRecoilStrength();
        // Apply immediate recoil force
        Vector3 recoilForce = Vector3.back * recoilStrength.z + Vector3.up * recoilStrength.y + Vector3.right * recoilStrength.x;
        Model.transform.localPosition += recoilForce;
        
        // Set spring target back to original
        applyRecoil = true;
        currentVelocity = Vector3.zero;
    }
    

    public void Tick(bool WantsToShoot)
    {
        ApplyRecoil();

        if (WantsToShoot)
        {
            LastFrameWantedToShoot = true;
            TryToShoot();

        }
        else if (!WantsToShoot && LastFrameWantedToShoot)
        {
            StopShootingTime = Time.time;
            LastFrameWantedToShoot = false;
        }
    }
    
    private IEnumerator PlayTrail(Vector3 startPoint, Vector3 endPoint, RaycastHit hit)
    {
        PoolableObject instance = TrailPool.GetObject();
        TrailRenderer trail = instance.gameObject.GetComponent<TrailRenderer>();
        trail.gameObject.SetActive(true);
        trail.transform.position = startPoint;
        yield return null; // avoid position carry-over from last frame if reused

        trail.emitting = true;

        float distance = Vector3.Distance(startPoint, endPoint);
        float remainingDistance = distance;
        while (remainingDistance > 0)
        {
            trail.transform.position = Vector3.Lerp(
                startPoint,
                endPoint,
                Mathf.Clamp01(1 - (remainingDistance / distance))
            );

            remainingDistance -= TrailConfig.SimulationSpeed * Time.deltaTime;

            yield return null;
        }

        trail.transform.position = endPoint;

        if(hit.collider != null)
        {
            SurfaceManager.Instance.HandleImpact(
                hit.transform.gameObject,
                endPoint,
                hit.normal,
                ImpactType,
                0
            );

            if(hit.collider.CompareTag("Damagable")){
                if(hit.collider.TryGetComponent(out IDamagable damagable))
                {
                    damagable.TakeDamage(DamageConfig.GetDamage(distance));
                    damagable.GetHitDirection(hit.point);
                }
                else
                {
                    try{
                        damagable = hit.collider.GetComponentInParent<IDamagable>();
                        damagable.TakeDamage(DamageConfig.GetDamage(distance));
                        damagable.GetHitDirection(hit.point);
                    }
                    catch(Exception e)
                    {
                        if(damagable == null)
                            Debug.LogError($"No IDamageable found on: {hit.collider}");
                        else
                            Debug.Log(e.Message);

                    }
                }
            }
        }

        yield return new WaitForSeconds(TrailConfig.Duration);
        yield return null;
        trail.emitting = false;
        trail.gameObject.SetActive(false);
    }

    private PoolableObject CreateTrail()
    {   
        GameObject instance = new GameObject("Bullet Trail");
        TrailRenderer trail = instance.AddComponent<TrailRenderer>();
        trail.colorGradient = TrailConfig.Color;
        trail.material = TrailConfig.Material;
        trail.widthCurve = TrailConfig.WidthCurve;
        trail.time = TrailConfig.Duration;
        trail.minVertexDistance = TrailConfig.MinVertexDistance;

        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        PoolableObject prefab = instance.AddComponent<PoolableObject>();

        return prefab;
    }
}
