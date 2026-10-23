using System;
using System.Collections.Generic;
using System.Text;

namespace RVA
{
    /// <summary>What occupies one cell of the maze grid.</summary>
    public enum MazeCell
    {
        Floor,
        Wall,
        Hole,
        Start,
        Exit,
        Checkpoint,
        RedGate,
        BlueGate
    }

    /// <summary>
    /// A maze level read from a text grid. Row 0 is the first line of the text (the far edge of the
    /// marker) and column 0 is the left edge.
    ///
    ///   #  wall        .  floor        S  start (exactly one)    E  exit
    ///   O  hole        C  checkpoint   R  red gate               B  blue gate
    ///
    /// Lines starting with ';' are comments and blank lines are ignored. Rows shorter than the
    /// longest row are padded with walls.
    ///
    /// Lines starting with '@' are directives, placed anywhere before or after the grid:
    ///   @time 120   Time Trial countdown for this level, in seconds.
    /// </summary>
    public sealed class MazeLayout
    {
        readonly MazeCell[,] cells;
        readonly List<string> warnings;

        public int Rows { get; }
        public int Cols { get; }
        public (int row, int col) Start { get; }
        public IReadOnlyList<string> Warnings => warnings;

        public float TimeLimit { get; }

        public MazeCell this[int row, int col] => cells[row, col];

        MazeLayout(MazeCell[,] cells, (int row, int col) start, float timeLimit, List<string> warnings)
        {
            this.cells = cells;
            this.warnings = warnings;
            Rows = cells.GetLength(0);
            Cols = cells.GetLength(1);
            Start = start;
            TimeLimit = timeLimit;
        }

        public bool InBounds(int row, int col) => row >= 0 && row < Rows && col >= 0 && col < Cols;

        /// <summary>Cells of one type in reading order (left to right, top to bottom).</summary>
        public IEnumerable<(int row, int col)> CellsOfType(MazeCell type)
        {
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    if (cells[r, c] == type)
                        yield return (r, c);
        }

        public static MazeCell CharToCell(char ch, out bool known)
        {
            known = true;
            switch (ch)
            {
                case '#': return MazeCell.Wall;
                case '.': return MazeCell.Floor;
                case ' ': return MazeCell.Floor;
                case 'S': case 's': return MazeCell.Start;
                case 'E': case 'e': return MazeCell.Exit;
                case 'O': case 'o': return MazeCell.Hole;
                case 'C': case 'c': return MazeCell.Checkpoint;
                case 'R': case 'r': return MazeCell.RedGate;
                case 'B': case 'b': return MazeCell.BlueGate;
                default:
                    known = false;
                    return MazeCell.Floor;
            }
        }

        public static char CellToChar(MazeCell cell)
        {
            switch (cell)
            {
                case MazeCell.Wall: return '#';
                case MazeCell.Start: return 'S';
                case MazeCell.Exit: return 'E';
                case MazeCell.Hole: return 'O';
                case MazeCell.Checkpoint: return 'C';
                case MazeCell.RedGate: return 'R';
                case MazeCell.BlueGate: return 'B';
                default: return '.';
            }
        }

        /// <summary>Parses a grid. Throws FormatException if no maze can be built from it.</summary>
        public static MazeLayout Parse(string text)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));

            var lines = new List<string>();
            var warnings = new List<string>();
            float timeLimit = 0f;

            foreach (string raw in text.TrimStart('﻿').Split('\n'))
            {
                string line = raw.TrimEnd('\r', ' ', '\t');
                string trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed[0] == ';')
                    continue;
                if (trimmed[0] == '@')
                {
                    ParseDirective(trimmed, ref timeLimit, warnings);
                    continue;
                }
                lines.Add(line);
            }

            if (lines.Count == 0)
                throw new FormatException("The maze layout is empty.");

            int rows = lines.Count;
            int cols = 0;
            foreach (string line in lines)
                cols = Math.Max(cols, line.Length);

            var cells = new MazeCell[rows, cols];
            (int row, int col)? start = null;

            for (int r = 0; r < rows; r++)
            {
                string line = lines[r];
                if (line.Length < cols)
                    warnings.Add($"Row {r + 1} is shorter than the others; padded with walls.");

                for (int c = 0; c < cols; c++)
                {
                    if (c >= line.Length)
                    {
                        cells[r, c] = MazeCell.Wall;
                        continue;
                    }

                    MazeCell cell = CharToCell(line[c], out bool known);
                    if (!known)
                        warnings.Add($"Unknown character '{line[c]}' at row {r + 1}, column {c + 1}; treated as floor.");

                    if (cell == MazeCell.Start)
                    {
                        if (start.HasValue)
                        {
                            warnings.Add($"More than one start (S); the one at row {r + 1}, column {c + 1} is treated as floor.");
                            cell = MazeCell.Floor;
                        }
                        else
                        {
                            start = (r, c);
                        }
                    }

                    cells[r, c] = cell;
                }
            }

            if (!start.HasValue)
            {
                for (int r = 0; r < rows && !start.HasValue; r++)
                    for (int c = 0; c < cols && !start.HasValue; c++)
                        if (cells[r, c] == MazeCell.Floor)
                            start = (r, c);

                if (!start.HasValue)
                    throw new FormatException("The maze has no start (S) and no floor cell to start on.");

                warnings.Add($"The maze has no start (S); using row {start.Value.row + 1}, column {start.Value.col + 1}.");
                cells[start.Value.row, start.Value.col] = MazeCell.Start;
            }

            var layout = new MazeLayout(cells, start.Value, timeLimit, warnings);
            bool hasExit = false;
            foreach (var _ in layout.CellsOfType(MazeCell.Exit))
            {
                hasExit = true;
                break;
            }
            if (!hasExit)
                warnings.Add("The maze has no exit (E).");

            return layout;
        }

        /// <summary>Handles one "@name value" line.</summary>
        static void ParseDirective(string line, ref float timeLimit, List<string> warnings)
        {
            string[] parts = line.Substring(1).Split(new[] { ' ', '\t', ':', '=' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                warnings.Add("Empty directive line ('@' with nothing after it); ignored.");
                return;
            }

            string name = parts[0].ToLowerInvariant();
            string value = parts.Length > 1 ? parts[1] : "";

            switch (name)
            {
                case "time":
                    if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float seconds) && seconds > 0f)
                        timeLimit = seconds;
                    else
                        warnings.Add($"'@time' needs a number of seconds greater than 0, got '{value}'; ignored.");
                    break;
                default:
                    warnings.Add($"Unknown directive '@{parts[0]}'; ignored.");
                    break;
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder(Rows * (Cols + 1));
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                    sb.Append(CellToChar(cells[r, c]));
                if (r < Rows - 1)
                    sb.Append('\n');
            }
            return sb.ToString();
        }
    }
}
