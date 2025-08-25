// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using Godot;
using System.Collections.Generic;
using Godot.Collections;

namespace XanaduProject.Factories
{
    /// <summary>
    /// Defines the types of meshes that can be created by the factory.
    /// Used for creating type-safe cache keys.
    /// </summary>
    public enum MeshType
    {
        Triangle,
        Heart,
        Crescent,
        Star,
        Circle,
    }

    public static class MeshFactory
    {
        // A dictionary is used for the cache. The key is an object that allows
        // for tuples with varying numbers of items, accommodating different mesh parameters.
        private static readonly System.Collections.Generic.Dictionary<object, ArrayMesh> s_mesh_cache = new();

        #region Public Mesh Creation Methods

        public static ArrayMesh CreateTriangle(float size)
        {
            var cacheKey = (MeshType.Triangle, size);
            if (s_mesh_cache.TryGetValue(cacheKey, out var cachedMesh))
            {
                return cachedMesh;
            }

            var vertices = new[]
            {
                new Vector3(0, size, 0),      // Top vertex
                new Vector3(-size, -size, 0), // Bottom left vertex
                new Vector3(size, -size, 0)   // Bottom right vertex
            };

            var arrayMesh = createMeshFromVertices(vertices);
            s_mesh_cache[cacheKey] = arrayMesh;
            return arrayMesh;
        }

        public static ArrayMesh CreateHeart(float size)
        {
            var cacheKey = (MeshType.Heart, size);
            if (s_mesh_cache.TryGetValue(cacheKey, out var cachedMesh))
            {
                return cachedMesh;
            }

            const int num_segments = 64;
            var outlineVertices = new Vector3[num_segments];

            for (int i = 0; i < num_segments; i++)
            {
                float t = Mathf.Pi * 2 * i / num_segments;
                float x = 16 * Mathf.Pow(Mathf.Sin(t), 3);
                float y = 13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) - 2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                // Invert the Y-axis and normalize to make the heart point upwards
                outlineVertices[i] = new Vector3(x / 16f, -y / 16f, 0) * size;
            }

            var meshVertices = triangulateFromCenter(outlineVertices);
            var arrayMesh = createMeshFromVertices(meshVertices);
            s_mesh_cache[cacheKey] = arrayMesh;
            return arrayMesh;
        }

            public static ArrayMesh CreateCrescent(float size, float thickness)
            {
                var cacheKey = (MeshType.Crescent, size, thickness);
                if (s_mesh_cache.TryGetValue(cacheKey, out var cachedMesh))
                {
                    return cachedMesh;
                }

                var outlinePoints = new List<Vector2>();
                const int segments = 32;

                thickness = Mathf.Clamp(thickness, 0.1f, 1.0f);

                // Outer arc
                for (int i = 0; i <= segments; i++)
                {
                    float t = (Mathf.Pi * i / segments) - Mathf.Pi / 2;
                    outlinePoints.Add(new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * size);
                }

                // Inner arc
                float innerOffset = size * (1f - thickness);
                float innerRadius = size - innerOffset;
                for (int i = segments; i >= 0; i--)
                {
                    float t = (Mathf.Pi * i / segments) - Mathf.Pi / 2;
                    outlinePoints.Add(new Vector2(Mathf.Cos(t) * innerRadius + innerOffset, Mathf.Sin(t) * innerRadius));
                }

                int[]? indices = Geometry2D.TriangulatePolygon(outlinePoints.ToArray());
                var vertices = new Vector3[outlinePoints.Count];
                for(int i = 0; i < outlinePoints.Count; i++)
                {
                    vertices[i] = new Vector3(outlinePoints[i].X, outlinePoints[i].Y, 0);
                }

                var arrayMesh = createMeshFromVertices(vertices, indices);
                s_mesh_cache[cacheKey] = arrayMesh;
                return arrayMesh;
            }

        public static ArrayMesh CreateStar(int numPoints, float size, float insetRatio)
        {
            if (numPoints < 2) numPoints = 2; // Ensure at least 2 points
            var cacheKey = (MeshType.Star, numPoints, size, insetRatio);
            if (s_mesh_cache.TryGetValue(cacheKey, out var cachedMesh))
            {
                return cachedMesh;
            }

            int totalPoints = numPoints * 2;
            var outlineVertices = new Vector3[totalPoints];
            float innerRadius = size * Mathf.Clamp(insetRatio, 0.1f, 1.0f);

            for (int i = 0; i < totalPoints; i++)
            {
                float radius = (i % 2 == 0) ? size : innerRadius;
                float angle = i * Mathf.Pi / numPoints;
                outlineVertices[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);
            }

            var meshVertices = triangulateFromCenter(outlineVertices);
            var arrayMesh = createMeshFromVertices(meshVertices);
            s_mesh_cache[cacheKey] = arrayMesh;
            return arrayMesh;
        }

        public static ArrayMesh CreateCircle(float size, int numSegments = 32)
        {
            if (numSegments < 3) numSegments = 3; // A circle needs at least 3 segments.

            var cacheKey = (MeshType.Circle, size, numSegments);
            if (s_mesh_cache.TryGetValue(cacheKey, out var cachedMesh))
            {
                return cachedMesh;
            }

            var outlineVertices = new Vector3[numSegments];
            for (int i = 0; i < numSegments; i++)
            {
                float angle = i * 2.0f * Mathf.Pi / numSegments;
                outlineVertices[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * size;
            }

            var meshVertices = triangulateFromCenter(outlineVertices);
            var arrayMesh = createMeshFromVertices(meshVertices);
            s_mesh_cache[cacheKey] = arrayMesh;
            return arrayMesh;
        }
        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Creates a list of triangles that form a fan from a center point to an outline.
        /// </summary>
        /// <param name="outlineVertices">The vertices defining the shape's perimeter.</param>
        /// <returns>A vertex array defining the triangles.</returns>
        private static Vector3[] triangulateFromCenter(Vector3[] outlineVertices)
        {
            int vertexCount = outlineVertices.Length;
            var meshVertices = new Vector3[vertexCount * 3];
            var center = Vector3.Zero;

            for (int i = 0; i < vertexCount; i++)
            {
                meshVertices[i * 3] = center;
                meshVertices[i * 3 + 1] = outlineVertices[i];
                meshVertices[i * 3 + 2] = outlineVertices[(i + 1) % vertexCount];
            }

            return meshVertices;
        }

        /// <summary>
        /// Creates an ArrayMesh and adds a single surface from the provided vertex and index data.
        /// </summary>
        /// <param name="vertices">The vertex data for the mesh.</param>
        /// <param name="indices">Optional index data for indexed drawing.</param>
        /// <returns>A new ArrayMesh with one surface.</returns>
        private static ArrayMesh createMeshFromVertices(Vector3[] vertices, int[]? indices = null)
        {
            var arrayMesh = new ArrayMesh();
            var arrays = new Array();
            arrays.Resize((int)Mesh.ArrayType.Max);
            arrays[(int)Mesh.ArrayType.Vertex] = vertices;


            if (indices != null)
                arrays[(int)Mesh.ArrayType.Index] = indices;


            arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
            return arrayMesh;
        }

        #endregion

        /// <summary>
        /// Clears the mesh cache.
        /// </summary>
        public static void ClearCache()
        {
            s_mesh_cache.Clear();
        }
    }
}
