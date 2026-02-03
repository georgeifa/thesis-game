using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AI_Combat))]
public class CombatHandlerEditor : Editor
{
    private void OnSceneGUI()
    {
        //EnemyCombatHandler combatHandler = (EnemyCombatHandler)target;
        AI_Combat combatHandler = (AI_Combat)target;

        Handles.color = Color.red;
        

        float range = Helpers.RangeWithColliderOffset(combatHandler.gameObject, combatHandler.attackRange);

        Vector3 viewAngle01 = DirectionFromAngle(combatHandler.transform.eulerAngles.y, -combatHandler.attackAngle / 2);
        Vector3 viewAngle02 = DirectionFromAngle(combatHandler.transform.eulerAngles.y, combatHandler.attackAngle / 2);

        Handles.color = Color.red;

        Handles.DrawLine(combatHandler.transform.position, combatHandler.transform.position + viewAngle01 * range);
        Handles.DrawLine(combatHandler.transform.position, combatHandler.transform.position + viewAngle02 * range);

        Handles.DrawWireArc(combatHandler.transform.position, Vector3.up, Vector3.forward, 360, range);
    }

    private Vector3 DirectionFromAngle(float eulerY, float angleInDegrees)
    {
        angleInDegrees += eulerY;

        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}