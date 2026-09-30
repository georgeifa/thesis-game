using UnityEngine;

public  interface IDamagable
{
    public int CurrentHealth { get; }
    public int MaxHealth { get; }

    public delegate void TakeDamageEvent(int Damage);
    public event TakeDamageEvent OnTakeDamage;

    public delegate void DeathEvent();
    public event DeathEvent OnDeath;
    public void TakeDamage(int damage, Vector3 sourcePosition, float staggerForce = 0f);
}
