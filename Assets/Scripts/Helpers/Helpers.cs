using System.Runtime.InteropServices.WindowsRuntime;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public static class Helpers
{
    
    
    //Matrix to rotate the direction matrix according to what the camera sees (Isometric View)
    private static Matrix4x4 _isoMatrix = Matrix4x4.Rotate(Quaternion.Euler(0,45f,0));

    //Matrix to revert the direction matrix
    private static Matrix4x4 _normalMatrix = Matrix4x4.Rotate(Quaternion.Euler(0, -45f, 0));

    //Calculate position of cursor on screen to help with things like aiming
    private static (bool success, RaycastHit position, Ray ray) CalculateMousePosition(Camera mainCamera, InputAction mousePosition, LayerMask groundMask)
    {
        var ray = mainCamera.ScreenPointToRay(mousePosition.ReadValue<Vector2>());

        // Reuse hitInfo to avoid allocation
        if (Physics.Raycast(ray, out RaycastHit hitInfo, Mathf.Infinity, groundMask))
        {
            return (success: true, hitInfo, ray);
        }
        else
        {
            return (success: false, hitInfo, ray);
        }
    }

    public static (bool success, Vector3 isometricPosition) MousePositionToIsometric(Camera mainCamera, InputAction mousePosition, LayerMask groundMask,  float aimHeight)
    {
        var (success, position, ray) = CalculateMousePosition(mainCamera, mousePosition, groundMask);
        Vector3 isometricPosition = new();
        if (success)
        {
            //length of triangle
            Vector3 aimHeightPos = new(position.point.x, aimHeight, position.point.z);

            float length = Vector3.Distance(aimHeightPos, position.point);

            //lenth of hypotenuse
            var deg = 30;

            var rad = deg * Mathf.Deg2Rad;

            float hypote = length / Mathf.Sin(rad);

            float distanceFromCamera = position.distance;

            if (aimHeight > position.point.y)
            {
                isometricPosition = ray.GetPoint(distanceFromCamera - hypote);
            }
            else if (aimHeight < position.point.y)
            {
                isometricPosition = ray.GetPoint(distanceFromCamera + hypote);
            }
            else
            {
                isometricPosition = ray.GetPoint(distanceFromCamera);
            }
        }

        return (success, isometricPosition);
    } 
    
    //calculate the movement of a transform in local space
    public static Vector3 CalculateLocalMove(Vector2 input, Transform transform)
    {        
        // Reuse vectors to avoid allocations
        Vector3 move = input.y * Vector3.forward + input.x * Vector3.right;

        if (move.sqrMagnitude > 1) // Use sqrMagnitude for performance
            move.Normalize();

        Vector3 localMove = transform.InverseTransformDirection(move).ToIso();
        return localMove;
    }
    
    public static Vector3 ToIso(this Vector3 input) => _isoMatrix.MultiplyPoint3x4(input);

    public static Vector3 ToNormal(this Vector3 input) => _isoMatrix.MultiplyPoint3x4(input);
   

    public static float RangeWithColliderOffset(GameObject gameObject, float Radius)
    {
        float m_ColliderOffset = 0.0f;
        NavMeshAgent agent = gameObject.GetComponentInChildren<NavMeshAgent>();
        if (agent != null)
        {
            m_ColliderOffset += agent.radius;
        } 


        return Radius + m_ColliderOffset;
    }

    public static Vector3 CalculateArcVelocity(Vector3 start, Vector3 target, float minArcHeight, float maxArcHeight)
    {
        // Get horizontal direction and distance
        Vector3 horizontalDirection = target - start;
        horizontalDirection.y = 0;  // Remove height difference
        float horizontalDistance = horizontalDirection.magnitude;
        
        // Normalize for direction
        horizontalDirection.Normalize();
        
        // Calculate time based on distance (faster for longer throws)
        float flightTime = Mathf.Sqrt(horizontalDistance) * 0.5f;
        
        // Calculate vertical and horizontal velocities
        // Random arc height
        float arcHeight = Random.Range(minArcHeight, maxArcHeight);
        float verticalVelocity = (arcHeight + (target.y - start.y)) / flightTime + 0.5f * Mathf.Abs(Physics.gravity.y) * flightTime;
        float horizontalVelocity = horizontalDistance / flightTime;
        
        // Combine into final velocity
        Vector3 velocity = (horizontalDirection * horizontalVelocity) + (Vector3.up * verticalVelocity);
        
        return velocity;
    }

    public static Vector3 CalculateArcVelocity(Vector3 start, Vector3 target, float minFlightTime, float maxFlightTime, float maxDistance)
    {
        Vector3 gravity = Physics.gravity;
        Vector3 delta = target - start;

        // How far is this throw, as a fraction of the max range (0 = on top of you, 1 = max).
        Vector3 flat = new Vector3(delta.x, 0f, delta.z);
        float distanceFraction = Mathf.Clamp01(flat.magnitude / maxDistance);

        // Closer throws get a shorter flight time; far throws get the longer one.
        float flightTime = Mathf.Lerp(minFlightTime, maxFlightTime, distanceFraction);

        Vector3 velocity = (delta - 0.5f * gravity * flightTime * flightTime) / flightTime;
        return velocity;
    }
}
