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

    [Header("Body Sockets (slot-derived stow homes)")]
    [SerializeField] private Transform backSocket;     // Primary stows here
    [SerializeField] private Transform waistSocket;    // Secondary stows here

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
    public EquipmentSlot CurrentSlot => currentSlot;

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
        MakeActiveAndHold(EquipmentSlot.Primary);
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

        // Both guns visible. Park each on its body socket; SwitchTo() then
        // pulls the starting weapon into the hand.
        ParkOnBodySocket(EquipmentSlot.Primary);
        ParkOnBodySocket(EquipmentSlot.Secondary);
    }

    private void EquipGun(GunScriptableObject data, EquipmentSlot slot)
    {
        GameObject obj = Instantiate(data.GunPrefab, weaponParent);

        Gun gun = obj.GetComponent<Gun>();
        gun.Initialize(data);


        equippedGuns[slot] = gun;
    }

    // ─────────────────────────────────────────────
    //  Switching
    // ─────────────────────────────────────────────

/// <summary>
    /// Logical swap only: makes <paramref name="slot"/> the active weapon
    /// (caches refs, fires OnGunChanged) WITHOUT moving the gun's transform.
    /// During a switch the gun stays on its body socket until the draw blink
    /// reparents it to the hand. Startup uses MakeActiveAndHold() instead.
    /// </summary>
    public void SwitchTo(EquipmentSlot slot)
    {
        if (currentSlot == slot) return;

        currentSlot = slot;

        if (equippedGuns.TryGetValue(slot, out Gun newGun))
        {
            ActiveGun = newGun.gameObject;
            CacheGunReferences(newGun);

            OnGunChanged?.Invoke(newGun);
        }
    }

    /// <summary>
    /// Logical swap PLUS physically place the gun in the hand (holder).
    /// Used at startup, where there is no draw animation to place it.
    /// </summary>
    private void MakeActiveAndHold(EquipmentSlot slot)
    {
        SwitchTo(slot);
        if (ActiveGun != null)
            AttachGunToHolder(ActiveGun.GetComponent<Gun>());
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
    /// 
    /// <summary>The body socket a slot stows to (slot-derived).</summary>
    private Transform GetBodySocket(EquipmentSlot slot)
    {
        return slot == EquipmentSlot.Primary ? backSocket : waistSocket;
    }

    /// <summary>
    /// Parks a slot's gun on its body socket at local zero. Used at startup and
    /// as the destination of a stow. (Per-gun body fit offset comes later — for
    /// now it sits at local zero.)
    /// </summary>
    private void ParkOnBodySocket(EquipmentSlot slot)
    {
        if (!equippedGuns.TryGetValue(slot, out Gun gun)) return;

        Transform socket = GetBodySocket(slot);
        if (socket == null) return;

        gun.transform.SetParent(socket);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
    }

    /// <summary>Parks a gun under WeaponHolder at local zero (rig-controlled in-hand pose).</summary>
    private void AttachGunToHolder(Gun gun)
    {
        gun.transform.SetParent(weaponParent);
        gun.transform.localPosition = Vector3.zero;
        gun.transform.localRotation = Quaternion.identity;
    }

    /// <summary>Switch-sequence call (stow blink): parks the slot's gun on its body socket.</summary>
    public void StowToBodySocket(EquipmentSlot slot) => ParkOnBodySocket(slot);

    public void AttachWeaponToHand()
    {
        if (ActiveGun == null || handWeaponSocket == null) return;

        Gun gun = ActiveGun.GetComponent<Gun>();
        gun.ResetRecoil();                       // cancel any mid-spring recoil

        ActiveGun.transform.SetParent(handWeaponSocket);
        ActiveGun.transform.localPosition = gun.gunData.HoldPositionOffset;
        ActiveGun.transform.localRotation = Quaternion.Euler(gun.gunData.HoldRotationOffset);
    }

    /// <summary>
    /// Reparents the active gun back under WeaponHolder so the aim/idle rigs
    /// control it again. Because guns spawn at local zero inside WeaponHolder,
    /// resetting to zero returns it exactly to its rig-controlled position.
    /// </summary>
    public void ReturnWeaponToHolder()
    {
        if (ActiveGun == null) return;
        AttachGunToHolder(ActiveGun.GetComponent<Gun>());
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