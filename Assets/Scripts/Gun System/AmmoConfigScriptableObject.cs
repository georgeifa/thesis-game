using UnityEngine;

[CreateAssetMenu(fileName = "Ammo Config", menuName = "Guns/Ammo Configuration", order = 3)]
public class AmmoConfigScriptableObject : ScriptableObject
{
    public int MaxAmmo = 120;
    public int ClipSize = 30;

    public int CurrentAmmo = 120;
    public int CurrentClipAmmo = 30;

    public void Reload()
    {
        int reloadAmount = Mathf.Min(ClipSize, CurrentAmmo);
        CurrentClipAmmo = reloadAmount;
        CurrentAmmo -= reloadAmount;
    }

    public bool CanReload()
    {
        return CurrentClipAmmo < ClipSize && CurrentAmmo > 0;
    }

    public void InitializeAmmo()
    {
        CurrentAmmo = MaxAmmo;
        CurrentClipAmmo = ClipSize;
    }
}
