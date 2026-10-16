using UnityEngine;

namespace RVA
{
    public enum MazeTriggerKind
    {
        Hole,
        Exit,
        Checkpoint
    }

    /// <summary>
    /// Marks a trigger collider in the hidden physics maze. MazeBall reacts to these.
    /// Created by MazeBuilder; you do not add it by hand.
    /// </summary>
    public class MazeTrigger : MonoBehaviour
    {
        [SerializeField] MazeTriggerKind kind;
        [Tooltip("Order of this trigger among those of the same kind (reading order of the grid).")]
        [SerializeField] int index;
        [SerializeField] int row;
        [SerializeField] int col;

        public MazeTriggerKind Kind => kind;
        public int Index => index;
        public int Row => row;
        public int Col => col;

        internal void Init(MazeTriggerKind kind, int index, int row, int col)
        {
            this.kind = kind;
            this.index = index;
            this.row = row;
            this.col = col;
        }
    }
}
