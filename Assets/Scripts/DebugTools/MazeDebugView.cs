using UnityEngine;
using UnityEngine.InputSystem;

namespace RVA
{
    /// <summary>
    /// A picture-in-picture camera that shows the hidden physics maze, so the levels and the physics
    /// can be tested with the arrow keys without a marker. Needs "Render Physics Copy" on MazeBuilder.
    /// V shows or hides it.
    /// </summary>
    public class MazeDebugView : MonoBehaviour
    {
        [SerializeField] MazeBuilder builder;
        [SerializeField] bool showInEditor = true;
        [SerializeField] bool showInBuild = false;
        [Tooltip("Part of the screen used by the view (x, y, width, height, from 0 to 1).")]
        [SerializeField] Rect viewport = new Rect(0.62f, 0.02f, 0.36f, 0.48f);
        [Tooltip("Angle of the view above the maze: 90 looks straight down.")]
        [SerializeField, Range(20f, 90f)] float pitch = 65f;

        Camera viewCamera;

        void Awake()
        {
            if (builder == null)
                builder = GetComponent<MazeBuilder>();
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();
        }

        void Start()
        {
            viewCamera = new GameObject("Maze Debug Camera").AddComponent<Camera>();
            viewCamera.transform.SetParent(transform, false);
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
            viewCamera.depth = 50f;
            viewCamera.nearClipPlane = 0.1f;
            viewCamera.farClipPlane = 200f;
            viewCamera.rect = viewport;
            if (builder != null)
                viewCamera.cullingMask = 1 << builder.HiddenLayer;
            viewCamera.enabled = Application.isEditor ? showInEditor : showInBuild;
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (viewCamera != null && keyboard != null && keyboard.vKey.wasPressedThisFrame)
                viewCamera.enabled = !viewCamera.enabled;
        }

        void LateUpdate()
        {
            if (viewCamera == null || !viewCamera.enabled)
                return;
            if (builder == null || builder.PhysicsRoot == null || builder.Layout == null)
                return;

            float size = Mathf.Max(builder.Layout.Rows, builder.Layout.Cols);
            float halfFov = viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float distance = size * 0.65f / Mathf.Tan(halfFov) / Mathf.Min(viewCamera.aspect, 1f);

            Quaternion rotation = Quaternion.Euler(pitch, 0f, 0f);
            viewCamera.transform.SetPositionAndRotation(builder.PhysicsRoot.position - rotation * Vector3.forward * distance, rotation);
        }
    }
}
