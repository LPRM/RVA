using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RVA
{
    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        Won,
        TimeUp
    }

    public enum GameMode
    {
        Normal,
        TimeTrial
    }

    /// <summary>
    /// Owns the run: which level is loaded, Normal or Time Trial, the clock, pause, win and time-up.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class GameManager : MonoBehaviour
    {
        [Serializable]
        public class LevelEntry
        {
            public string name = "Level";
            public TextAsset layout;
            [Tooltip("Time Trial countdown in seconds, used only when the level file has no '@time' line.")]
            [Min(1f)] public float timeLimit = 90f;
        }

        [Header("References (found automatically when empty)")]
        [SerializeField] MazeBuilder builder;
        [SerializeField] MazeGravity gravity;

        [Header("Levels")]
        [SerializeField] List<LevelEntry> levels = new List<LevelEntry>();
        [Tooltip("Load level 0 as soon as the game starts. Turn off once the start menu exists. Also turn off Build On Start on the MazeBuilder, or the first frame builds twice.")]
        [SerializeField] bool loadFirstLevelOnStart = true;
        [SerializeField] GameMode mode = GameMode.Normal;

        [Header("Debug (until the UI exists)")]
        [Tooltip("P = pause, T = switch mode and restart, 1/2/3 = load level, Enter = restart, M = menu.")]
        [SerializeField] bool enableDebugKeys = true;
        [SerializeField] bool showDebugOverlay = true;
        [SerializeField] bool logEvents = true;

        MazeBall ball;

        public GameState State { get; private set; } = GameState.Menu;
        public GameMode Mode => mode;
        public bool IsPlaying => State == GameState.Playing;

        /// <summary>Index into Levels of the current level; -1 when none is loaded.</summary>
        public int LevelIndex { get; private set; } = -1;
        public int LevelCount => levels.Count;
        public string CurrentLevelName => LevelIndex >= 0 && LevelIndex < levels.Count ? levels[LevelIndex].name : "";

        /// <summary>Seconds played in this run (clock stopped while paused, lost or uncalibrated).</summary>
        public float Elapsed { get; private set; }

        /// <summary>Seconds left in Time Trial; equals the time limit in Normal.</summary>
        public float Remaining => Mathf.Max(0f, CurrentTimeLimit - Elapsed);

        /// <summary>Result of the last completed level: elapsed time in Normal, time left in Time Trial.</summary>
        public float LastResultSeconds { get; private set; }

        public event Action<GameState> StateChanged;
        public event Action<int> LevelStarted;
        public event Action<float> LevelCompleted;
        public event Action TimeExpired;

        /// <summary>The level file's "@time" value when it has one, otherwise the Inspector fallback.</summary>
        float CurrentTimeLimit
        {
            get
            {
                if (builder != null && builder.Layout != null && builder.Layout.TimeLimit > 0f)
                    return builder.Layout.TimeLimit;
                return LevelIndex >= 0 && LevelIndex < levels.Count ? levels[LevelIndex].timeLimit : 0f;
            }
        }

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

            if (builder == null)
                Debug.LogError("[GameManager] No MazeBuilder in the scene.", this);
            if (gravity == null)
                Debug.LogWarning("[GameManager] No MazeGravity in the scene; the ball cannot be paused.", this);
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
            HookBall(null);
        }

        void Start()
        {
            if (loadFirstLevelOnStart && levels.Count > 0)
            {
                LoadLevel(0);
            }
            else if (builder != null && builder.Ball != null)
            {
                // No level list: manage whatever MazeBuilder built on its own.
                OnBuilt(builder);
                StartRun();
            }
        }

        void Update()
        {
            if (enableDebugKeys)
                HandleDebugKeys();

            if (State != GameState.Playing)
                return;

            bool frozen = gravity != null && gravity.IsFrozen;
            if (frozen)
                return;

            Elapsed += Time.deltaTime;

            if (mode == GameMode.TimeTrial && Elapsed >= CurrentTimeLimit)
            {
                Elapsed = CurrentTimeLimit;
                SetState(GameState.TimeUp);
                if (logEvents)
                    Debug.Log($"[GameManager] Time up on {CurrentLevelName}.", this);
                TimeExpired?.Invoke();
            }
        }

        public void LoadLevel(int index)
        {
            if (builder == null)
                return;
            if (index < 0 || index >= levels.Count || levels[index].layout == null)
            {
                Debug.LogWarning($"[GameManager] No level {index + 1} (the list has {levels.Count}).", this);
                return;
            }

            LevelIndex = index;
            if (!builder.Build(levels[index].layout))
                return;

            StartRun();
            if (logEvents)
                Debug.Log($"[GameManager] Level {index + 1} ({CurrentLevelName}) started in {mode}.", this);
        }

        public void LoadNextLevel()
        {
            if (LevelIndex + 1 < levels.Count)
                LoadLevel(LevelIndex + 1);
            else
                ReturnToMenu();
        }

        public void Restart()
        {
            if (LevelIndex >= 0)
                LoadLevel(LevelIndex);
            else if (builder != null && builder.BuildFromInspector())
                StartRun();
        }

        public void ReturnToMenu()
        {
            if (builder != null)
                builder.Clear();
            HookBall(null);
            LevelIndex = -1;
            Elapsed = 0f;
            SetState(GameState.Menu);
        }

        /// <summary>Takes effect on the next level load or restart.</summary>
        public void SetMode(GameMode newMode)
        {
            mode = newMode;
        }

        public void Pause()
        {
            if (State == GameState.Playing)
                SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State == GameState.Paused)
                SetState(GameState.Playing);
        }

        public void TogglePause()
        {
            if (State == GameState.Playing)
                Pause();
            else if (State == GameState.Paused)
                Resume();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int minutes = (int)(seconds / 60f);
            float rest = seconds - minutes * 60f;
            return $"{minutes}:{rest:00.0}";
        }


        void StartRun()
        {
            Elapsed = 0f;
            SetState(GameState.Playing);
            LevelStarted?.Invoke(LevelIndex);
        }

        void SetState(GameState next)
        {
            GameState previous = State;
            State = next;

            if (gravity != null)
                gravity.SetPaused(next != GameState.Playing);

            if (previous != next)
                StateChanged?.Invoke(next);
        }

        void OnBuilt(MazeBuilder built)
        {
            HookBall(built.Ball);
        }

        void HookBall(MazeBall newBall)
        {
            if (ball != null)
                ball.TriggerEntered -= OnBallTrigger;
            ball = newBall;
            if (ball != null)
                ball.TriggerEntered += OnBallTrigger;
        }

        void OnBallTrigger(MazeBall sender, MazeTrigger trigger)
        {
            if (trigger.Kind != MazeTriggerKind.Exit || State != GameState.Playing)
                return;

            LastResultSeconds = mode == GameMode.TimeTrial ? Remaining : Elapsed;
            SetState(GameState.Won);

            if (logEvents)
            {
                string what = mode == GameMode.TimeTrial ? "time left" : "time";
                Debug.Log($"[GameManager] {CurrentLevelName} complete, {what} {FormatTime(LastResultSeconds)}.", this);
            }
            LevelCompleted?.Invoke(LastResultSeconds);
        }

        void HandleDebugKeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.pKey.wasPressedThisFrame)
                TogglePause();
            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                Restart();
            if (keyboard.mKey.wasPressedThisFrame)
                ReturnToMenu();
            if (keyboard.tKey.wasPressedThisFrame)
            {
                SetMode(mode == GameMode.Normal ? GameMode.TimeTrial : GameMode.Normal);
                Restart();
            }
            if (keyboard.digit1Key.wasPressedThisFrame) LoadLevel(0);
            if (keyboard.digit2Key.wasPressedThisFrame) LoadLevel(1);
            if (keyboard.digit3Key.wasPressedThisFrame) LoadLevel(2);
        }

        void OnGUI()
        {
            if (!showDebugOverlay)
                return;

            string level = LevelIndex >= 0 ? $"{LevelIndex + 1}/{levels.Count} {CurrentLevelName}" : "nenhum";
            string time = mode == GameMode.TimeTrial ? FormatTime(Remaining) : FormatTime(Elapsed);
            string state;
            switch (State)
            {
                case GameState.Menu: state = "Menu"; break;
                case GameState.Playing: state = "Playing"; break;
                case GameState.Paused: state = "Paused"; break;
                case GameState.Won: state = $"Won  {FormatTime(LastResultSeconds)}"; break;
                case GameState.TimeUp: state = "TimeUp"; break;
                default: state = State.ToString(); break;
            }

            string text =
                $"Nível: {level}   [1 2 3 changes levels]\n" +
                $"Modo: {(mode == GameMode.TimeTrial ? "Time Trial" : "Normal")}   [T changes mode]\n" +
                $"Estado: {state}   [P Pause]  [Enter Restart]  [M Menu]\n" +
                $"Tempo: {time}";

            // Same scaling as the MazeGravity overlay, placed just below it.
            float scale = Mathf.Max(1f, Screen.height / 540f);
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.Box(new Rect(8f, 112f, 360f, 78f), GUIContent.none);
            GUI.Label(new Rect(16f, 116f, 350f, 72f), text);
            GUI.matrix = previousMatrix;
        }
    }
}
