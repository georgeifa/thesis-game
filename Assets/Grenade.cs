using System.Collections;
using System.Globalization;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class Grenade : MonoBehaviour
{

    public float ExplodeAfter;
    public int Damage;
    public float BlastRadius;
    public PoolableObject BlastVFX;
    public LayerMask PlayerLayer;

    void OnCollisionEnter(Collision collision)
    {
        StartCoroutine(Explode());   
    }

    void Start()
    {
        BlastVFX.GetComponent<AdjustableExplosion>().Adjust(BlastRadius);
    }

    IEnumerator Explode()
    {
        yield return new WaitForSeconds(ExplodeAfter);

        BlastVFX.transform.SetPositionAndRotation(transform.position,transform.rotation);
        BlastVFX.gameObject.SetActive(true);
        BlastVFX.GetComponent<SpawnExplosionDecal>().SpawnDecal();
        
        Collider[] colliders = new Collider[4];

        int numColliders = Physics.OverlapSphereNonAlloc(transform.position, BlastRadius,colliders,PlayerLayer);

        if (numColliders > 0)
        {
            colliders[0].GetComponent<IDamagable>().TakeDamage(Damage);
        }

        gameObject.SetActive(false);
    }
}
