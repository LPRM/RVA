using System;
using UnityEngine;
#if RVA_VUFORIA
using Vuforia;
#endif

namespace RVA
{
    /// <summary>
    /// Goes on the maze ImageTarget. Reads the Vuforia tracking state (OnTargetStatusChanged) and
    /// gives a smoothed marker rotation relative to the camera, for MazeGravity.
    /// Without the Vuforia package it compiles to a stub that is never tracked (use the keyboard tilt).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public class MazeTracker : MonoBehaviour
    {
        [Tooltip("Camera that sees the marker. When empty, Camera.main (the ARCamera) is used.")]
        [SerializeField] Transform arCamera;
        [Tooltip("Measure the marker's rotation relative to the camera. Keep this on for a webcam that does not move.")]
        [SerializeField] bool relativeToCamera = true;

        [Header("Tracking state")]
#pragma warning disable CS0414 // only read when the Vuforia package is installed
        [SerializeField] bool acceptExtendedTracked = true;
        [SerializeField] bool acceptLimited = false;
#pragma warning restore CS0414
        [Tooltip("How long the marker can disappear before it counts as lost (hides short flickers).")]
        [SerializeField, Min(0f)] float lostGraceTime = 0.15f;

        [Header("Smoothing")]
        [Tooltip("Higher follows the marker faster; lower is smoother but lags more.")]
        [SerializeField, Min(0.1f)] float rotationSharpness = 12f;

        bool statusOk;
        float lostTimer;

        /// <summary>True while the marker is seen (with a short grace period).</summary>
        public bool IsTracked { get; private set; }

        /// <summary>Last Vuforia status, for debugging.</summary>
        public string StatusText { get; private set; } = "NO_POSE";

        /// <summary>Seconds since the marker was last found.</summary>
        public float TrackedTime { get; private set; }

        /// <summary>Marker rotation this frame (relative to the camera unless relativeToCamera is off).</summary>
        public Quaternion RawRotation { get; private set; } = Quaternion.identity;

        /// <summary>Marker rotation with jitter smoothed out.</summary>
        public Quaternion SmoothedRotation { get; private set; } = Quaternion.identity;

        public event Action TrackingFound;
        public event Action TrackingLost;

        public static bool VuforiaAvailable
        {
            get
            {
#if RVA_VUFORIA
                return true;
#else
                return false;
#endif
            }
        }

#if RVA_VUFORIA
        ObserverBehaviour observer;

        void Awake()
        {
            observer = GetComponent<ObserverBehaviour>();
            if (observer == null)
            {
                Debug.LogError("[MazeTracker] Put this script on the maze ImageTarget (it needs an ObserverBehaviour).", this);
                enabled = false;
                return;
            }
            observer.OnTargetStatusChanged += OnTargetStatusChanged;
        }

        void OnDestroy()
        {
            if (observer != null)
                observer.OnTargetStatusChanged -= OnTargetStatusChanged;
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
            StatusText = "Vuforia not installed";
            Debug.LogWarning("[MazeTracker] The Vuforia Engine package is not installed, so the marker cannot be tracked. " +
                             "Use the keyboard tilt (arrow keys) until it is installed (see Docs/SETUP.md).", this);
        }
#endif

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            if (statusOk)
            {
                lostTimer = 0f;
                RawRotation = ReadRotation();

                if (!IsTracked)
                {
                    // Snap on (re)acquire instead of sliding from a stale pose.
                    SmoothedRotation = RawRotation;
                    IsTracked = true;
                    TrackedTime = 0f;
                    TrackingFound?.Invoke();
                }
                else
                {
                    SmoothedRotation = Quaternion.Slerp(SmoothedRotation, RawRotation, 1f - Mathf.Exp(-rotationSharpness * dt));
                }

                TrackedTime += dt;
            }
            else if (IsTracked)
            {
                lostTimer += dt;
                if (lostTimer >= lostGraceTime)
                {
                    IsTracked = false;
                    TrackedTime = 0f;
                    TrackingLost?.Invoke();
                }
            }
        }

        Quaternion ReadRotation()
        {
            if (arCamera == null && Camera.main != null)
                arCamera = Camera.main.transform;

            if (relativeToCamera && arCamera != null)
                return Quaternion.Inverse(arCamera.rotation) * transform.rotation;
            return transform.rotation;
        }
    }
}
