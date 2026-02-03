using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public class FieldOfView : MonoBehaviour
{
    public float interval;

    public float radius;
    [Range(0,360)]
    public int DetectionAngle;

    //[HideInInspector]
    public GameObject playerRef;
    //[HideInInspector]
    public LayerMask targetMask;
    //[HideInInspector]
    public LayerMask obstructionMask;

    public bool playerDetected;


    public event Func<Transform,float,LayerMask,float,bool> PerformFOVCheck;
    public event Func<int> ResetAngle;
    public event Func<int> MaxAngle;

    private Coroutine FOVcor;

    private void Start()
    {
        StartRoutine();
    }

    public IEnumerator FOVRoutine(float interval, bool isForCombat, Transform transform, float radius, LayerMask targetMask, float DetectionAngle,Action<bool> onComplete)
    {
        WaitForSeconds wait = new WaitForSeconds(interval);
        float Angle;

        while (true)
        {
            yield return wait;

            if(isForCombat)
                Angle = DetectionAngle;
            else
                Angle = this.DetectionAngle;
            
            bool result = PerformFOVCheck(transform, radius, targetMask, Angle);
            onComplete?.Invoke(result);
        }
    }

    public void StopRoutine()
    {
        StopCoroutine(FOVcor);
    }

    public void StartRoutine()
    {
        FOVcor = StartCoroutine(FOVRoutine(interval,false,transform,radius,targetMask,DetectionAngle,(result) => playerDetected = result));
    }

    public void ResetFOVAngle()
    {
        int? newAngle = ResetAngle?.Invoke();
        if (newAngle.HasValue)
            DetectionAngle = newAngle.Value;
    }

    public void MaxFOVAngle()
    {
        int? newAngle = MaxAngle?.Invoke();
        if (newAngle.HasValue)
            DetectionAngle = newAngle.Value;
    }

}
