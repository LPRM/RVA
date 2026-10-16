using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVA
{
    /// <summary>
    /// The ball in the hidden physics maze. It does not use Unity's global gravity: MazeGravity sets
    /// <see cref="Gravity"/> every frame because its direction follows the tilt of the marker.
    /// Holes work by switching off the collision between the ball and the floor, so the floor can be a
    /// single box with no seams for the ball to catch on.
    /// Created by MazeBuilder; you do not add it by hand.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
    public class MazeBall : MonoBehaviour
    {
        [Tooltip("How long the ball falls into a hole before it respawns (seconds).")]
        [SerializeField, Min(0.1f)] float fallDuration = 0.8f;
        [Tooltip("Respawn at SpawnPoint automatically after falling into a hole.")]
        [SerializeField] bool autoRespawn = true;
        [Tooltip("Write hole, exit and checkpoint events to the Console.")]
        [SerializeField] bool logEvents = true;

        Transform root;
        Collider floor;
        float holeCaptureRadius = 0.28f;
        float fallTimer;
        MazeTrigger currentHole;
        Vector3 savedVelocity;
        Vector3 savedAngularVelocity;
        readonly List<MazeTrigger> overlappingHoles = new List<MazeTrigger>();

        public Rigidbody Body { get; private set; }
        public SphereCollider Sphere { get; private set; }

        /// <summary>Gravity acceleration in world space. Set every frame by MazeGravity.</summary>
        public Vector3 Gravity { get; set; } = Vector3.down * 9.81f;

        public bool IsFrozen { get; private set; }
        public bool IsFalling { get; private set; }

        /// <summary>Where the ball respawns after a fall, in the physics maze's local space.</summary>
        public Vector3 SpawnPoint { get; set; }

        public bool AutoRespawn
        {
            get => autoRespawn;
            set => autoRespawn = value;
        }

        /// <summary>Ball position in the physics maze's local space.</summary>
        public Vector3 LocalPosition => root != null ? root.InverseTransformPoint(Body.position) : Body.position;

        /// <summary>Rolling resistance. Unity barely simulates rolling friction, so weather effects change this.</summary>
        public float LinearDamping
        {
            get => Body.linearDamping;
            set => Body.linearDamping = Mathf.Max(0f, value);
        }

        /// <summary>The ball has finished falling into a hole (the trigger is null if it left the maze another way).</summary>
        public event Action<MazeBall, MazeTrigger> FellIntoHole;

        /// <summary>The ball touched an exit or checkpoint trigger.</summary>
        public event Action<MazeBall, MazeTrigger> TriggerEntered;

        /// <summary>The ball was placed somewhere with Respawn.</summary>
        public event Action<MazeBall> Respawned;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
            Sphere = GetComponent<SphereCollider>();
        }

        internal void Init(Transform physicsRoot, Collider floorCollider, float captureRadius)
        {
            root = physicsRoot;
            floor = floorCollider;
            holeCaptureRadius = captureRadius;
            Body = GetComponent<Rigidbody>();
            Sphere = GetComponent<SphereCollider>();
        }

        /// <summary>Stops the ball where it is (tracking lost, pause) and later resumes with the same velocity.</summary>
        public void SetFrozen(bool frozen)
        {
            if (frozen == IsFrozen)
                return;

            IsFrozen = frozen;
            if (frozen)
            {
                savedVelocity = Body.linearVelocity;
                savedAngularVelocity = Body.angularVelocity;
                Body.linearVelocity = Vector3.zero;
                Body.angularVelocity = Vector3.zero;
                Body.constraints = RigidbodyConstraints.FreezeAll;
            }
            else
            {
                Body.constraints = RigidbodyConstraints.None;
                Body.linearVelocity = savedVelocity;
                Body.angularVelocity = savedAngularVelocity;
            }
        }

        /// <summary>Puts the ball at rest at a position in the physics maze's local space.</summary>
        public void Respawn(Vector3 localPosition)
        {
            if (floor != null)
                Physics.IgnoreCollision(Sphere, floor, false);

            IsFalling = false;
            fallTimer = 0f;
            currentHole = null;
            overlappingHoles.Clear();

            savedVelocity = Vector3.zero;
            savedAngularVelocity = Vector3.zero;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;

            Vector3 world = root != null ? root.TransformPoint(localPosition) : localPosition;
            Body.position = world;
            transform.position = world;

            Respawned?.Invoke(this);
        }

        public void RespawnAtSpawnPoint()
        {
            Respawn(SpawnPoint);
        }

        void FixedUpdate()
        {
            if (IsFrozen)
                return;

            Body.AddForce(Gravity, ForceMode.Acceleration);

            if (IsFalling)
            {
                fallTimer += Time.fixedDeltaTime;
                if (fallTimer >= fallDuration)
                    FinishFall();
                return;
            }

            // Below the floor without a hole: something pushed it out. Treat it like a fall.
            if (LocalPosition.y < -1f)
            {
                currentHole = null;
                FinishFall();
                return;
            }

            CheckHoles();
        }

        void CheckHoles()
        {
            Vector3 up = root != null ? root.up : Vector3.up;
            for (int i = overlappingHoles.Count - 1; i >= 0; i--)
            {
                MazeTrigger hole = overlappingHoles[i];
                if (hole == null)
                {
                    overlappingHoles.RemoveAt(i);
                    continue;
                }

                Vector3 offset = Vector3.ProjectOnPlane(Body.position - hole.transform.position, up);
                if (offset.sqrMagnitude <= holeCaptureRadius * holeCaptureRadius)
                {
                    StartFall(hole);
                    return;
                }
            }
        }

        void StartFall(MazeTrigger hole)
        {
            IsFalling = true;
            fallTimer = 0f;
            currentHole = hole;

            if (floor != null)
                Physics.IgnoreCollision(Sphere, floor, true);

            // Steer the ball over the centre of the hole so it drops straight in.
            Vector3 up = root != null ? root.up : Vector3.up;
            Vector3 toCentre = Vector3.ProjectOnPlane(hole.transform.position - Body.position, up);
            Vector3 vertical = Vector3.Project(Body.linearVelocity, up);
            Body.linearVelocity = vertical + toCentre * 8f;

            if (logEvents)
                Debug.Log($"[MazeBall] Fell into the hole at row {hole.Row + 1}, column {hole.Col + 1}.");
        }

        void FinishFall()
        {
            MazeTrigger hole = currentHole;
            IsFalling = false;
            FellIntoHole?.Invoke(this, hole);

            if (autoRespawn)
                RespawnAtSpawnPoint();
        }

        void OnTriggerEnter(Collider other)
        {
            MazeTrigger trigger = other.GetComponent<MazeTrigger>();
            if (trigger == null)
                return;

            if (trigger.Kind == MazeTriggerKind.Hole)
            {
                if (!overlappingHoles.Contains(trigger))
                    overlappingHoles.Add(trigger);
                return;
            }

            if (IsFalling)
                return;

            if (logEvents)
                Debug.Log($"[MazeBall] Reached {trigger.Kind} {trigger.Index} (row {trigger.Row + 1}, column {trigger.Col + 1}).");
            TriggerEntered?.Invoke(this, trigger);
        }

        void OnTriggerExit(Collider other)
        {
            MazeTrigger trigger = other.GetComponent<MazeTrigger>();
            if (trigger != null && trigger.Kind == MazeTriggerKind.Hole)
                overlappingHoles.Remove(trigger);
        }
    }
}
