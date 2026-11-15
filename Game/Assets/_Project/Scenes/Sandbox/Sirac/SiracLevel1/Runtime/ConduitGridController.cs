using System.Collections.Generic;
using TheLightenRoad.Input;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TheLightenRoad.Sirac.Level1
{
    /// <summary>
    /// Evaluates BFS connectivity from SourceCell to OutputCell across rotatable ConduitNode2D tiles,
    /// supports random scrambles, and slides open a secret panel on completion.
    /// </summary>
    public sealed class ConduitGridController : MonoBehaviour
    {
        public List<ConduitNode2D> Nodes = new List<ConduitNode2D>();
        public Vector2Int SourceCell;
        public Vector2Int OutputCell;
        public AdvancedEmberVein PowerSourceVein;
        public SecretSlidingPanel2D TargetSecretPanel;

        private static readonly Vector2Int[] DirOffsets =
        {
            new Vector2Int(0, 1),  // North
            new Vector2Int(1, 0),  // East
            new Vector2Int(0, -1), // South
            new Vector2Int(-1, 0)  // West
        };

        private readonly Dictionary<Vector2Int, ConduitNode2D> lookup = new Dictionary<Vector2Int, ConduitNode2D>(16);
        private readonly HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        private readonly Queue<ConduitNode2D> queue = new Queue<ConduitNode2D>(16);

        public bool IsOutputPowered { get; private set; }
        public int LastSeed { get; private set; }

        private void Awake()
        {
            RegisterNodes();
            if (PowerSourceVein != null)
            {
                PowerSourceVein.OnVeinStateChanged -= HandleVeinStateChanged;
                PowerSourceVein.OnVeinStateChanged += HandleVeinStateChanged;
            }
            EvaluateNetwork();
        }

        private void OnDestroy()
        {
            if (PowerSourceVein != null)
            {
                PowerSourceVein.OnVeinStateChanged -= HandleVeinStateChanged;
            }
        }

        private void HandleVeinStateChanged(int _) => EvaluateNetwork();

        public void RegisterNodes()
        {
            foreach (var n in Nodes)
            {
                if (n != null) n.Grid = this;
            }
        }

        public void RandomizeLayout(int seed = 42)
        {
            LastSeed = seed;
            RegisterNodes();
            var rng = new System.Random(seed);
            bool anyScrambled = false;

            foreach (var node in Nodes)
            {
                if (node == null || node.Locked || node.Shape == ConduitPipeShape.Cross) continue;
                int offset = rng.Next(1, 4); // 1, 2, or 3 quarter turns off solved rotation
                node.SetRotationSteps((node.SolvedRotationSteps + offset) % 4);
                anyScrambled = true;
            }

            if (!anyScrambled && Nodes.Count > 0 && !Nodes[0].Locked)
            {
                Nodes[0].SetRotationSteps((Nodes[0].SolvedRotationSteps + 1) % 4);
            }

            EvaluateNetwork();
        }

        public void SolveAllNodes()
        {
            RegisterNodes();
            foreach (var node in Nodes)
            {
                if (node == null) continue;
                node.SetRotationSteps(node.SolvedRotationSteps);
            }
            EvaluateNetwork();
        }

        public bool EvaluateNetwork()
        {
            RegisterNodes();
            lookup.Clear();
            foreach (var node in Nodes)
            {
                if (node == null) continue;
                node.IsPowered = false;
                lookup[node.GridPos] = node;
            }

            bool hasVeinPower = PowerSourceVein == null || PowerSourceVein.VeinStateValue == 1;
            bool outputReached = false;

            if (hasVeinPower && lookup.TryGetValue(SourceCell, out var startNode))
            {
                visited.Clear();
                queue.Clear();
                visited.Add(SourceCell);
                startNode.IsPowered = true;
                queue.Enqueue(startNode);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    if (current.GridPos == OutputCell) outputReached = true;

                    for (int dir = 0; dir < 4; dir++)
                    {
                        if (!current.HasPort(dir)) continue;
                        Vector2Int nextPos = current.GridPos + DirOffsets[dir];
                        if (visited.Contains(nextPos)) continue;
                        if (!lookup.TryGetValue(nextPos, out var neighbor)) continue;
                        int opposite = (dir + 2) % 4;
                        if (!neighbor.HasPort(opposite)) continue;

                        visited.Add(nextPos);
                        neighbor.IsPowered = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            foreach (var node in Nodes)
            {
                if (node != null) node.RefreshPowerVisual();
            }

            IsOutputPowered = outputReached;
            if (TargetSecretPanel != null)
            {
                TargetSecretPanel.SetOpen(IsOutputPowered);
            }
            return IsOutputPowered;
        }
    }
}
