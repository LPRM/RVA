#if RVA_VUFORIA
using Vuforia;
#endif
using UnityEngine;

namespace RVA
{
    public class CheckpointManager : MonoBehaviour
    {
        [SerializeField] MazeBuilder builder;

        [SerializeField] Color activeCheckpointColor = new Color(1f, 0.4f, 0.7f);
        [SerializeField] bool startWithKeyboardInEditor = true;


        void Awake()
        {
            if (builder == null)
                builder = GetComponent<MazeBuilder>();
            if (builder == null)
                builder = FindAnyObjectByType<MazeBuilder>();
        }

        void OnBuilt(MazeBuilder built)
        {
            MazeBall ball = built.Ball;
            ball.TriggerEntered += OnBallTrigger;
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

        void OnBallTrigger(MazeBall ball, MazeTrigger trigger)
        {
            if(trigger.Kind != MazeTriggerKind.Checkpoint)
            {
                return;
            }

            ball.SpawnPoint = trigger.transform.localPosition + Vector3.up * 0.01f;
            Paint(trigger);
        }

        void Paint(MazeTrigger trigger)
        {
            string discName = $"Checkpoint {trigger.Row + 1},{trigger.Col + 1}";
            Transform disc = builder.VisualRoot.Find(discName);
            Transform debugDisc = builder.PhysicsRoot.Find("Debug renderers/" + discName);

            if (disc != null)
            {
                disc.GetComponent<Renderer>().material.color = activeCheckpointColor;
            }

            if(debugDisc != null)
            {
                debugDisc.GetComponent<Renderer>().material.color = activeCheckpointColor;
            }
        }
    }
}