using System.Collections;
using UnityEngine;

public class EnemyWeapon : MonoBehaviour
{
    [HideInInspector]
    public int Damage;
    [HideInInspector]
    public LayerMask playerLayer;
    [HideInInspector]

    private int playerLayerValue;
    private bool HitAlready;

    void Start()
    {
        playerLayerValue = (int) Mathf.Log(playerLayer, 2);
    }

    void OnTriggerEnter(Collider other)
    {
        if(!HitAlready)
            if(other.CompareTag("Damagable") && playerLayerValue == other.gameObject.layer){
                other.GetComponent<IDamagable>().TakeDamage(Damage);
                HitAlready = true;

            }

    }

    private IEnumerator ResetHit()
    {
        yield return new WaitForSeconds(.1f);
        HitAlready = false;
    }
}
