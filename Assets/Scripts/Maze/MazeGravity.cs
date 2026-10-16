using UnityEngine;
using UnityEngine.InputSystem;

namespace RVA
{
    /// <summary>
    /// Turns the tilt of the marker (or the arrow keys) into the direction of gravity in the hidden
    /// physics maze. The physics maze never moves; only gravity does.
    ///
    /// Tilt is measured against the calibrated pose: hold the marker "level" and press Calibrate (C).
    /// It is limited to maxTiltDegrees, small tilts are ignored (dead zone) and the ball freezes while
    /// the marker is lost.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class MazeGravity : MonoBehaviour
    {
        public enum TiltSource
        {
            Marker,
            Keyboard
        }

        [Header("References (found automatically when empty)")]
        [SerializeField] MazeTracker tracker;
        [SerializeField] MazeBuilder builder;

        [Header("Gravity")]
        [Tooltip("Strength of gravity in the physics maze, in cells per second squared.")]
        [SerializeField, Min(0f)] float gravityStrength = 15f;
        [Tooltip("The tilt is limited to this angle.")]
        [SerializeField, Range(5f, 60f)] float maxTiltDegrees = 30f;
        [Tooltip("Tilts smaller than this are ignored, so marker jitter does not move the ball.")]
        [SerializeField, Range(0f, 10f)] float deadZoneDegrees = 2f;
        [Tooltip("Multiplies the marker's tilt. 2 = half the hand movement for the same effect.")]
        [SerializeField, Range(0.5f, 3f)] float tiltSensitivity = 1f;
        [Tooltip("How quickly gravity follows the tilt. Lower is smoother.")]
        [SerializeField, Min(0.1f)] float gravitySharpness = 8f;
        [Tooltip("Flip left/right marker tilt (e.g. a mirrored laptop webcam). Keyboard tilt is not affected.")]
        [SerializeField] bool invertTiltX;
        [Tooltip("Flip forward/back marker tilt. Keyboard tilt is not affected.")]
        [SerializeField] bool invertTiltZ;

        [Header("Calibration")]
        [Tooltip("Calibrate automatically the first time the marker has been seen for a moment.")]
        [SerializeField] bool autoCalibrateOnFirstTracking = true;
        [SerializeField, Min(0f)] float autoCalibrateDelay = 0.5f;

        [Header("Keyboard tilt and debug keys (for testing)")]
        [Tooltip("In the Editor, start with the arrow keys instead of the marker (K switches).")]
        [SerializeField] bool startWithKeyboardInEditor = true;
        [Tooltip("How fast the keyboard tilt reaches its maximum, per second.")]
        [SerializeField, Min(0.1f)] float keyboardTiltSpeed = 3f;
        [Tooltip("K = keyboard/marker, C = calibrate, R = respawn ball, comma/period = rotate maze.")]
        [SerializeField] bool enableDebugKeys = true;
        [SerializeField] bool showDebugOverlay = true;

        Quaternion calibratedRotation = Quaternion.identity;
        Vector3 gravityDirection = Vector3.down;
        Vector2 keyboardTilt;

        public TiltSource Source { get; private set; }
        public bool IsCalibrated { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>True while the ball is held still (paused, marker lost or not yet calibrated).</summary>
        public bool IsFrozen { get; private set; }

        /// <summary>Rotation of the visible maze around the marker's up axis (the rotation slider), in degrees.</summary>
        public float RotationOffset { get; private set; }

        /// <summary>Current tilt in degrees, after the dead zone and the limit.</summary>
        public float TiltDegrees => Vector3.Angle(Vector3.down, gravityDirection);

        /// <summary>Unit direction of gravity in the physics maze's local space.</summary>
        public Vector3 GravityDirectionLocal => gravityDirection;

        public float GravityStrength
        {
            get => gravityStrength;
            set => gravityStrength = Mathf.Max(0f, value);
        }

        public MazeTracker Tracker => tracker;

        void Awake()
        {
            if (builder == null)
                builder = GetComponent<MazeBuilder>();
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();
            if (tracker == null)
                tracker = FindAnyObjectByType<MazeTracker>();

            bool noMarker = tracker == null || !MazeTracker.VuforiaAvailable;
            bool keyboardFirst = Application.isEditor && startWithKeyboardInEditor;
            Source = noMarker || keyboardFirst ? TiltSource.Keyboard : TiltSource.Marker;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (enableDebugKeys)
                HandleDebugKeys();

            bool tracked = tracker != null && tracker.IsTracked;
            if (Source == TiltSource.Marker && tracked && !IsCalibrated
                && autoCalibrateOnFirstTracking && tracker.TrackedTime >= autoCalibrateDelay)
            {
                Calibrate();
            }

            Vector3 target = Source == TiltSource.Keyboard ? KeyboardDirection(dt) : MarkerDirection();
            gravityDirection = Vector3.Slerp(gravityDirection, target, 1f - Mathf.Exp(-gravitySharpness * dt));

            IsFrozen = IsPaused || (Source == TiltSource.Marker && (!tracked || !IsCalibrated));

            MazeBall ball = builder != null ? builder.Ball : null;
            if (ball != null)
            {
                ball.SetFrozen(IsFrozen);
                Transform root = builder.PhysicsRoot;
                Vector3 worldDirection = root != null ? root.TransformDirection(gravityDirection) : gravityDirection;
                ball.Gravity = worldDirection * gravityStrength;
            }
        }

        /// <summary>Makes the marker's current pose the "level" pose. Also centres the keyboard tilt.</summary>
        public void Calibrate()
        {
            keyboardTilt = Vector2.zero;

            if (tracker != null && tracker.IsTracked)
            {
                calibratedRotation = tracker.SmoothedRotation;
                IsCalibrated = true;
                Debug.Log("[MazeGravity] Calibrated: the marker's current pose is now level.", this);
            }
            else if (Source == TiltSource.Marker)
            {
                Debug.Log("[MazeGravity] Cannot calibrate: the marker is not being tracked.", this);
            }
        }

        /// <summary>Rotates the visible maze on the marker (for the rotation slider).</summary>
        public void SetRotationOffset(float degrees)
        {
            RotationOffset = Mathf.Repeat(degrees, 360f);
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
        }

        public void SetSource(TiltSource source)
        {
            Source = source;
            keyboardTilt = Vector2.zero;
        }

        public void ToggleSource()
        {
            SetSource(Source == TiltSource.Marker ? TiltSource.Keyboard : TiltSource.Marker);
        }

        Vector3 MarkerDirection()
        {
            if (tracker == null || !tracker.IsTracked || !IsCalibrated)
                return gravityDirection;

            // "Down" as it was when calibrated, expressed in the maze's current frame.
            Vector3 down = calibratedRotation * Vector3.down;
            Vector3 markerLocal = Quaternion.Inverse(tracker.SmoothedRotation) * down;

            // Flip in the marker's frame, before the rotation offset, so the slider doesn't swap the axes.
            if (invertTiltX) markerLocal.x = -markerLocal.x;
            if (invertTiltZ) markerLocal.z = -markerLocal.z;

            Vector3 local = Quaternion.Euler(0f, -RotationOffset, 0f) * markerLocal;
            return LimitTilt(local);
        }

        Vector3 KeyboardDirection(float dt)
        {
            keyboardTilt = Vector2.MoveTowards(keyboardTilt, ReadArrowKeys(), keyboardTiltSpeed * dt);
            Vector2 tilt = Vector2.ClampMagnitude(keyboardTilt, 1f);
            if (tilt.sqrMagnitude < 1e-6f)
                return Vector3.down;

            // The arrows are relative to the marker, so undo the maze's rotation offset.
            Vector3 planar = Quaternion.Euler(0f, -RotationOffset, 0f) * new Vector3(tilt.x, 0f, tilt.y).normalized;
            float angle = tilt.magnitude * maxTiltDegrees * Mathf.Deg2Rad;
            return Vector3.down * Mathf.Cos(angle) + planar * Mathf.Sin(angle);
        }

        Vector3 LimitTilt(Vector3 localDirection)
        {
            Vector3 planar = new Vector3(localDirection.x, 0f, localDirection.z);
            if (planar.sqrMagnitude < 1e-8f)
                return Vector3.down;
            planar.Normalize();

            float angle = Vector3.Angle(Vector3.down, localDirection) * tiltSensitivity - deadZoneDegrees;
            angle = Mathf.Clamp(angle, 0f, maxTiltDegrees) * Mathf.Deg2Rad;
            return Vector3.down * Mathf.Cos(angle) + planar * Mathf.Sin(angle);
        }

        static Vector2 ReadArrowKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            float x = (keyboard.rightArrowKey.isPressed ? 1f : 0f) - (keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float y = (keyboard.upArrowKey.isPressed ? 1f : 0f) - (keyboard.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }

        void HandleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.kKey.wasPressedThisFrame)
                ToggleSource();
            if (keyboard.cKey.wasPressedThisFrame)
                Calibrate();
            if (keyboard.rKey.wasPressedThisFrame && builder != null && builder.Ball != null)
                builder.Ball.RespawnAtSpawnPoint();
            if (keyboard.commaKey.wasPressedThisFrame)
                SetRotationOffset(RotationOffset - 15f);
            if (keyboard.periodKey.wasPressedThisFrame)
                SetRotationOffset(RotationOffset + 15f);
        }

        void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            string tracking = tracker == null
                ? "sem MazeTracker"
                : (tracker.IsTracked ? "detetado" : "perdido") + " (" + tracker.StatusText + ")";

            string text =
                $"Inclinação: {(Source == TiltSource.Keyboard ? "TECLADO (setas)" : "MARCADOR")}   [K muda]\n" +
                $"Marcador: {tracking}\n" +
                $"Calibrado: {(IsCalibrated ? "sim" : "não")}   [C calibra]\n" +
                $"Ângulo: {TiltDegrees:0.0}°   Rotação: {RotationOffset:0}°   [ , . rodam]\n" +
                $"Bola: {(IsFrozen ? "congelada" : "livre")}   [R reinicia]   [V vista física]";

            // Scale with the screen so the text stays readable at high resolutions (e.g. a QHD Game view).
            float scale = Mathf.Max(1f, Screen.height / 540f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.Box(new Rect(8f, 8f, 360f, 96f), GUIContent.none);
            GUI.Label(new Rect(16f, 12f, 350f, 90f), text);
            GUI.matrix = previousMatrix;
        }
    }
}
