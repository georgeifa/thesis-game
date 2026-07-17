using UnityEngine;

[CreateAssetMenu(fileName = "Explosion Config", menuName = "Misc/Explosions/Explosion Config")]
public class ExplosionScriptableObject : ScriptableObject
{
    public PoolableObject explosionPrefab;

    public PoolableObject explosionDecal;
    public float VisibleDuration;
    public float FadeDuration;

    public void SetupExplosion(GameObject explosion)
    {
        SpawnExplosionDecal explosionDecal = explosion.GetComponent<SpawnExplosionDecal>();
        explosionDecal.explosionDecal = this.explosionDecal;
        explosionDecal.VisibleDuration = VisibleDuration;
        explosionDecal.FadeDuration = FadeDuration;
    }
}
