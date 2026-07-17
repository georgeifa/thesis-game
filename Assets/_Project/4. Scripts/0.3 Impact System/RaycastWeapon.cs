using System.Collections;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public class RaycastWeapon : MonoBehaviour
{
    public bool isFiring;
    [SerializeField]
    private ParticleSystem muzzleFlash;
    [SerializeField]
    private Transform raycastOrigin;
    [SerializeField]
    private ParticleSystem hitEffect;
    [SerializeField]
    private TrailRenderer tracerEffect;
    [SerializeField]
    private float bulletSpeed;
    [SerializeField]
    private float nextShoot;
    private bool nShoot;

    [SerializeField]
    //private GameObject bullet;

    private Vector3 aimPosition;

    public void SetAimPosition(Vector3 aimPosition)
    {
        this.aimPosition = aimPosition;
    }

    void Update()
    {
        nShoot = nextShoot > 2;
        nextShoot += Time.deltaTime;
    }

    Ray ray;
    RaycastHit hitInfo;
    public void StartFiring()
    {
        if (nShoot)
        {
            isFiring = true;
            muzzleFlash.Emit(1);

            ray.origin = raycastOrigin.position;
            ray.direction = raycastOrigin.forward;

            ray.direction = new Vector3(ray.direction.x, 0, ray.direction.z);

            if (Physics.Raycast(ray, out hitInfo))
            {
                //GameObject bullet1 = Instantiate(this.bullet, raycastOrigin.position, quaternion.identity);
                //bullet1.transform.forward = raycastOrigin.forward;

                //StartCoroutine(SpawnTrail(bullet1, hitInfo));
            }

            nextShoot = 0;
        }
    }

    private IEnumerator SpawnTrail(GameObject bull, RaycastHit hit)
    {
        float time = 0;
        Vector3 startPosition = bull.transform.position;

        while (time < bulletSpeed)
        {
            bull.transform.position = Vector3.MoveTowards(startPosition, hit.point, time);
            time += Time.deltaTime;

            yield return null;
        }

        bull.transform.position = hit.point;

        hitEffect.transform.position = hitInfo.point;
        hitEffect.transform.forward = hitInfo.normal;
        hitEffect.Emit(1);
    }
    
    public void StopFiring()
    {
        isFiring = false;
    }
}
