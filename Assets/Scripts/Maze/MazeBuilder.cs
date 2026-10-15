using System;
using System.Collections.Generic;
using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Builds a maze from a text grid (legend in MazeLayout) twice:
    ///  - a hidden physics copy (colliders, triggers and the Rigidbody ball) that never moves, far
    ///    below the scene, where one unit is one cell;
    ///  - a visible copy under the maze marker with renderers only, kept in sync by MazeMirror.
    /// Tracking jitter therefore never moves a collider: only the direction of gravity changes.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MazeBuilder : MonoBehaviour
    {
        [Serializable]
        public class PieceLook
        {
            [Tooltip("Optional. When empty, a copy of the default material in the colour below is used.")]
            public Material material;
            public Color color = Color.white;

            public PieceLook() { }

            public PieceLook(Color color)
            {
                this.color = color;
            }
        }

        /// <summary>A moving object in the physics maze and its visible copy.</summary>
        public readonly struct MirrorPair
        {
            public readonly Transform Physics;
            public readonly Transform Visual;
            public readonly bool ShrinkWhenFalling;
            public readonly Vector3 BaseScale;

            public MirrorPair(Transform physics, Transform visual, bool shrinkWhenFalling)
            {
                Physics = physics;
                Visual = visual;
                ShrinkWhenFalling = shrinkWhenFalling;
                BaseScale = visual.localScale;
            }
        }

        [Header("Level")]
        [Tooltip("Text file with the grid. When empty, the layout below is used.")]
        [SerializeField] TextAsset levelFile;
        [SerializeField, TextArea(5, 16)] string fallbackLayout =
            "#########\n" +
            "#S..#..E#\n" +
            "#.O.R...#\n" +
            "#########";
        [SerializeField] bool buildOnStart = true;

        [Header("Placement")]
        [Tooltip("Parent of the visible maze: the maze ImageTarget. When empty, the object with MazeTracker is used.")]
        [SerializeField] Transform visualParent;
        [Tooltip("Length of the visible maze's longest side in metres (Vuforia works in metres).")]
        [SerializeField, Min(0.01f)] float visualSize = 0.13f;
        [Tooltip("Where the hidden physics maze is built. Far away so the AR camera never sees it.")]
        [SerializeField] Vector3 physicsOrigin = new Vector3(0f, -1000f, 0f);
        [Tooltip("Give the physics maze plain renderers so MazeDebugView (key V) can show it.")]
        [SerializeField] bool renderPhysicsCopy = true;
        [Tooltip("Layer of the hidden physics maze. The main (AR) camera stops rendering it; only the debug view shows it.")]
        [SerializeField, Range(8, 31)] int hiddenLayer = 29;

        [Header("Sizes (in cells)")]
        [SerializeField, Range(0.2f, 2f)] float wallHeight = 0.8f;
        [SerializeField, Range(0.1f, 0.45f)] float ballRadius = 0.3f;
        [Tooltip("Radius of the visible hole.")]
        [SerializeField, Range(0.2f, 0.5f)] float holeRadius = 0.42f;
        [Tooltip("The ball drops once its centre is this close to the centre of a hole.")]
        [SerializeField, Range(0.05f, 0.5f)] float holeCaptureRadius = 0.28f;

        [Header("Ball physics")]
        [SerializeField, Min(0.01f)] float ballMass = 1f;
        [SerializeField, Min(0f)] float ballLinearDamping = 0.3f;
        [SerializeField, Min(0f)] float ballAngularDamping = 0.3f;
        [SerializeField, Range(0f, 1f)] float ballFriction = 0.4f;
        [SerializeField, Range(0f, 1f)] float ballBounciness = 0.2f;

        [Header("Look (plain shapes for now; models come in Phase 4)")]
        [SerializeField] PieceLook floorLook = new PieceLook(new Color(0.86f, 0.8f, 0.68f));
        [SerializeField] PieceLook wallLook = new PieceLook(new Color(0.36f, 0.25f, 0.17f));
        [SerializeField] PieceLook ballLook = new PieceLook(new Color(0.8f, 0.82f, 0.88f));
        [SerializeField] PieceLook holeLook = new PieceLook(new Color(0.04f, 0.04f, 0.04f));
        [SerializeField] PieceLook exitLook = new PieceLook(new Color(0.2f, 0.85f, 0.3f));
        [SerializeField] PieceLook checkpointLook = new PieceLook(new Color(1f, 0.82f, 0.15f));
        [SerializeField] PieceLook redGateLook = new PieceLook(new Color(0.9f, 0.15f, 0.15f));
        [SerializeField] PieceLook blueGateLook = new PieceLook(new Color(0.15f, 0.35f, 0.95f));

        static readonly Dictionary<PrimitiveType, Mesh> meshCache = new Dictionary<PrimitiveType, Mesh>();
        static Material defaultMaterial;

        readonly Dictionary<PieceLook, Material> materials = new Dictionary<PieceLook, Material>();
        readonly List<MazeGate> gates = new List<MazeGate>();
        readonly List<MazeTrigger> holes = new List<MazeTrigger>();
        readonly List<MazeTrigger> checkpoints = new List<MazeTrigger>();
        readonly List<MazeTrigger> exits = new List<MazeTrigger>();
        readonly List<MirrorPair> mirrorPairs = new List<MirrorPair>();
        PhysicsMaterial ballPhysicsMaterial;
        Collider floorCollider;

        public MazeLayout Layout { get; private set; }
        public Transform PhysicsRoot { get; private set; }
        public Transform VisualRoot { get; private set; }
        public MazeBall Ball { get; private set; }
        public Transform VisualBall { get; private set; }

        /// <summary>Where the ball starts, in the physics maze's local space.</summary>
        public Vector3 StartLocalPosition { get; private set; }

        public IReadOnlyList<MazeGate> Gates => gates;
        public IReadOnlyList<MazeTrigger> Holes => holes;
        public IReadOnlyList<MazeTrigger> Checkpoints => checkpoints;
        public IReadOnlyList<MazeTrigger> Exits => exits;
        public IReadOnlyList<MirrorPair> MirrorPairs => mirrorPairs;
        public Transform VisualParent => visualParent;
        public float BallRadius => ballRadius;
        public float WallHeight => wallHeight;
        public int HiddenLayer => hiddenLayer;

        /// <summary>Called after every successful Build.</summary>
        public event Action<MazeBuilder> Built;

        void Awake()
        {
            if (visualParent == null)
            {
                MazeTracker tracker = FindAnyObjectByType<MazeTracker>();
                if (tracker != null)
                    visualParent = tracker.transform;
            }
        }

        void Start()
        {
            // The AR camera must never draw the hidden copy, whatever Vuforia's world centre mode is.
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                mainCamera.cullingMask &= ~(1 << hiddenLayer);

            if (buildOnStart)
                BuildFromInspector();
        }

        void OnDestroy()
        {
            Clear();
            foreach (Material material in materials.Values)
                if (material != null)
                    Destroy(material);
            materials.Clear();
            if (ballPhysicsMaterial != null)
                Destroy(ballPhysicsMaterial);
        }

        public bool BuildFromInspector()
        {
            return Build(levelFile != null ? levelFile.text : fallbackLayout);
        }

        public bool Build(TextAsset file)
        {
            if (file == null)
            {
                Debug.LogError("[MazeBuilder] No level file given.", this);
                return false;
            }
            return Build(file.text);
        }

        /// <summary>Removes the current maze (if any) and builds a new one from grid text.</summary>
        public bool Build(string layoutText)
        {
            MazeLayout layout;
            try
            {
                layout = MazeLayout.Parse(layoutText);
            }
            catch (FormatException e)
            {
                Debug.LogError("[MazeBuilder] " + e.Message, this);
                return false;
            }

            foreach (string warning in layout.Warnings)
                Debug.LogWarning("[MazeBuilder] " + warning, this);

            if (visualParent == null)
                Debug.LogWarning("[MazeBuilder] No visual parent (maze ImageTarget) found; the visible maze is built under " + name + ".", this);

            Clear();
            Layout = layout;
            BuildPhysics();
            BuildVisual();
            Built?.Invoke(this);
            return true;
        }

        public void Clear()
        {
            if (PhysicsRoot != null)
            {
                PhysicsRoot.gameObject.SetActive(false);
                Destroy(PhysicsRoot.gameObject);
            }
            if (VisualRoot != null)
            {
                VisualRoot.gameObject.SetActive(false);
                Destroy(VisualRoot.gameObject);
            }

            PhysicsRoot = null;
            VisualRoot = null;
            Ball = null;
            VisualBall = null;
            floorCollider = null;
            gates.Clear();
            holes.Clear();
            checkpoints.Clear();
            exits.Clear();
            mirrorPairs.Clear();
        }

        /// <summary>Centre of a cell at floor level, in the local space of either maze copy.</summary>
        public Vector3 CellToLocal(float row, float col)
        {
            return new Vector3(col - (Layout.Cols - 1) * 0.5f, 0f, (Layout.Rows - 1) * 0.5f - row);
        }

        /// <summary>Scales the visible maze so its longest side is visualSize metres in the world.</summary>
        public void ApplyVisualScale()
        {
            if (VisualRoot == null || Layout == null)
                return;

            float cellSize = visualSize / Mathf.Max(Layout.Rows, Layout.Cols);
            Transform parent = VisualRoot.parent;
            float parentScale = parent != null ? Mathf.Max(Mathf.Abs(parent.lossyScale.x), 1e-6f) : 1f;
            VisualRoot.localScale = Vector3.one * (cellSize / parentScale);
        }

        // ---------------------------------------------------------------- physics copy

        void BuildPhysics()
        {
            MazeLayout layout = Layout;
            PhysicsRoot = new GameObject("MazePhysics (hidden)").transform;
            PhysicsRoot.SetPositionAndRotation(physicsOrigin, Quaternion.identity);

            float width = layout.Cols;
            float depth = layout.Rows;

            // One floor box for the whole maze, so the ball never catches on seams between tiles.
            floorCollider = AddBox(PhysicsRoot, "Floor", new Vector3(0f, -0.25f, 0f), new Vector3(width + 2f, 0.5f, depth + 2f));

            // A lid just above the walls: the ball can never jump out of the maze.
            AddBox(PhysicsRoot, "Lid", new Vector3(0f, wallHeight + 0.05f, 0f), new Vector3(width + 2f, 0.1f, depth + 2f));

            // An invisible border, in case the edge of the grid is not all walls.
            float h = wallHeight;
            AddBox(PhysicsRoot, "Border N", new Vector3(0f, h * 0.5f, depth * 0.5f + 0.5f), new Vector3(width + 2f, h, 1f));
            AddBox(PhysicsRoot, "Border S", new Vector3(0f, h * 0.5f, -depth * 0.5f - 0.5f), new Vector3(width + 2f, h, 1f));
            AddBox(PhysicsRoot, "Border E", new Vector3(width * 0.5f + 0.5f, h * 0.5f, 0f), new Vector3(1f, h, depth));
            AddBox(PhysicsRoot, "Border W", new Vector3(-width * 0.5f - 0.5f, h * 0.5f, 0f), new Vector3(1f, h, depth));

            // Walls: one box per horizontal run of '#', for fewer colliders and fewer seams.
            for (int r = 0; r < layout.Rows; r++)
            {
                int c = 0;
                while (c < layout.Cols)
                {
                    if (layout[r, c] != MazeCell.Wall)
                    {
                        c++;
                        continue;
                    }

                    int first = c;
                    while (c < layout.Cols && layout[r, c] == MazeCell.Wall)
                        c++;
                    int length = c - first;
                    Vector3 centre = CellToLocal(r, first + (length - 1) * 0.5f) + Vector3.up * (h * 0.5f);
                    AddBox(PhysicsRoot, $"Wall row {r + 1}", centre, new Vector3(length, h, 1f));
                }
            }

            // Triggers and gates.
            for (int r = 0; r < layout.Rows; r++)
            {
                for (int c = 0; c < layout.Cols; c++)
                {
                    Vector3 cell = CellToLocal(r, c);
                    switch (layout[r, c])
                    {
                        case MazeCell.Hole:
                        {
                            MazeTrigger hole = AddTrigger(MazeTriggerKind.Hole, holes.Count, r, c, cell + Vector3.up * ballRadius);
                            var sphere = hole.gameObject.AddComponent<SphereCollider>();
                            sphere.isTrigger = true;
                            sphere.radius = 0.5f;
                            holes.Add(hole);
                            break;
                        }
                        case MazeCell.Exit:
                            exits.Add(AddBoxTrigger(MazeTriggerKind.Exit, exits.Count, r, c, cell));
                            break;
                        case MazeCell.Checkpoint:
                            checkpoints.Add(AddBoxTrigger(MazeTriggerKind.Checkpoint, checkpoints.Count, r, c, cell));
                            break;
                        case MazeCell.RedGate:
                            gates.Add(AddPhysicsGate(GateColor.Red, r, c, cell));
                            break;
                        case MazeCell.BlueGate:
                            gates.Add(AddPhysicsGate(GateColor.Blue, r, c, cell));
                            break;
                    }
                }
            }

            BuildPhysicsBall();

            if (renderPhysicsCopy)
            {
                var debugRoot = new GameObject("Debug renderers").transform;
                debugRoot.SetParent(PhysicsRoot, false);
                AddStaticPieces(debugRoot);
            }

            SetLayerRecursively(PhysicsRoot, hiddenLayer);
        }

        static void SetLayerRecursively(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target)
                SetLayerRecursively(child, layer);
        }

        void BuildPhysicsBall()
        {
            (int row, int col) start = Layout.Start;
            StartLocalPosition = CellToLocal(start.row, start.col) + Vector3.up * (ballRadius + 0.01f);

            var ballObject = new GameObject("Ball");
            ballObject.transform.SetParent(PhysicsRoot, false);
            ballObject.transform.localPosition = StartLocalPosition;

            var sphere = ballObject.AddComponent<SphereCollider>();
            sphere.radius = ballRadius;
            sphere.sharedMaterial = GetBallPhysicsMaterial();

            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = ballMass;
            body.linearDamping = ballLinearDamping;
            body.angularDamping = ballAngularDamping;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 50f;
            body.sleepThreshold = 0f;

            Ball = ballObject.AddComponent<MazeBall>();
            Ball.Init(PhysicsRoot, floorCollider, holeCaptureRadius);
            Ball.SpawnPoint = StartLocalPosition;

            if (renderPhysicsCopy)
                AddMesh(ballObject.transform, "Debug renderer", PrimitiveType.Sphere, Vector3.zero, Vector3.one * (ballRadius * 2f), ballLook);
        }

        PhysicsMaterial GetBallPhysicsMaterial()
        {
            if (ballPhysicsMaterial == null)
            {
                ballPhysicsMaterial = new PhysicsMaterial("Maze Ball")
                {
                    dynamicFriction = ballFriction,
                    staticFriction = ballFriction,
                    bounciness = ballBounciness,
                    frictionCombine = PhysicsMaterialCombine.Average,
                    bounceCombine = PhysicsMaterialCombine.Maximum
                };
            }
            return ballPhysicsMaterial;
        }

        MazeGate AddPhysicsGate(GateColor color, int row, int col, Vector3 cellPosition)
        {
            var gateObject = new GameObject($"{color} gate {row + 1},{col + 1}");
            gateObject.transform.SetParent(PhysicsRoot, false);
            gateObject.transform.localPosition = cellPosition;

            var box = gateObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, wallHeight * 0.5f, 0f);
            box.size = new Vector3(1f, wallHeight, 1f);
            gateObject.AddComponent<Rigidbody>();

            MazeGate gate = gateObject.AddComponent<MazeGate>();
            gate.Init(color, row, col, wallHeight + 0.02f);

            if (renderPhysicsCopy)
                AddGateMesh(gateObject.transform, color);
            return gate;
        }

        MazeTrigger AddBoxTrigger(MazeTriggerKind kind, int index, int row, int col, Vector3 cellPosition)
        {
            MazeTrigger trigger = AddTrigger(kind, index, row, col, cellPosition + Vector3.up * ballRadius);
            var box = trigger.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.7f, ballRadius * 2f, 0.7f);
            return trigger;
        }

        MazeTrigger AddTrigger(MazeTriggerKind kind, int index, int row, int col, Vector3 localPosition)
        {
            var triggerObject = new GameObject($"{kind} {row + 1},{col + 1}");
            triggerObject.transform.SetParent(PhysicsRoot, false);
            triggerObject.transform.localPosition = localPosition;
            MazeTrigger trigger = triggerObject.AddComponent<MazeTrigger>();
            trigger.Init(kind, index, row, col);
            return trigger;
        }

        static BoxCollider AddBox(Transform parent, string objectName, Vector3 localCentre, Vector3 size)
        {
            var boxObject = new GameObject(objectName);
            boxObject.transform.SetParent(parent, false);
            boxObject.transform.localPosition = localCentre;
            var box = boxObject.AddComponent<BoxCollider>();
            box.size = size;
            return box;
        }

        // ---------------------------------------------------------------- visible copy

        void BuildVisual()
        {
            VisualRoot = new GameObject("MazeVisual").transform;
            VisualRoot.SetParent(visualParent != null ? visualParent : transform, false);
            VisualRoot.localPosition = Vector3.zero;
            VisualRoot.localRotation = Quaternion.identity;
            ApplyVisualScale();

            AddStaticPieces(VisualRoot);

            VisualBall = AddMesh(VisualRoot, "Ball", PrimitiveType.Sphere, StartLocalPosition, Vector3.one * (ballRadius * 2f), ballLook).transform;
            mirrorPairs.Add(new MirrorPair(Ball.transform, VisualBall, true));

            foreach (MazeGate gate in gates)
            {
                var gateObject = new GameObject(gate.name);
                gateObject.transform.SetParent(VisualRoot, false);
                gateObject.transform.localPosition = CellToLocal(gate.Row, gate.Col);
                AddGateMesh(gateObject.transform, gate.KeyColor);
                mirrorPairs.Add(new MirrorPair(gate.transform, gateObject.transform, false));
            }
        }

        /// <summary>Floor, walls, holes, exits and checkpoints: everything that never moves.</summary>
        void AddStaticPieces(Transform parent)
        {
            MazeLayout layout = Layout;
            AddMesh(parent, "Floor", PrimitiveType.Cube, new Vector3(0f, -0.05f, 0f), new Vector3(layout.Cols, 0.1f, layout.Rows), floorLook);

            for (int r = 0; r < layout.Rows; r++)
            {
                for (int c = 0; c < layout.Cols; c++)
                {
                    Vector3 cell = CellToLocal(r, c);
                    switch (layout[r, c])
                    {
                        case MazeCell.Wall:
                            AddMesh(parent, $"Wall {r + 1},{c + 1}", PrimitiveType.Cube,
                                cell + Vector3.up * (wallHeight * 0.5f), new Vector3(1f, wallHeight, 1f), wallLook);
                            break;
                        case MazeCell.Hole:
                            AddMesh(parent, $"Hole {r + 1},{c + 1}", PrimitiveType.Cylinder,
                                cell + Vector3.up * 0.01f, new Vector3(holeRadius * 2f, 0.005f, holeRadius * 2f), holeLook);
                            break;
                        case MazeCell.Exit:
                            AddMesh(parent, $"Exit {r + 1},{c + 1}", PrimitiveType.Cube,
                                cell + Vector3.up * 0.01f, new Vector3(0.8f, 0.02f, 0.8f), exitLook);
                            break;
                        case MazeCell.Checkpoint:
                            AddMesh(parent, $"Checkpoint {r + 1},{c + 1}", PrimitiveType.Cylinder,
                                cell + Vector3.up * 0.01f, new Vector3(0.6f, 0.01f, 0.6f), checkpointLook);
                            break;
                    }
                }
            }
        }

        void AddGateMesh(Transform gateTransform, GateColor color)
        {
            AddMesh(gateTransform, "Gate mesh", PrimitiveType.Cube, new Vector3(0f, wallHeight * 0.5f, 0f),
                new Vector3(0.96f, wallHeight * 0.96f, 0.96f), color == GateColor.Red ? redGateLook : blueGateLook);
        }

        GameObject AddMesh(Transform parent, string objectName, PrimitiveType shape, Vector3 localPosition, Vector3 localScale, PieceLook look)
        {
            var meshObject = new GameObject(objectName);
            meshObject.transform.SetParent(parent, false);
            meshObject.transform.localPosition = localPosition;
            meshObject.transform.localScale = localScale;
            meshObject.AddComponent<MeshFilter>().sharedMesh = GetPrimitiveMesh(shape);
            meshObject.AddComponent<MeshRenderer>().sharedMaterial = GetMaterial(look);
            return meshObject;
        }

        Material GetMaterial(PieceLook look)
        {
            if (look.material != null)
                return look.material;

            if (!materials.TryGetValue(look, out Material material) || material == null)
            {
                GetPrimitiveMesh(PrimitiveType.Cube); // makes sure defaultMaterial is set
                material = new Material(defaultMaterial) { color = look.color };
                materials[look] = material;
            }
            return material;
        }

        static Mesh GetPrimitiveMesh(PrimitiveType shape)
        {
            if (!meshCache.TryGetValue(shape, out Mesh mesh) || mesh == null || defaultMaterial == null)
            {
                GameObject temporary = GameObject.CreatePrimitive(shape);
                temporary.SetActive(false);
                mesh = temporary.GetComponent<MeshFilter>().sharedMesh;
                defaultMaterial = temporary.GetComponent<MeshRenderer>().sharedMaterial;
                Destroy(temporary);
                meshCache[shape] = mesh;
            }
            return mesh;
        }
    }
}
