using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class SpawnExplosionDecal : MonoBehaviour
{
    public PoolableObject explosionDecal;
    public float VisibleDuration;
    public float FadeDuration;


    private DecalProjector _decalProjector;

    public void SpawnDecal()
    {
                
        ObjectPool pool = ObjectPool.CreateInstance(explosionDecal,5);

        PoolableObject decal = pool.GetObject();
        decal.transform.position = transform.position;
        decal.transform.rotation = Quaternion.Euler(new Vector3(90f,0f,0f));

        _decalProjector = decal.GetComponent<DecalProjector>();

        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeOut()
    {
        yield return new WaitForSeconds(VisibleDuration);

        float elapsed = 0;
        float initialFactor = _decalProjector.fadeFactor;
       
        while(elapsed < 1)
        {
            _decalProjector.fadeFactor = Mathf.Lerp(initialFactor, 0, elapsed);
            elapsed += Time.deltaTime / FadeDuration;
            yield return null;
        }

        _decalProjector.gameObject.SetActive(false);
        _decalProjector.fadeFactor = initialFactor;
        gameObject.SetActive(false);
    }

}
