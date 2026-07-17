using System;
using UnityEngine;

public class AdjustableExplosion : MonoBehaviour
{
    [SerializeField]
    private GameObject[] particlesToAdjust;

    public void Adjust(float scale)
    {
        foreach(GameObject ps in particlesToAdjust)
        {
            ps.transform.localScale = Vector3.one * scale;
        }
    } 
}
