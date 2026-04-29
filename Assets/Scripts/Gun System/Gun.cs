using UnityEngine;

public enum GunState
{
    Idle,
    Shooting,
    Reloading,
    Equipping
}

public class Gun : MonoBehaviour
{
    public GunScriptableObject gunData;

    public int CurrentAmmo;
    public int CurrentClipAmmo;

    private GunState currentState = GunState.Idle;

    private AudioSource ShootingAudioSource;
    private ParticleSystem ShootSystem;

    private ObjectPool TrailPool;

    //recoil parameters
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private bool applyRecoil;

    //fire rate parameters
    private float LastShootTime;
    private float InitialClickTime;
    private bool wasShootingLastFrame;


#region Ammo Tracking

    public void InitializeAmmo()
    {
        CurrentAmmo = gunData.AmmoConfig.MaxAmmo;
        CurrentClipAmmo = gunData.AmmoConfig.ClipSize;
    }

#endregion

#region Reloading

    public void StartReloading()
    {
        gunData.AudioConfig.PlayReloadClip(ShootingAudioSource);
    }

    public void Reload()
    {
        int reloadAmount = Mathf.Min(gunData.AmmoConfig.ClipSize, CurrentAmmo);
        CurrentClipAmmo = reloadAmount;
        CurrentAmmo -= reloadAmount;
    }

    public bool CanReload()
    {
        return CurrentClipAmmo < gunData.AmmoConfig.ClipSize && CurrentAmmo > 0;
    }

#endregion

#region Shooting Logic

    public void Tick(bool wantsToShoot)
    {
        ApplyRecoil();

        switch (currentState)
        {
            case GunState.Idle:
                if (wantsToShoot)
                    TryStartShooting();
                break;

            case GunState.Shooting:
                if (wantsToShoot)
                    HandleShooting();
                else
                    StopShooting();
                break;

            case GunState.Reloading:
                // Do nothing (locked)
                break;
        }

        wasShootingLastFrame = wantsToShoot;
    }

    private void TryStartShooting()
    {
        if (!CanShoot())
            return;

        currentState = GunState.Shooting;

        InitialClickTime = Time.time;

        HandleShooting();
    }

    private bool CanShoot()
    {
        if (CurrentClipAmmo == 0)
        {
            gunData.AudioConfig.PlayOutOfAmmoClip(ShootingAudioSource);
            return false; // empty mag
        }
        return true;
    }

    private void HandleShooting()
    {
        if (!CanShoot())
        {
            StopShooting();
            return;
        }

        if (Time.time >= LastShootTime + gunData.ShootConfig.GetFireDelay())
        {
            LastShootTime = Time.time;

            switch (gunData.ShootConfig.FireMode)
            {
                case FireMode.FullAuto:
                    FireSingle();
                    break;

                case FireMode.SemiAuto:
                    if(!wasShootingLastFrame)
                        FireSingle();
                    break;

                case FireMode.Shotgun:
                    if(!wasShootingLastFrame)
                        FireShotgun();
                    break;
            }
        }
    }

    private void FireSingle()
    {
        ShootSystem.Play();
        gunData.AudioConfig.PlayShootingClip(ShootingAudioSource, CurrentClipAmmo == 1);

        Vector3 spreadAmount = gunData.ShootConfig.GetSpread(Time.time - InitialClickTime);

        TriggerRecoil();

        Vector3 shootDirection = ShootSystem.transform.forward + spreadAmount;
        //Vector3 shootDirection = ShootSystem.transform.forward;


        CurrentClipAmmo--;

        if (Physics.Raycast(
            ShootSystem.transform.position,
            shootDirection,
            out RaycastHit hit,
            float.MaxValue,
            gunData.ShootConfig.HitMask
        ))
        {
            StartCoroutine(gunData.TrailConfig.PlayTrail(gunData, TrailPool, ShootSystem.transform.position, hit.point, hit, gunData.ImpactType));
        }
        else
        {
            StartCoroutine(gunData.TrailConfig.PlayTrail(gunData, TrailPool, ShootSystem.transform.position, ShootSystem.transform.position + (shootDirection * gunData.TrailConfig.MissDistance), new RaycastHit(), gunData.ImpactType));
        }
    }

    private void FireShotgun()
    {
        ShootSystem.Play();
        gunData.AudioConfig.PlayShootingClip(ShootingAudioSource, CurrentClipAmmo == 1);


        TriggerRecoil();

        int pellets = gunData.ShootConfig.PelletCount;
        CurrentClipAmmo--;

        for (int i = 0; i < pellets; i++)
        {
            Vector3 spreadAmount = gunData.ShootConfig.GetSpread(gunData.ShootConfig.MaxSpreadTime);

            Vector3 shootDirection = ShootSystem.transform.forward + spreadAmount;
            //Vector3 shootDirection = ShootSystem.transform.forward;



            if (Physics.Raycast(
                ShootSystem.transform.position,
                shootDirection,
                out RaycastHit hit,
                float.MaxValue,
                gunData.ShootConfig.HitMask
            ))
            {
                StartCoroutine(gunData.TrailConfig.PlayTrail(gunData, TrailPool, ShootSystem.transform.position, hit.point, hit, gunData.ImpactType));
            }
            else
            {
                StartCoroutine(gunData.TrailConfig.PlayTrail(gunData, TrailPool, ShootSystem.transform.position, ShootSystem.transform.position + (shootDirection * gunData.TrailConfig.MissDistance), new RaycastHit(), gunData.ImpactType));
            }
        }
    }
    
    private void StopShooting()
    {
        currentState = GunState.Idle;
    }
    
#endregion

#region Recoil Logic

    private void ApplyRecoil()
    {
        if (applyRecoil)
        {
            // Spring physics for smooth recoil
            transform.localPosition = Vector3.SmoothDamp(
                transform.localPosition,
                targetPosition,
                ref currentVelocity,
                1f / gunData.ShootConfig.springSpeed
            );

            // Check if we've returned close enough to target
            if (Vector3.Distance(transform.localPosition, targetPosition) < 0.001f)
            {
                applyRecoil = false;
                currentVelocity = Vector3.zero;
            }
        }
    }
    
    private void TriggerRecoil()
    {
        Vector3 recoilStrength = gunData.ShootConfig.GetRecoilStrength();
        // Apply immediate recoil force
        Vector3 recoilForce = Vector3.back * recoilStrength.z + Vector3.up * recoilStrength.y + Vector3.right * recoilStrength.x;
        transform.localPosition += recoilForce;
        
        // Set spring target back to original
        applyRecoil = true;
        currentVelocity = Vector3.zero;
    }

#endregion
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        LastShootTime = 0; // in editor this will not be properly reset, in build it's fine

        targetPosition = transform.localPosition;
    }

    public void Initialize(GunScriptableObject data)
    {
        gunData = data;

        TrailPool = ObjectPool.CreateInstance(
            gunData.TrailConfig.CreateTrail(gunData), 100
        );

        ShootSystem = GetComponentInChildren<ParticleSystem>();
        ShootingAudioSource = GetComponentInChildren<AudioSource>();

        targetPosition = transform.localPosition;

        InitializeAmmo();

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
