using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations.Rigging;

public enum EquipmentSlot
{
    None = -1,
    Primary,
    Secondary,
    Grenade,
    Stratagem // future
}
[DisallowMultipleComponent]
public class PlayerEquipmentManager : MonoBehaviour
{
    [Header("Loadout")]
    public GunScriptableObject Primary;
    public GunScriptableObject Secondary;

    [Header("Spawn")]
    [SerializeField] private Transform weaponParent;

    private Dictionary<EquipmentSlot, Gun> equippedGuns = new();
    private EquipmentSlot currentSlot = EquipmentSlot.None;

    public GameObject ActiveGun;

    public System.Action<Gun> OnGunChanged;



    [Space]
    [Header("IK References")]
    [SerializeField]
    private Transform Grip;
    [SerializeField]
    private Transform Trigger;

    //reference of the grip and trigger location on the gun
    private Transform gripRef;
    private Transform triggerRef;


    void Start()
    {
        EquipInitialLoadout();
        SwitchTo(EquipmentSlot.Primary);

        CacheGunReferences();
    }

    private void EquipInitialLoadout()
    {
        EquipGun(Primary, EquipmentSlot.Primary);
        EquipGun(Secondary, EquipmentSlot.Secondary);
    }

    private void EquipGun(GunScriptableObject data, EquipmentSlot slot)
    {
        GameObject obj = Instantiate(data.GunPrefab, weaponParent);

        Gun gun = obj.GetComponent<Gun>();
        gun.Initialize(data);

        obj.SetActive(false); // important

        equippedGuns[slot] = gun;
    }

    public void SwitchTo(EquipmentSlot slot)
    {
        if (currentSlot == slot) return;

            if (equippedGuns.TryGetValue(currentSlot, out Gun oldGun))
            {
                oldGun.gameObject.SetActive(false);
            }

            currentSlot = slot;

            if (equippedGuns.TryGetValue(slot, out Gun newGun))
            {
                newGun.gameObject.SetActive(true);
                ActiveGun = newGun.gameObject;

                OnGunChanged?.Invoke(newGun);
            }
    }

    private void Update()
    {
        if (ActiveGun != null)
        {
            if (gripRef != null)
                Grip.SetPositionAndRotation(gripRef.position, gripRef.rotation);
            
            if (triggerRef != null)
                Trigger.SetPositionAndRotation(triggerRef.position, triggerRef.rotation);
        }
    
    }
    

    private void CacheGunReferences()
    {
        if (ActiveGun != null)
        {
            gripRef = ActiveGun.GetComponent<Gun>().References.Grip;
            triggerRef = ActiveGun.GetComponent<Gun>().References.Trigger;
        }
    }
}
