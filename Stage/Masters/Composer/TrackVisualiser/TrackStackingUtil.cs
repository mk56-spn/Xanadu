// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using System;
using System.Linq;

namespace XanaduProject.Stage.Masters.Composer.TrackVisualiser
{
    public static class TrackStackingUtil
    {
        public static void DrawStackedItems(Control control, float[] points, float spacing, Func<int, Color> colorFunc, Action<Vector2, Color> drawAction, int selectedIndex = -1, Action<Vector2> drawSelection = null)
        {
            var pointData = points
                .Select((p, i) => new { Value = p, Index = i })
                .ToArray();

            var pointGroups = pointData
                .GroupBy(p => p.Value)
                .OrderBy(g => g.Key);

            const float yStep = 15f;

            Vector2? selectionPos = null;

            foreach (var group in pointGroups)
            {
                var stackSize = group.Count();
                var startY = -(stackSize - 1) / 2.0f * yStep;

                int i = 0;
                foreach(var point in group)
                {
                    var x = point.Value * spacing;
                    var y = startY + i * yStep;
                    var pos = new Vector2(x, y);

                    drawAction(pos, colorFunc(point.Index));

                    if (point.Index == selectedIndex)
                    {
                        selectionPos = pos;
                    }
                    i++;
                }
            }

            if (selectionPos.HasValue && drawSelection != null)
            {
                drawSelection(selectionPos.Value);
            }
        }

        /// <summary>
        /// Draws a tree structure with nodes connected by lines
        /// </summary>
        /// <param name="control">The control to draw on</param>
        /// <param name="nodes">Array of node positions</param>
        /// <param name="parentIndices">Array indicating parent index for each node (-1 for root)</param>
        /// <param name="nodeColorFunc">Function to determine node color</param>
        /// <param name="drawNodeAction">Action to draw each node</param>
        /// <param name="drawConnectionAction">Action to draw connections between nodes</param>
        /// <param name="selectedNodeIndex">Index of selected node (-1 for none)</param>
        /// <param name="drawSelection">Action to draw selection indicator</param>
        public static void DrawTree(Control control, Vector2[] nodes, int[] parentIndices,
            Func<int, Color> nodeColorFunc, Action<Vector2, Color> drawNodeAction,
            Action<Vector2, Vector2> drawConnectionAction, int selectedNodeIndex = -1,
            Action<Vector2> drawSelection = null)
        {
            if (nodes.Length != parentIndices.Length)
                throw new ArgumentException("Nodes and parentIndices arrays must have the same length");

            Vector2? selectionPos = null;

            // First, draw all connections (edges)
            for (int i = 0; i < parentIndices.Length; i++)
            {
                int parentIndex = parentIndices[i];
                if (parentIndex >= 0 && parentIndex < nodes.Length)
                {
                    Vector2 parentPos = nodes[parentIndex];
                    Vector2 childPos = nodes[i];
                    drawConnectionAction(parentPos, childPos);
                }

                if (i == selectedNodeIndex)
                {
                    selectionPos = nodes[i];
                }
            }

            // Then, draw all nodes on top
            for (int i = 0; i < nodes.Length; i++)
            {
                Color nodeColor = nodeColorFunc(i);
                drawNodeAction(nodes[i], nodeColor);
            }

            // Finally, draw selection if needed
            if (selectionPos.HasValue && drawSelection != null)
            {
                drawSelection(selectionPos.Value);
            }
        }

        /// <summary>
        /// Draws a tree structure with automatic layout
        /// </summary>
        /// <param name="control">The control to draw on</param>
        /// <param name="nodeCount">Number of nodes in the tree</param>
        /// <param name="parentIndices">Array indicating parent index for each node (-1 for root)</param>
        /// <param name="horizontalSpacing">Horizontal spacing between levels</param>
        /// <param name="verticalSpacing">Vertical spacing between nodes on same level</param>
        /// <param name="nodeColorFunc">Function to determine node color</param>
        /// <param name="drawNodeAction">Action to draw each node</param>
        /// <param name="drawConnectionAction">Action to draw connections between nodes</param>
        /// <param name="selectedNodeIndex">Index of selected node (-1 for none)</param>
        /// <param name="drawSelection">Action to draw selection indicator</param>
        public static void DrawTreeLayout(Control control, int nodeCount, int[] parentIndices,
            float horizontalSpacing = 100f, float verticalSpacing = 50f,
            Func<int, Color> nodeColorFunc = null, Action<Vector2, Color> drawNodeAction = null,
            Action<Vector2, Vector2> drawConnectionAction = null, int selectedNodeIndex = -1,
            Action<Vector2> drawSelection = null)
        {
            if (nodeCount != parentIndices.Length)
                throw new ArgumentException("Node count and parentIndices array length must match");

            // Calculate node positions based on tree structure
            Vector2[] nodePositions = new Vector2[nodeCount];

            // Group nodes by level (depth from root)
            var nodesByLevel = new System.Collections.Generic.List<System.Collections.Generic.List<int>>();
            var nodeLevels = new int[nodeCount];

            // Find root nodes and determine levels
            for (int i = 0; i < nodeCount; i++)
            {
                if (parentIndices[i] == -1)
                {
                    nodeLevels[i] = 0;
                }
                else
                {
                    nodeLevels[i] = nodeLevels[parentIndices[i]] + 1;
                }
            }

            // Group by level
            var levelGroups = Enumerable.Range(0, nodeCount)
                .GroupBy(i => nodeLevels[i])
                .OrderBy(g => g.Key)
                .ToList();

            // Calculate positions
            foreach (var levelGroup in levelGroups)
            {
                int level = levelGroup.Key;
                var nodesAtLevel = levelGroup.ToList();
                int countAtLevel = nodesAtLevel.Count;

                float startY = -(countAtLevel - 1) * verticalSpacing / 2f;

                for (int i = 0; i < countAtLevel; i++)
                {
                    int nodeIndex = nodesAtLevel[i];
                    nodePositions[nodeIndex] = new Vector2(
                        level * horizontalSpacing,
                        startY + i * verticalSpacing
                    );
                }
            }

            // Use default drawing functions if not provided
            var defaultNodeColorFunc = nodeColorFunc ?? (_ => Colors.White);
            var defaultDrawNodeAction = drawNodeAction ?? ((pos, color) => {
                control.DrawCircle(pos, 5f, color);
            });
            var defaultDrawConnectionAction = drawConnectionAction ?? ((parent, child) => {
                control.DrawLine(parent, child, Colors.Gray, 2f);
            });

            DrawTree(control, nodePositions, parentIndices, defaultNodeColorFunc,
                defaultDrawNodeAction, defaultDrawConnectionAction, selectedNodeIndex, drawSelection);
        }
    }
}
