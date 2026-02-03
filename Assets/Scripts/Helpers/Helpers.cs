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
}
