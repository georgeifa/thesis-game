using System;
using System.Collections;
using FIMSpace.FProceduralAnimation;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.PlayerLoop;

public class DeathScriptableObject : ScriptableObject
{
    protected Animator animator;
    protected LegsAnimator legsAnimator;
    protected AI_Locomotion AI_Locomotion;
    protected BehaviorGraphAgent behaviorGraphAgent;

    protected ParticleSystem deathParticle;

    public float freezeAfter = 2f;
    public float SinkAfter = 2f;
    public float sinkSpeed = 2f;

    protected virtual void Initializations(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        InitializeComponents(enemy, particleSystem);
        DisableMovement();
    }

    public virtual void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        enemy.StartCoroutine(SinkUntilUnderGround(enemy));
    }

    private void InitializeComponents(Enemy enemy,ParticleSystem particleSystem)
    {
        animator = enemy.GetComponent<Animator>();
        legsAnimator = enemy.GetComponent<LegsAnimator>();
        AI_Locomotion = enemy.GetComponent<AI_Locomotion>();
        behaviorGraphAgent = enemy.GetComponent<BehaviorGraphAgent>();

        deathParticle = particleSystem;

    }

    private void DisableMovement()
    {
        if(legsAnimator != null) legsAnimator.enabled = false;
        if(AI_Locomotion != null) AI_Locomotion.Stop();
        if(behaviorGraphAgent != null) behaviorGraphAgent.enabled = false;
    }

    IEnumerator SinkUntilUnderGround(Enemy enemy)
    {
        yield return new WaitForSeconds(SinkAfter);

        float objectHeight = GetObjectHeight(enemy);
        float groundLevel = enemy.transform.position.y;
        float targetY = groundLevel - objectHeight;
        
        // Move downward until target reached
        while (enemy.transform.position.y > targetY)
        {
            // Move one frame
            enemy.transform.Translate(Vector3.down * sinkSpeed * Time.deltaTime);
            
            // PAUSE until next frame, then continue loop
            yield return null;
        }

        enemy.gameObject.SetActive(false);
    }

    private float GetObjectHeight(Enemy enemy)
    {
        NavMeshAgent navMesh = enemy.GetComponent<NavMeshAgent>();

        if (navMesh != null)
            return navMesh.height * 2f;
        
        return enemy.transform.localScale.y*3f;
    }

}
