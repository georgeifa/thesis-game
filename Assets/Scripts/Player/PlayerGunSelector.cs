using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Animations.Rigging;

[DisallowMultipleComponent]
public class PlayerGunSelector : MonoBehaviour
{
    [SerializeField]
    private GunType Gun;
    [SerializeField]
    private Transform GunParent;
    [SerializeField]
    private Transform MagParent;
    [SerializeField]
    private List<GunScriptableObject> Guns;
    [SerializeField]
    private Transform Grip;
    [SerializeField]
    private Transform Trigger;
    [SerializeField]
    private Transform Magazine;

    //reference of the grip and trigger location on the gun
    private Transform gripRef;
    private Transform triggerRef;
    private Transform magRef;


    [Space]
    [Header("Runtime Filled")]
    public GunScriptableObject ActiveGun;

    void Start()
    {
        GunScriptableObject gun = Guns.Find(gun => gun.Type == Gun);

        if (gun == null)
        {
            Debug.LogError($"No GunScriptableObject found for GunType: {gun}");
            return;
        }

        ActiveGun = gun;

        gun.Spawn(GunParent,MagParent, this);
        CacheGunReferences();
    }

    private void Update()
    {
        if (ActiveGun != null)
        {
            if (gripRef != null)
                Grip.SetPositionAndRotation(gripRef.position, gripRef.rotation);
            
            if (triggerRef != null)
                Trigger.SetPositionAndRotation(triggerRef.position, triggerRef.rotation);

            if (magRef != null)
                Magazine.SetPositionAndRotation(magRef.position, magRef.rotation);
        }
    
    }
    

    private void CacheGunReferences()
    {
        if (ActiveGun != null)
        {
            PlayerWeapon weapon = ActiveGun.GetInGameModel().GetComponent<PlayerWeapon>();
            gripRef = weapon.Grip.transform;
            triggerRef = weapon.Trigger.transform;
            magRef = weapon.Magazine.transform;

        }
    }
}
