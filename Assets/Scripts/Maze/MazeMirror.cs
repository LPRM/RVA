using UnityEngine;

namespace RVA
{
    /// <summary>
    /// Every frame, copies the moving parts (ball, gates) from the hidden physics maze to the visible
    /// maze on the marker, and keeps the visible maze's scale, rotation offset and visibility up to date.
    /// The visible maze has no colliders, so marker jitter can never push the ball.
    /// </summary>
    [DefaultExecutionOrder(300)]
    public class MazeMirror : MonoBehaviour
    {
        [Header("References (found automatically when empty)")]
        [SerializeField] MazeBuilder builder;
        [SerializeField] MazeGravity gravity;
        [SerializeField] MazeTracker tracker;

        [Tooltip("Hide the visible maze while the marker is lost.")]
        [SerializeField] bool hideWhenLost = true;
        [Tooltip("How far below the floor, in cells, the falling ball has shrunk to nothing.")]
        [SerializeField, Min(0.05f)] float fallShrinkDepth = 0.8f;

        void Awake()
        {
            if (builder == null)
                builder = GetComponent<MazeBuilder>();
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();
            if (gravity == null)
                gravity = GetComponent<MazeGravity>();
            if (gravity == null)
                gravity = FindAnyObjectByType<MazeGravity>();
            if (tracker == null)
                tracker = FindAnyObjectByType<MazeTracker>();
        }

        void LateUpdate()
        {
            if (builder == null || builder.PhysicsRoot == null || builder.VisualRoot == null)
                return;

            Transform visualRoot = builder.VisualRoot;
            bool visible = !hideWhenLost || tracker == null || !MazeTracker.VuforiaAvailable || tracker.IsTracked;
            if (visualRoot.gameObject.activeSelf != visible)
                visualRoot.gameObject.SetActive(visible);
            if (!visible)
                return;

            builder.ApplyVisualScale();
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.Euler(0f, gravity != null ? gravity.RotationOffset : 0f, 0f);

            Transform physicsRoot = builder.PhysicsRoot;
            Quaternion inverseRootRotation = Quaternion.Inverse(physicsRoot.rotation);

            foreach (MazeBuilder.MirrorPair pair in builder.MirrorPairs)
            {
                if (pair.Physics == null || pair.Visual == null)
                    continue;

                Vector3 local = physicsRoot.InverseTransformPoint(pair.Physics.position);
                pair.Visual.localPosition = local;
                pair.Visual.localRotation = inverseRootRotation * pair.Physics.rotation;

                if (pair.ShrinkWhenFalling)
                {
                    float sunk = builder.BallRadius - local.y; // > 0 once the ball drops below the floor
                    float scale = sunk > 0f ? Mathf.Clamp01(1f - sunk / fallShrinkDepth) : 1f;
                    pair.Visual.localScale = pair.BaseScale * scale;
                }
            }
        }
    }
}
