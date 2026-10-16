using UnityEngine;

namespace RVA
{
    public enum GateColor
    {
        Red,
        Blue
    }

    /// <summary>
    /// A gate in the hidden physics maze. When open it slides down into the floor so the ball can pass.
    /// This script only moves the gate; deciding when to open it is the job of KeyGate (Phase 2).
    /// Created by MazeBuilder; you do not add it by hand.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MazeGate : MonoBehaviour
    {
        [SerializeField] GateColor keyColor;
        [Tooltip("Sliding speed in cells per second.")]
        [SerializeField, Min(0.1f)] float slideSpeed = 4f;

        Rigidbody body;
        Vector3 closedLocalPosition;
        float travel = 1f;

        public GateColor KeyColor => keyColor;
        public int Row { get; private set; }
        public int Col { get; private set; }
        public bool IsOpen { get; private set; }

        /// <summary>0 = fully closed, 1 = fully open.</summary>
        public float OpenAmount { get; private set; }

        internal void Init(GateColor color, int row, int col, float travelDistance)
        {
            keyColor = color;
            Row = row;
            Col = col;
            travel = Mathf.Max(0.01f, travelDistance);
            closedLocalPosition = transform.localPosition;

            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
        }

        void FixedUpdate()
        {
            if (body == null)
                return;

            float target = IsOpen ? 1f : 0f;
            if (Mathf.Approximately(OpenAmount, target))
                return;

            OpenAmount = Mathf.MoveTowards(OpenAmount, target, slideSpeed / travel * Time.fixedDeltaTime);
            Vector3 local = closedLocalPosition + Vector3.down * (travel * OpenAmount);
            Transform parent = transform.parent;
            body.MovePosition(parent != null ? parent.TransformPoint(local) : local);
        }
    }
}
