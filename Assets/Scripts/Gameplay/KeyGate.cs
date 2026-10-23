#if RVA_VUFORIA
using Vuforia;
#endif
using UnityEngine;
using System.Collections.Generic;

namespace RVA
{
    /// <summary>
    /// Goes on a key ImageTarget (red or blue). Opens the gates of its colour while the key is close
    /// to them and closes them when it moves away or is lost. Distances are measured in the visible
    /// maze's local space (on the maze marker), in maze cells, ignoring height. While the maze marker
    /// is lost the gates keep their state, since its pose is stale (the ball is frozen then anyway).
    /// Set debugKey to any Transform to test in Play mode without the printed key.
    /// </summary>
    public class KeyGate : MonoBehaviour
    {
        [SerializeField] bool logEvents = true;
        [Tooltip("Log the distance to the nearest gate twice per second, for tuning.")]
        [SerializeField] bool logDistance = false;

        [SerializeField] GateColor color;
        [Tooltip("The maze ImageTarget. Found automatically (via its MazeTracker) when empty.")]
        [SerializeField] Transform mazeTransform;
        [Tooltip("Found on mazeTransform when empty.")]
        [SerializeField] MazeTracker mazeTracker;
        [SerializeField] MazeBuilder builder;
        [Tooltip("Found automatically when empty. Gates only move while the game is Playing; without a GameManager they always follow the key.")]
        [SerializeField] GameManager game;

        [Tooltip("The gate opens when the key is closer than this, in maze cells (1 = one grid square).")]
        [SerializeField, Min(0f)] float openDistanceCells = 3f;
        [Tooltip("The gate closes when the key is farther than this, in maze cells. Keep it above the open distance.")]
        [SerializeField, Min(0f)] float closeDistanceCells = 4f;

        [SerializeField] Transform debugKey;
        [SerializeField, Min(0f)] float lostGraceTime = 0.15f;

        [SerializeField] bool acceptExtendedTracked = true;
        [SerializeField] bool acceptLimited = false;

        bool statusOk;
        float lostTimer;
        float distanceLogTimer;
        public string StatusText { get; private set; } = "NO_POSE";
        public bool IsTracked { get; private set; }
        public GateColor KeyColor => color;

        /// <summary>Point used to measure the distance to the gates (e.g. the tip of KeyVisual's stick).
        /// When null, the marker's centre is used.</summary>
        public Transform ContactPoint { get; set; }

        /// <summary>Distance in cells from the key to the nearest gate of this colour (infinity when unknown).</summary>
        public float NearestDistance { get; private set; } = float.PositiveInfinity;

        List<(MazeGate gate, Transform visual)> gates = new List<(MazeGate gate, Transform visual)>();

#if RVA_VUFORIA
        ObserverBehaviour observer;

        void Awake()
        {
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();

            observer = GetComponent<ObserverBehaviour>();
            if (observer == null)
            {
                Debug.LogError("[KeyGate] Put this script on a key ImageTarget (it needs an ObserverBehaviour).", this);
                enabled = false;
                return;
            }
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        }

        void OnDestroy()
        {
            if(observer != null)
            {
                observer.OnTargetStatusChanged -= OnTargetStatusChanged;
            }
        }

        void OnTargetStatusChanged(ObserverBehaviour behaviour, TargetStatus targetStatus)
        {
            Status status = targetStatus.Status;
            StatusText = status.ToString();
            statusOk = status == Status.TRACKED
                       || (acceptExtendedTracked && status == Status.EXTENDED_TRACKED)
                       || (acceptLimited && status == Status.LIMITED);
        }
#else
        void Awake()
        {
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();

            StatusText = "Vuforia not installed";
            Debug.LogWarning("[KeyGate] The Vuforia Engine package is not installed, so the marker cannot be tracked. ", this);
        }
#endif
        void Start()
        {
            if (mazeTracker == null && mazeTransform != null)
                mazeTracker = mazeTransform.GetComponent<MazeTracker>();
            if (mazeTracker == null)
                mazeTracker = FindAnyObjectByType<MazeTracker>();
            if (mazeTransform == null && mazeTracker != null)
                mazeTransform = mazeTracker.transform;
            if (game == null)
                game = FindAnyObjectByType<GameManager>();

            if (builder == null)
                Debug.LogWarning("[KeyGate] No MazeBuilder in the scene, so there are no gates to open.", this);
            if (mazeTransform == null)
                Debug.LogWarning("[KeyGate] No maze marker found. Set Maze Transform to the maze ImageTarget.", this);

            RefreshGates();
        }

        void OnValidate()
        {
            closeDistanceCells = Mathf.Max(closeDistanceCells, openDistanceCells);
        }

        void OnBuilt(MazeBuilder built)
        {
            RefreshGates();
        }

        void OnEnable()
        {
            if (builder != null)
                builder.Built += OnBuilt;
        }

        void OnDisable()
        {
            if (builder != null)
                builder.Built -= OnBuilt;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (statusOk)
            {
                lostTimer = 0.0f;
                if (!IsTracked)
                {
                    IsTracked = true;
                    if (logEvents) Debug.Log($"[KeyGate] {color} key found");
                }
            }
            else if (IsTracked)
            {
                lostTimer += dt;
                if (lostTimer >= lostGraceTime)
                {
                    IsTracked = false;
                    if (logEvents) Debug.Log($"[KeyGate] {color} key lost");
                }
            }

            UpdateGates();
            LogDistance(dt);
        }

        void RefreshGates()
        {
            if(builder == null)
            {
                return;
            }

            gates.Clear();

            foreach (var pair in builder.MirrorPairs)
            {
                if (pair.Physics != null && pair.Physics.TryGetComponent(out MazeGate gate) && gate.KeyColor == color)
                {
                    gates.Add((gate, pair.Visual));
                }
            }

            if (logEvents) Debug.Log($"[KeyGate] {gates.Count} {color} gate(s) found.");
        }

        void UpdateGates()
        {
            NearestDistance = float.PositiveInfinity;

            // Not playing (paused, won, time up, menu): the gates keep their state.
            if (game != null && !game.IsPlaying)
                return;

            // Maze lost: its pose is stale, so the distances would be wrong. Keep the gates as they are.
            // The debug key skips this check so it can be tested without the maze marker in view.
            bool mazeLost = mazeTracker != null && MazeTracker.VuforiaAvailable && !mazeTracker.IsTracked;
            if (mazeLost && debugKey == null)
                return;

            // Key gone (or nothing to measure against): close this key's gates.
            bool keyAvailable = debugKey != null || IsTracked;
            if (mazeTransform == null || builder == null || builder.VisualRoot == null || !keyAvailable)
            {
                foreach (var (gate, visual) in gates)
                {
                    if (gate != null && gate.IsOpen)
                    {
                        gate.SetOpen(false);
                        if (logEvents) Debug.Log($"[KeyGate] {gate.name} closed (key not available).");
                    }
                }
                return;
            }

            Vector3 keyPosition = debugKey != null ? debugKey.position
                                : ContactPoint != null ? ContactPoint.position
                                : transform.position;

            foreach (var (gate, visual) in gates)
            {
                if (gate == null || visual == null)
                    continue;

                float distance = DistanceInMaze(keyPosition, visual.position);
                NearestDistance = Mathf.Min(NearestDistance, distance);

                if(!gate.IsOpen && distance <= openDistanceCells)
                {
                    gate.SetOpen(true);
                    if (logEvents) Debug.Log($"[KeyGate] {gate.name} opened (distance {distance:0.0} cells).");
                }
                else if(gate.IsOpen && distance >= closeDistanceCells)
                {
                    gate.SetOpen(false);
                    if (logEvents) Debug.Log($"[KeyGate] {gate.name} closed (distance {distance:0.0} cells).");
                }
            }
        }

        /// <summary>
        /// Distance between two world points, measured in the visible maze's local space (on the maze
        /// marker), so it is in maze cells and does not depend on where the camera is. Height is ignored,
        /// so holding the key above the board still counts.
        /// </summary>
        float DistanceInMaze(Vector3 keyPos, Vector3 gatePos)
        {
            Transform mazeSpace = builder.VisualRoot;
            Vector3 keyLocal  = mazeSpace.InverseTransformPoint(keyPos);
            Vector3 gateLocal = mazeSpace.InverseTransformPoint(gatePos);

            return Vector2.Distance(new Vector2(keyLocal.x, keyLocal.z), new Vector2(gateLocal.x, gateLocal.z));
        }

        void LogDistance(float dt)
        {
            if (!logDistance)
                return;

            distanceLogTimer += dt;
            if (distanceLogTimer < 0.5f)
                return;
            distanceLogTimer = 0f;

            string distance = float.IsPositiveInfinity(NearestDistance) ? "n/a" : $"{NearestDistance:0.00} cells";
            Debug.Log($"[KeyGate] {color}: key {(debugKey != null ? "debug" : IsTracked ? "tracked" : "lost")}, " +
                      $"maze {(mazeTracker == null ? "no tracker" : mazeTracker.IsTracked ? "tracked" : "lost")}, " +
                      $"{gates.Count} gate(s), nearest {distance}", this);
        }

        void OnDrawGizmosSelected()
        {
            // Shows the open (green) and close (yellow) radius around each gate in the Scene view.
            if (builder == null || builder.VisualRoot == null)
                return;

            Gizmos.matrix = builder.VisualRoot.localToWorldMatrix;
            foreach (var (_, visual) in gates)
            {
                if (visual == null)
                    continue;
                Vector3 centre = builder.VisualRoot.InverseTransformPoint(visual.position);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(centre, openDistanceCells);
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(centre, closeDistanceCells);
            }
        }
    }
}
