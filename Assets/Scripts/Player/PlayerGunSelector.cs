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
        Unarmed,
        Primary,
        Secondary
    }

    [SerializeField]
    private GunType Gun;

    [SerializeField]
    private Transform Grip;
    [SerializeField]
    private Transform Trigger;
    [SerializeField]
    private Transform Magazine;

    [Header("Primary Gun References")]
    public GunScriptableObject PrimaryGunSO;
    public GameObject PrimaryGun;
    [SerializeField]
    private Transform PrimaryGunParent;
    [SerializeField]
    private Transform PrimaryMagParent;


    [Header("Secondary Gun References")]
    public GunScriptableObject SecondaryGunSO;
    public GameObject SecondaryGun;
    [SerializeField]
    private Transform SecondaryGunParent;
    [SerializeField]
    private Transform SecondaryMagParent;    


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
        if (gripRef != null)
            Grip.SetPositionAndRotation(gripRef.position, gripRef.rotation);
                    
        if (triggerRef != null)
            Trigger.SetPositionAndRotation(triggerRef.position, triggerRef.rotation);

        if (magRef != null)
            Magazine.SetPositionAndRotation(magRef.position, magRef.rotation);    
    }

    public int GetWeaponID()
    {
        if(ActiveWeapon == WeaponType.Primary) return 1;
        if(ActiveWeapon == WeaponType.Secondary) return 2;

        return 0;
    }

    public void StartReloading()
    {
        ActiveGun.StartReloading();
        PrimaryGun.GetComponent<PlayerWeapon>().Magazine_Model.transform.SetParent(PrimaryMagParent);
    }

    public void EndReloading()
    {
        ActiveGun.EndReload();
        PrimaryGun.GetComponent<PlayerWeapon>().Magazine_Model.transform.SetParent(PrimaryGun.GetComponent<PlayerWeapon>().Parent_Model.transform);
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
            case 1:
                PrimaryGunParent.gameObject.SetActive(false);
                break;
            case 2:
                SecondaryGunParent.gameObject.SetActive(false);
                break;
        }
    }

    public void SwapIn(int GunID)
    {
        switch (GunID)
        {
            case 1:
                PrimaryGunParent.gameObject.SetActive(true);
                break;
            case 2:
                SecondaryGunParent.gameObject.SetActive(true);
                break;
        }
    }
}
