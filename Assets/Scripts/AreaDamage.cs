using System.Collections;
using UnityEngine;

public class AreaDamage : MonoBehaviour
{
    public int Damage;
    public float TickRate;
    private IDamagable Damagable;

    void OnTriggerEnter(Collider other)
    {
        if(Damagable == null){
            IDamagable damagable = other.GetComponent<IDamagable>();
            if(damagable != null)
            {
                this.Damagable = damagable;
                StartCoroutine(DealDamage());
            }
        }
    }

    IEnumerator DealDamage()
    {
        
        WaitForSeconds Wait = new WaitForSeconds(TickRate);
        while(Damagable != null)
        {
            Damagable.TakeDamage(Damage);
            yield return Wait;
        }
    }

    void OnDisable()
    {
        Damagable = null;
    }

    void OnTriggerExit(Collider other)
    {
        if(Damagable == null){
            IDamagable damagable = other.GetComponent<IDamagable>();
            if(damagable != null)
            {
                Damagable = null;
            }
        }
    }
}
