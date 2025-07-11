// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using XanaduProject.ECSComponents.Tag;
using ZLinq;

namespace XanaduProject.ECSComponents.EntitySystem.Refresh_systems
{
    public class TriangleArrayRefresh : QuerySystem<ElementEcs, TriangleArrayEcs>
    {
        private const int mosaic_size = 500; // Number of squares in one row/column of the mosaic
        private const float cell_size = 32; // Pixel size of each square's side
        private readonly Vector2[] vertices;
        private readonly int[] indices;
        private readonly Color[] colors;

        public TriangleArrayRefresh()
        {
            Filter.AnyTags(Tags.Get<Dormant, SelectionFlag>());
            GenerateAlternatingColorPolygon(200, 100,out vertices, out indices, out colors);
        }

       /* private void generateMosaic(int mosaicSize, float cellSize)
        {
            // Number of vertices required
            vertices = new Vector2[(mosaicSize + 1) * (mosaicSize + 1)];
            indices = new int[mosaicSize * mosaicSize * 6]; // 2 triangles per square => 6 indices per square
            colors = new Color[vertices.Length];

            // Generate vertices and their associated colors
            int vertIndex = 0;
            for (int y = 0; y <= mosaicSize; y++)
            {
                for (int x = 0; x <= mosaicSize; x++)
                {
                    vertices[vertIndex] = new Vector2(x * cellSize, y * cellSize);
                    // Assign random colors to each vertex for a colorful mosaic
                    colors[vertIndex] = generateWinteryColor(x, y, mosaicSize);
                    vertIndex++;
                }
            }

            // Create indices for the triangles
            int idxIndex = 0;
            for (int y = 0; y < mosaicSize; y++)
            {
                for (int x = 0; x < mosaicSize; x++)
                {
                    // Top-left, top-right, bottom-left, bottom-right
                    int topLeft = y * (mosaicSize + 1) + x;
                    int topRight = topLeft + 1;
                    int bottomLeft = topLeft + (mosaicSize + 1);
                    int bottomRight = bottomLeft + 1;

                    // First triangle (top-left, bottom-left, top-right)
                    indices[idxIndex++] = topLeft;
                    indices[idxIndex++] = bottomLeft;
                    indices[idxIndex++] = topRight;

                    // Second triangle (bottom-left, bottom-right, top-right)
                    indices[idxIndex++] = bottomLeft;
                    indices[idxIndex++] = bottomRight;
                    indices[idxIndex++] = topRight;
                }
            }

        }*/
        private Color generateWinteryColor(int x, int y, int mosaicSize)
        {
            float randomValue = GD.Randf();

            // 75% chance for blue shades (dark/light)
            if (randomValue < 0.75f)
            {
                float blueTint = GD.Randf() * 0.5f + 0.5f; // Light to dark blue
                return new Color(0.2f * blueTint, 0.4f * blueTint, 0.8f * blueTint);
            }
            // 10% chance for green (tree-like)
            else if (randomValue < 0.85f)
            {
                return new Color(0.1f, 0.6f + GD.Randf() * 0.2f, 0.1f); // Light green
            }
            // 10% chance for red (holiday decorations)
            else if (randomValue < 0.95f)
            {
                return new Color(0.8f + GD.Randf() * 0.2f, 0.1f, 0.1f); // Red
            }
            // 5% chance for yellow (lightbulbs)
            else
            {
                return new Color(1.0f, 1.0f, 0.6f); // Warm yellow
            }
        }

        public static void GenerateAlternatingColorPolygon(
        float radius,
        int sides,
        out Vector2[] vertices,
        out int[] indices,
        out Color[] colors)
    {
        // Each triangle will use 3 unique vertices: center point, edge point A, edge point B.
        // Therefore, total vertices = 3 * sides.
        vertices = new Vector2[sides * 3];
        colors = new Color[sides * 3];
        indices = new int[sides * 3];

        // We can alternate between two colors for demonstration.
        // Of course, you could randomize or pick a more elaborate scheme.
        Color colorA = new Color(1.0f, 0.2f, 0.2f); // Reddish
        Color colorB = colorA.Darkened(0.3f); // Greenish

        // Calculate the angle step for each triangle around a circle
        float angleStep = Mathf.Tau / sides;

        // Build each triangle
        for (int i = 0; i < sides; i++)
        {
            // Indices for the triangle in the array
            int baseIndex = i * 3;

            // Calculate angles for two adjacent outer vertices
            float angleA = i * angleStep;
            float angleB = (i + 1) % sides * angleStep;

            // Center vertex (we give each triangle its own center vertex)
            vertices[baseIndex + 0] = Vector2.Zero;

            // Outer vertex 1
            vertices[baseIndex + 1] = new Vector2(
                radius * Mathf.Cos(angleA),
                radius * Mathf.Sin(angleA)
            );

            // Outer vertex 2
            vertices[baseIndex + 2] = new Vector2(
                radius * Mathf.Cos(angleB),
                radius * Mathf.Sin(angleB)
            );

            // Assign indices (each triangle has distinct indices)
            indices[baseIndex + 0] = baseIndex;
            indices[baseIndex + 1] = baseIndex + 1;
            indices[baseIndex + 2] = baseIndex + 2;

            // Choose one of two alternating colors
            // (All three vertices in the same triangle share the same color to keep a solid fill.)
            Color chosenColor = i % 2 == 0 ? colorA : colorB;

            colors[baseIndex + 0] = chosenColor;
            colors[baseIndex + 1] = chosenColor;
            colors[baseIndex + 2] = chosenColor;
        }
    }


        protected override void OnUpdate()
        {
            float time = Mathf.Sin(Time.GetTicksMsec() * 0.001f);
            foreach (var (elements, _, entities) in Query.Chunks)
            {
                for (int n = 0; n < entities.Length; n++)
                {
                    RenderingServer.CanvasItemAddTriangleArray(elements[n].Canvas, indices, vertices, colors);
                }
            }
        }
    }
}
