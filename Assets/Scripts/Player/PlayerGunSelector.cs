using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class PlayerGunSelector : MonoBehaviour
{
    public enum WeaponType
    {
        Primary,
        Secondary
    }

    [SerializeField]
    private GunType Gun;


    [Header("Primary Gun References")]
    public GunScriptableObject PrimaryGunSO;
    public GameObject PrimaryGun;
    [SerializeField]
    private Transform PrimaryGunParent;
    [SerializeField]
    private Transform PrimaryMagParent;
    [SerializeField]
    private Transform PrimaryGrip;
    [SerializeField]
    private Transform PrimaryTrigger;
    [SerializeField]
    private Transform PrimaryMagazine;

    [Header("Secondary Gun References")]
    public GunScriptableObject SecondaryGunSO;
    public GameObject SecondaryGun;
    [SerializeField]
    private Transform SecondaryGunParent;
    [SerializeField]
    private Transform SecondaryMagParent;
    [SerializeField]
    private Transform SecondaryGrip;
    [SerializeField]
    private Transform SecondaryTrigger;
    [SerializeField]
    private Transform SecondaryMagazine;

    //reference of the grip and trigger location on the gun
    private Transform gripRef;
    private Transform triggerRef;
    private Transform magRef;


    [Space]
    [Header("Runtime Filled")]
    public WeaponType ActiveWeapon;
    public GunScriptableObject ActiveGun;
    

    void Start()
    {
        SpawnGuns();

        ActiveGun = PrimaryGunSO;
        ActiveWeapon = WeaponType.Primary;
        CacheGunReferences();
    }

    private void SpawnGuns()
    {
        if (PrimaryGunSO == null)
        {
            Debug.LogWarning($"No GunScriptableObject found for Primary Gun.");
            return;
        }else
            PrimaryGun = PrimaryGunSO.Spawn(PrimaryGunParent,PrimaryMagParent,this);

        
        if (SecondaryGunSO == null)
        {
            Debug.LogWarning($"No GunScriptableObject found for Secondary Gun.");
            return;
        }else
            SecondaryGun = SecondaryGunSO.Spawn(SecondaryGunParent,SecondaryMagParent,this);

    }

    private void Update()
    {
        switch (ActiveWeapon)
            {
                case WeaponType.Primary:
                    if (gripRef != null)
                        PrimaryGrip.SetPositionAndRotation(gripRef.position, gripRef.rotation);
                    
                    if (triggerRef != null)
                        PrimaryTrigger.SetPositionAndRotation(triggerRef.position, triggerRef.rotation);

                    if (magRef != null)
                        PrimaryMagazine.SetPositionAndRotation(magRef.position, magRef.rotation);
                    break;

                case WeaponType.Secondary:
                    if (gripRef != null)
                        SecondaryGrip.SetPositionAndRotation(gripRef.position, gripRef.rotation);
                    
                    if (triggerRef != null)
                        SecondaryTrigger.SetPositionAndRotation(triggerRef.position, triggerRef.rotation);

                    if (magRef != null)
                        SecondaryMagazine.SetPositionAndRotation(magRef.position, magRef.rotation);
                    break;
            }
    
    }
    

    private void CacheGunReferences()
    {
        PlayerWeapon weapon;
        switch (ActiveWeapon)
            {
                case WeaponType.Primary:
                    weapon = PrimaryGun.GetComponent<PlayerWeapon>();
                    break;

                case WeaponType.Secondary:
                    weapon = SecondaryGun.GetComponent<PlayerWeapon>();
                    break;
                default:
                    weapon = PrimaryGun.GetComponent<PlayerWeapon>();
                    break;
            }
        gripRef = weapon.Grip.transform;
        triggerRef = weapon.Trigger.transform;
        magRef = weapon.Magazine.transform;
    }

    public void SwapOut(int GunID)
    {
        switch (GunID)
        {
            case 0:
                PrimaryGunParent.gameObject.SetActive(false);
                break;
            case 1:
                SecondaryGunParent.gameObject.SetActive(false);
                break;
        }
    }
}
