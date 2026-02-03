using Unity.Profiling;
using UnityEngine;

[RequireComponent(typeof(IDamagable))]
public class SpawnParticleSystemOnDeath : MonoBehaviour
{
    [SerializeField]
    private ParticleSystem DeathSystem;
    public IDamagable Damagable;

    void Awake()
    {
        Damagable = GetComponent<IDamagable>();
    }

    private void OnEnable()
    {
        Damagable.OnDeath += Damagable_OnDeath;
    }

    private void Damagable_OnDeath()
    {
        Instantiate(DeathSystem, transform.position, Quaternion.identity);
        gameObject.SetActive(false);

    }
}
