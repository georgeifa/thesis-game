using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Fills every garrison post at mission start and refills them slowly as the
// player thins them out.
//
// The delay is the whole design. Finite garrison lets a careful player clear
// the map and walk to the objective unopposed. Instant refill means killing a
// guard buys nothing. A long delay, at the guard's own post, means a stealth
// kill buys a WINDOW.
public class GarrisonManager : MonoBehaviour
{
    [SerializeField] EnemySpawnService spawner;
    [SerializeField] List<GarrisonPost> posts = new List<GarrisonPost>();

    [Tooltip("Seconds before a dead guard is replaced at his own post.")]
    [SerializeField] float respawnDelay = 90f;
    [Tooltip("Random extra on top, so replacements don't arrive in lockstep.")]
    [SerializeField] float respawnJitter = 20f;

    [Header("Debug")]
    [Tooltip("F1 refills every empty slot immediately.")]
    [SerializeField] bool debugRefillKey = true;

    class Slot
    {
        public GarrisonPost post;
        public Enemy enemy;
        public float refillAt = -1f;   // -1 = occupied, or not yet noticed empty
    }

    readonly List<Slot> slots = new List<Slot>();

    public int Total => slots.Count;

    public int Alive
    {
        get
        {
            int n = 0;
            foreach (Slot s in slots)
                if (IsAlive(s)) n++;
            return n;
        }
    }

    void Start()
    {
        if (spawner == null)
        {
            Debug.LogError($"{name}: no EnemySpawnService - the garrison will never spawn.");
            enabled = false;
            return;
        }

        foreach (GarrisonPost p in posts)
        {
            if (p == null || p.enemyType == null) continue;

            for (int i = 0; i < p.slots; i++)
            {
                Slot s = new Slot { post = p };
                slots.Add(s);
                Fill(s);
            }
        }
    }

    void Update()
    {
        if (debugRefillKey && Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            RefillAllNow();

        // TODO day 2: skip refilling while AlertState is Engaged. The Director
        // owns spawning from that point, and this would double up on the player.

        float now = Time.time;

        foreach (Slot s in slots)
        {
            if (IsAlive(s))
            {
                s.refillAt = -1f;
                continue;
            }

            // First frame we've noticed it's empty - start the clock.
            if (s.refillAt < 0f)
            {
                s.enemy = null;
                s.refillAt = now + respawnDelay + Random.Range(0f, respawnJitter);
                continue;
            }

            if (now >= s.refillAt) Fill(s);
        }
    }

    // Pooled enemies deactivate on death, so "active in the hierarchy" is the
    // whole liveness test. No OnDeath subscription to unhook.
    static bool IsAlive(Slot s) => s.enemy != null && s.enemy.gameObject.activeSelf;

    void Fill(Slot s)
    {
        s.enemy = spawner.SpawnAt(s.post.enemyType, s.post.PickPoint());
        s.refillAt = -1f;
    }

    void RefillAllNow()
    {
        foreach (Slot s in slots)
            if (!IsAlive(s)) Fill(s);
    }
}