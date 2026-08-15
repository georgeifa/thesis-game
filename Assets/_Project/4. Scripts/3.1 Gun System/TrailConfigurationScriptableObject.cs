using System;
using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Trail Config", menuName = "Guns/Trail Configuration", order = 4)]
public class TrailConfigurationScriptableObject : ScriptableObject
{
    public Material Material;
    public AnimationCurve WidthCurve;
    public float Duration = .5f;
    public float MinVertexDistance = .1f;
    public Gradient Color;

    public float MissDistance = 100f;
    public float SimulationSpeed = 100f;

    public IEnumerator PlayTrail(GunScriptableObject GunConfig, ObjectPool TrailPool, Vector3 startPoint, Vector3 endPoint, RaycastHit hit, ImpactType ImpactType)
    {
        TrailConfigurationScriptableObject TrailConfig = GunConfig.TrailConfig;
        DamageConfigScriptableObject DamageConfig = GunConfig.DamageConfig;

        PoolableObject instance = TrailPool.GetObject();
        TrailRenderer trail = instance.gameObject.GetComponent<TrailRenderer>();
        trail.gameObject.SetActive(true);
        trail.transform.position = startPoint;
        yield return null; // avoid position carry-over from last frame if reused

        trail.emitting = true;

        float distance = Vector3.Distance(startPoint, endPoint);
        float remainingDistance = distance;
        while (remainingDistance > 0)
        {
            trail.transform.position = Vector3.Lerp(
                startPoint,
                endPoint,
                Mathf.Clamp01(1 - (remainingDistance / distance))
            );

            remainingDistance -= TrailConfig.SimulationSpeed * Time.deltaTime;

            yield return null;
        }

        trail.transform.position = endPoint;

        if (hit.collider != null)
        {
            SurfaceManager.Instance.HandleImpact(
                hit.transform.gameObject, endPoint, hit.normal, ImpactType, 0);

            // The interface is the test — no tag needed. GetComponentInParent walks up
            // so hitboxes can live on child meshes.
            IDamagable damagable = hit.collider.GetComponentInParent<IDamagable>();
            if (damagable != null)
            {
                damagable.GetHitDirection(hit.point);
                damagable.TakeDamage(DamageConfig.GetDamage(distance));
            }
        }

        yield return new WaitForSeconds(TrailConfig.Duration);
        yield return null;
        trail.emitting = false;
        trail.gameObject.SetActive(false);
    }

    public PoolableObject CreateTrail(GunScriptableObject GunConfig)
    {   
        TrailConfigurationScriptableObject TrailConfig = GunConfig.TrailConfig;


        GameObject instance = new("Bullet Trail");
        TrailRenderer trail = instance.AddComponent<TrailRenderer>();
        trail.colorGradient = TrailConfig.Color;
        trail.material = TrailConfig.Material;
        trail.widthCurve = TrailConfig.WidthCurve;
        trail.time = TrailConfig.Duration;
        trail.minVertexDistance = TrailConfig.MinVertexDistance;

        trail.emitting = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        PoolableObject prefab = instance.AddComponent<PoolableObject>();

        return prefab;
    }
}
