using UnityEngine;

public class PlayerHealthManager : MonoBehaviour, IDamagable
{
    public int Health = 100;

    public event IDamagable.TakeDamageEvent OnTakeDamage;
    public event IDamagable.DeathEvent OnDeath;

    public int CurrentHealth { get => Health; private set => Health = value; }
    public int MaxHealth { get => Health; private set => Health = value; }


    public void TakeDamage(int Damage)
    {
        int damageTaken = Mathf.Clamp(Damage, 0, CurrentHealth);

        CurrentHealth -= damageTaken;

        if (damageTaken != 0)
        {
            OnTakeDamage?.Invoke(damageTaken);
        }
        
        if(CurrentHealth == 0 && damageTaken != 0)
        {
            OnDeath?.Invoke();
        }
    }

    public void GetHitDirection(Vector3 hitPoint)
    {
        throw new System.NotImplementedException();
    }
}
