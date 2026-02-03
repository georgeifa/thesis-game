using Unity.VisualScripting;
using UnityEngine;

public class EnemyHealth : MonoBehaviour, IDamagable
{
    [SerializeField]
    private int _MaxHealth = 100;
    [SerializeField]
    private int _Health; //just to see in the inspector


    public int CurrentHealth { get => _Health; private set => _Health = value; }
    public int MaxHealth { get => _MaxHealth; private set => _MaxHealth = value; }

    public event IDamagable.TakeDamageEvent OnTakeDamage;
    public event IDamagable.DeathEvent OnDeath;

    void OnEnable()
    {
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(int Damage){
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
