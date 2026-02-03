using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserSight : MonoBehaviour
{

    private LineRenderer laserVFX;

    public float MaxRange = 3f;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        laserVFX = GetComponent<LineRenderer>();
    }

    // Update is called once per frame
    void Update()
    {
        RaycastHit hit;

        if(Physics.Raycast(transform.position,transform.forward, out hit, MaxRange))
        {
            laserVFX.SetPosition(1,new Vector3(0,0,hit.distance));

        }
        else
        {
            laserVFX.SetPosition(1,new Vector3(0,0,MaxRange));

        }
    }
}
