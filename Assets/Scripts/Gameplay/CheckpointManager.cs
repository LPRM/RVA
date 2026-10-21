using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RVA
{
    public class CheckpointManager : MonoBehaviour
    {
        /// <summary>
        /// One entry of the undo history: where the ball respawns and which checkpoint that is
        /// (null = the level's start). Gate states can be added here later if we decide to restore them.
        /// </summary>
        struct Snapshot
        {
            public Vector3 position;
            public MazeTrigger checkpoint;
        }

        [SerializeField] MazeBuilder builder;

        [SerializeField] Color activeCheckpointColor = new Color(1f, 0.4f, 0.7f);
        [Tooltip("Colour of a checkpoint the ball has not reached (same as Checkpoint Look in the MazeBuilder).")]
        [SerializeField] Color inactiveCheckpointColor = new Color(1f, 0.82f, 0.15f);
        [Tooltip("Within this distance (in cells) the ball counts as already being at a checkpoint, so Undo goes one further back.")]
        [SerializeField, Min(0.05f)] float atCheckpointDistance = 0.5f;
        [Tooltip("U = Undo, for testing until the UI button exists.")]
        [SerializeField] bool enableDebugKey = true;

        // The undo history. history[0] is always the start of the level and is never removed;
        // the last entry is the current respawn point.
        readonly List<Snapshot> history = new List<Snapshot>();
        MazeBall ball;

        /// <summary>How many times Undo can still go back.</summary>
        public int UndoCount => Mathf.Max(0, history.Count - 1);

        void Awake()
        {
            if (builder == null)
                builder = GetComponent<MazeBuilder>();
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();
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

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (enableDebugKey && keyboard != null && keyboard.uKey.wasPressedThisFrame)
                Undo();
        }

        void OnBuilt(MazeBuilder built)
        {
            ball = built.Ball;
            ball.TriggerEntered += OnBallTrigger;

            // New level: the history starts with only the start position.
            history.Clear();
            history.Add(new Snapshot { position = built.StartLocalPosition, checkpoint = null });
        }

        void OnBallTrigger(MazeBall ball, MazeTrigger trigger)
        {
            if (trigger.Kind != MazeTriggerKind.Checkpoint)
            {
                return;
            }

            // Don't save duplicate checkpoints
            if (InHistory(trigger))
            {
                return;
            }

            Vector3 position = trigger.transform.localPosition + Vector3.up * 0.01f;
            history.Add(new Snapshot { position = position, checkpoint = trigger });

            ball.SpawnPoint = position;
            Paint(trigger, activeCheckpointColor);
        }

        /// <summary>
        /// Like Ctrl+Z: the first Undo puts the ball back at the latest checkpoint it reached.
        /// Pressing it again while the ball is still there forgets that checkpoint and goes to the
        /// one before it, and so on down to the start.
        /// </summary>
        public void Undo()
        {
            if (ball == null || history.Count == 0)
            {
                return;
            }

            // Ball has moved away from the latest checkpoint: just go back to it and keep the history.
            // Ball is already there: forget that checkpoint and go one further back.
            Snapshot latest = history[history.Count - 1];
            bool atLatest = Vector3.Distance(ball.LocalPosition, latest.position) <= atCheckpointDistance;

            if (atLatest && history.Count > 1)
            {
                Snapshot removed = history[history.Count - 1];
                history.RemoveAt(history.Count - 1);

                // Only turn the disc back to yellow if that checkpoint is not still in the history.
                if (removed.checkpoint != null && !InHistory(removed.checkpoint))
                {
                    Paint(removed.checkpoint, inactiveCheckpointColor);
                }
            }

            Snapshot target = history[history.Count - 1];
            ball.SpawnPoint = target.position;
            ball.Respawn(target.position);
            Debug.Log($"[CheckpointManager] Undo: ball back at {(target.checkpoint == null ? "the start" : "checkpoint " + target.checkpoint.Index)}. {UndoCount} checkpoint(s) still saved.");
        }

        bool InHistory(MazeTrigger checkpoint)
        {
            foreach (Snapshot snapshot in history)
            {
                if (snapshot.checkpoint == checkpoint)
                {
                    return true;
                }
            }
            return false;
        }

        void Paint(MazeTrigger trigger, Color color)
        {
            string discName = $"Checkpoint {trigger.Row + 1},{trigger.Col + 1}";
            Transform disc = builder.VisualRoot.Find(discName);
            Transform debugDisc = builder.PhysicsRoot.Find("Debug renderers/" + discName);

            if (disc != null)
            {
                disc.GetComponent<Renderer>().material.color = color;
            }

            if (debugDisc != null)
            {
                debugDisc.GetComponent<Renderer>().material.color = color;
            }
        }
    }
}
