using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class SurfaceManager : MonoBehaviour
{
    private static SurfaceManager _instance;
    public static SurfaceManager Instance
    {
        get
        {
            return _instance;
        }
        private set
        {
            _instance = value;
        }
    }

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("More than one SurfaceManager active in the scene! Destroying latest one: " + name);
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    [SerializeField]
    private List<SurfaceType> Surfaces = new List<SurfaceType>();
    [SerializeField]
    private int DefaultPoolSizes = 10;
    [SerializeField]
    private Surface DefaultSurface;

    public void HandleImpact(GameObject HitObject, Vector3 HitPoint, Vector3 HitNormal, ImpactType Impact, int TriangleIndex)
    {
        SurfaceType surfaceType = Surfaces.Find(surface => surface.Tag == HitObject.tag);
        if (surfaceType != null)
            {
                foreach (Surface.SurfaceImpactTypeEffect typeEffect in surfaceType.Surface.ImpactTypeEffects)
                {
                    if (typeEffect.ImpactType == Impact)
                    {
                        PlayEffects(HitPoint, HitNormal, typeEffect.SurfaceEffect, 1);
                    }
                }
            }
            else
            {
                foreach (Surface.SurfaceImpactTypeEffect typeEffect in DefaultSurface.ImpactTypeEffects)
                {
                    if (typeEffect.ImpactType == Impact)
                    {
                        PlayEffects(HitPoint, HitNormal, typeEffect.SurfaceEffect, 1);
                    }
                }
            }
    }

    private void PlayEffects(Vector3 HitPoint, Vector3 HitNormal, SurfaceEffect SurfaceEffect, float SoundOffset)
    {
        foreach (SpawnObjectEffect spawnObjectEffect in SurfaceEffect.SpawnObjectEffects)
        {
            if (spawnObjectEffect.Probability > Random.value)
            {
                ObjectPool pool = ObjectPool.CreateInstance(spawnObjectEffect.Prefab.GetComponent<PoolableObject>(), DefaultPoolSizes);

                PoolableObject instance = pool.GetObject(HitPoint + HitNormal * 0.001f, Quaternion.LookRotation(HitNormal));

                instance.transform.forward = HitNormal;
                if (spawnObjectEffect.RandomizeRotation)
                {
                    Vector3 offset = new Vector3(
                        Random.Range(0, 180 * spawnObjectEffect.RandomizedRotationMultiplier.x),
                        Random.Range(0, 180 * spawnObjectEffect.RandomizedRotationMultiplier.y),
                        Random.Range(0, 180 * spawnObjectEffect.RandomizedRotationMultiplier.z)
                    );

                    instance.transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles + offset);
                }
            }
        }

        foreach(PlayAudioEffect playAudioEffect in SurfaceEffect.PlayAudioEffects)
        {
            AudioClip clip = playAudioEffect.AudioClips[Random.Range(0, playAudioEffect.AudioClips.Count)];
            ObjectPool pool = ObjectPool.CreateInstance(playAudioEffect.AudioSourcePrefab.GetComponent<PoolableObject>(), DefaultPoolSizes);
            AudioSource audioSource = pool.GetObject().GetComponent<AudioSource>();

            audioSource.transform.position = HitPoint;
            audioSource.PlayOneShot(clip, SoundOffset * Random.Range(playAudioEffect.VolumeRange.x, playAudioEffect.VolumeRange.y));
            StartCoroutine(DisableAudioSource(audioSource, clip.length));
        }
    }

    private IEnumerator DisableAudioSource(AudioSource AudioSource, float Time)
    {
        yield return new WaitForSeconds(Time);

        AudioSource.gameObject.SetActive(false);
    }
}
