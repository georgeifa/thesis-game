using System.Collections.Generic;
using UnityEngine;

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
    // ─────────────────────────────────────────────
    //  Inspector References
    // ─────────────────────────────────────────────

    [Header("Loadout")]
    public GunScriptableObject Primary;
    public GunScriptableObject Secondary;

    [Header("Spawn")]
    [SerializeField] private Transform weaponParent;

    [Header("IK Targets (persistent proxies on Player)")]
    [SerializeField] private Transform Grip;       // follows ref_grip on active gun
    [SerializeField] private Transform Trigger;    // follows ref_trigger on active gun

    [Header("Scripted Action Attach Point")]
    [SerializeField] private Transform handWeaponSocket; // empty child of the right hand bone

    // ─────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────

    private Dictionary<EquipmentSlot, Gun> equippedGuns = new();
    private EquipmentSlot currentSlot = EquipmentSlot.None;

    public GameObject ActiveGun { get; private set; }

    public System.Action<Gun> OnGunChanged;

    // Cached references from the active gun
    private Transform gripRef;
    private Transform triggerRef;

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Start()
    {
        EquipInitialLoadout();
        SwitchTo(EquipmentSlot.Primary);
    }

    private void Update()
    {
        // Keep IK proxy transforms snapped to the active gun's reference points
        if (gripRef    != null) Grip.SetPositionAndRotation(gripRef.position, gripRef.rotation);
        if (triggerRef != null) Trigger.SetPositionAndRotation(triggerRef.position, triggerRef.rotation);
    }

    // ─────────────────────────────────────────────
    //  Loadout
    // ─────────────────────────────────────────────

    private void EquipInitialLoadout()
    {
        EquipGun(Primary,   EquipmentSlot.Primary);
        EquipGun(Secondary, EquipmentSlot.Secondary);
    }

    private void EquipGun(GunScriptableObject data, EquipmentSlot slot)
    {
        GameObject obj = Instantiate(data.GunPrefab, weaponParent);

        Gun gun = obj.GetComponent<Gun>();
        gun.Initialize(data);

        obj.SetActive(false);

        equippedGuns[slot] = gun;
    }

    // ─────────────────────────────────────────────
    //  Switching
    // ─────────────────────────────────────────────

    public void SwitchTo(EquipmentSlot slot)
    {
        if (currentSlot == slot) return;

        // Deactivate old gun
        if (equippedGuns.TryGetValue(currentSlot, out Gun oldGun))
            oldGun.gameObject.SetActive(false);

        currentSlot = slot;

        // Activate new gun
        if (equippedGuns.TryGetValue(slot, out Gun newGun))
        {
            newGun.gameObject.SetActive(true);
            ActiveGun = newGun.gameObject;

            CacheGunReferences(newGun);

            OnGunChanged?.Invoke(newGun);
        }
    }

    public EquipmentSlot GetNextWeaponSlot()
    {
        return currentSlot == EquipmentSlot.Primary
            ? EquipmentSlot.Secondary
            : EquipmentSlot.Primary;
    }

    public bool CanSwitchTo(EquipmentSlot slot)
    {
        return slot != currentSlot && equippedGuns.ContainsKey(slot);
    }

    // ─────────────────────────────────────────────
    //  Scripted Action Attach / Return
    // ─────────────────────────────────────────────

    /// <summary>
    /// Reparents the active gun from WeaponHolder into the right hand socket.
    /// While attached here the rigs no longer touch the gun — the animation
    /// drives the hand, and the gun rides along because it is parented to it.
    /// Used by reload now, and reusable for weapon switch / grenade later.
    ///
    /// Each weapon's mesh pivot differs, so the gun carries its own in-hand
    /// pose offset (HoldPositionOffset / HoldRotationOffset on its GunData).
    /// </summary>
    public void AttachWeaponToHand()
    {
        if (ActiveGun == null || handWeaponSocket == null) return;

        ActiveGun.transform.SetParent(handWeaponSocket);

        GunScriptableObject data = ActiveGun.GetComponent<Gun>().gunData;
        ActiveGun.transform.localPosition = data.HoldPositionOffset;
        ActiveGun.transform.localRotation = Quaternion.Euler(data.HoldRotationOffset);
    }

    /// <summary>
    /// Reparents the active gun back under WeaponHolder so the aim/idle rigs
    /// control it again. Because guns spawn at local zero inside WeaponHolder,
    /// resetting to zero returns it exactly to its rig-controlled position.
    /// </summary>
    public void ReturnWeaponToHolder()
    {
        if (ActiveGun == null) return;

        ActiveGun.transform.SetParent(weaponParent);
        ActiveGun.transform.localPosition = Vector3.zero;
        ActiveGun.transform.localRotation = Quaternion.identity;
    }

    // ─────────────────────────────────────────────
    //  Gun Reference Caching
    // ─────────────────────────────────────────────

    private void CacheGunReferences(Gun gun)
    {
        gripRef    = gun.References.Grip;
        triggerRef = gun.References.Trigger;
    }
}