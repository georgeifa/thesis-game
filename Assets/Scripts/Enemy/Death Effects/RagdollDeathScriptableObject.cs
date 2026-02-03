using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Ragdoll Death Config", menuName = "Deaths/Ragdoll/Default Death Config")]
public class RagdollDeathScriptableObject : DeathScriptableObject
{
    private Rigidbody[] rigidbodies;
    private CharacterJoint[] CJ;
    private Collider[] col;
    private MeshCollider[] meshColliders;

    protected override void Initializations(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        base.Initializations(enemy,root,particleSystem);

        InitializeRagdoll(root);

        meshColliders = enemy.GetComponentsInChildren<MeshCollider>();

    }

    public override void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        if(root == null) Debug.LogError($"No root for ragdoll on {enemy.gameObject}.");
        Initializations(enemy,root,particleSystem);
        EnableDeathRagdoll();
        enemy.StartCoroutine(FreezeAndDissapear(enemy,root,particleSystem));
    }

    private void InitializeRagdoll(Transform root)
    {
        if(animator != null) animator.enabled = false;


        rigidbodies = root.GetComponentsInChildren<Rigidbody>();
        CJ = root.GetComponentsInChildren<CharacterJoint>();
        col = root.GetComponentsInChildren<Collider>();
    }

    private void EnableDeathRagdoll()
    {   
        if(meshColliders != null) foreach(MeshCollider col in meshColliders) col.enabled = false;

        foreach(Rigidbody r in rigidbodies)
        {
            r.linearVelocity = Vector3.zero;
            r.detectCollisions = true;
            r.useGravity = true;

        }
        foreach(CharacterJoint r in CJ)
        {
            r.enableCollision = true;

        }
        foreach(Collider r in col)
        {
            r.enabled = true;

        } 

        deathParticle.Play();
    }
    
    IEnumerator FreezeAndDissapear(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        yield return new WaitForSeconds(freezeAfter);

        foreach(Rigidbody r in rigidbodies)
        {
            r.isKinematic = true;
            r.constraints = RigidbodyConstraints.FreezeAll;
            r.useGravity = false;

        }
        foreach(CharacterJoint r in CJ)
        {
            r.enableCollision = false;

        }
        foreach(Collider r in col)
        {
            r.enabled = false;
        }

        deathParticle.Stop();
        
        base.Die(enemy,root,particleSystem);

    }

    public void DisableDeathRagdoll()
    {
        UnFreeze();
        legsAnimator.enabled = true;
        animator.enabled = true;
        foreach(MeshCollider col in meshColliders) col.enabled = true;
        AI_Locomotion.Reset();
        behaviorGraphAgent.enabled = true;


        foreach(Rigidbody r in rigidbodies)
        {
            r.detectCollisions = false;
            r.useGravity = false;
        }
        foreach(CharacterJoint r in CJ)
        {
            r.enableCollision = false;
        }
        foreach(Collider r in col)
        {
            r.enabled = false;
        }
    }

    void UnFreeze()
    {
        foreach(Rigidbody r in rigidbodies)
        {
            r.isKinematic = false;
            r.constraints = RigidbodyConstraints.None;
            r.useGravity = true;
        }
        foreach(CharacterJoint r in CJ)
        {
            r.enableCollision = true;

        }
        foreach(Collider r in col)
        {
            r.enabled = true;
        }

    }
}
