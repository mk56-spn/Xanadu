using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;

namespace Xanadu.Model.Animation;

/// <summary>Parts drawn as meshes bent by their bones: a grid over the art, or a stored skin, weighted once and bent every frame.</summary>
public sealed partial class XifDrawer
{
    private ConditionalWeakTable<XifGeometry, MeshCache> _bent = new();

    /// <summary>Where each skinned part was last drawn, by part index, for an editor to outline.</summary>
    private readonly Dictionary<int, (Vector2[] Points, int[] Triangles)> _drawnMeshes = new();

    /// <summary>
    /// The target-space points and triangles a skinned part was last drawn
    /// with; false when it was not drawn bent.
    /// </summary>
    public bool TryMesh(int attachment, out Vector2[] points, out int[] triangles)
    {
        if (_drawnMeshes.TryGetValue(attachment, out (Vector2[] Points, int[] Triangles) drawn) && drawn.Points.Length > 0)
        {
            points = drawn.Points;
            triangles = drawn.Triangles;
            return true;
        }
        points = Array.Empty<Vector2>();
        triangles = Array.Empty<int>();
        return false;
    }

    /// <summary>The part's art bent by its bones, in target space; null when no bone reaches it.</summary>
    private MeshCache? Bend(RigPackage package, RigPose pose, RigAttachment part, RigMesh mesh, XifGeometry geometry,
        ArtMesh art, Transform2D[] bones, Transform2D placed, Transform2D[] restBones, Transform2D restAttachment,
        Transform2D placement)
    {
        List<int> influences = RigSkinWeights.Influences(package, part);
        if (influences.Count == 0)
            return null;

        MeshCache? cache = _bent.TryGetValue(geometry, out MeshCache? kept) ? kept : null;
        if (mesh.Skin is { Triangles.Length: > 0 } skin)
        {
            var key = new WeightKey(restAttachment, skin.Version, influences.ConvertAll(bone => (bone, restBones[bone])));
            if (cache is null || cache.Skin != skin || cache.Key is null || !cache.Key.Matches(key))
            {
                var local = new Vector2[skin.Points.Length];
                var restPoints = new Vector2[skin.Points.Length];
                for (int i = 0; i < local.Length; i++)
                {
                    local[i] = skin.Points[i] - skin.ArtSize / 2f;
                    restPoints[i] = restAttachment * local[i];
                }
                cache = new MeshCache(-1, local, ColorsAt(geometry, local), skin.Triangles, 0, 0, null)
                {
                    Skin = skin,
                    RestPoints = restPoints,
                    Weights = RigSkinAuthoring.WeightsOver(package, skin, influences),
                    Key = key,
                };
                _bent.AddOrUpdate(geometry, cache);
            }
        }
        else
        {
            Rect2 bounds = art.Bounds;
            if (cache is null || cache.Detail != mesh.Detail || cache.Skin is not null)
            {
                (Vector2[] grid, _) = RigSkinGrid.Grid(mesh, bounds);
                (int columns, int rows) = RigSkinGrid.Points(mesh, bounds.Size);
                List<Vector2[]> outline = RigOutline.Polygons(geometry);
                bool[] covered = RigSkinGrid.Covered(mesh, bounds.Size,
                    uv => RigOutline.Inside(outline, bounds.Position + bounds.Size * uv) ? 1f : 0f);
                cache = new MeshCache(mesh.Detail, grid, ColorsAt(geometry, grid),
                    RigSkinGrid.Triangles(mesh, bounds.Size, covered), columns, rows, covered);
                _bent.AddOrUpdate(geometry, cache);
            }

            // Weights depend only on the rest pose, so they are worked out again
            // only when it, the bones or the blend change, not every frame.
            var key = new WeightKey(restAttachment, mesh.Blend, influences.ConvertAll(bone => (bone, restBones[bone])));
            if (cache.Weights is null || cache.Key is null || !cache.Key.Matches(key))
            {
                var restPoints = new Vector2[cache.Local.Length];
                for (int i = 0; i < restPoints.Length; i++)
                    restPoints[i] = restAttachment * cache.Local[i];
                cache.RestPoints = restPoints;
                cache.Weights = cache.Covered is { } coveredCells
                    ? RigSkinWeights.Weights(package, influences, restPoints, restBones, mesh.Blend, cache.Columns, cache.Rows, coveredCells)
                    : RigSkinWeights.Weights(package, influences, restPoints, restBones, mesh.Blend);
                cache.Key = key;
            }
        }

        var squash = new Transform2D[bones.Length];
        for (int i = 0; i < squash.Length; i++)
            squash[i] = RigRig.Foreshorten(package, pose, i);

        Vector2[] bent = RigSkinDeform.Deform(influences, cache.Weights!, cache.RestPoints, restBones, bones,
            restAttachment, placed, squash);
        for (int i = 0; i < bent.Length; i++)
            bent[i] = placement * bent[i];
        cache.Drawn = bent;
        return cache;
    }

    /// <summary>Each point's colour: the fill of the polygon it sits in.</summary>
    private static Color[] ColorsAt(XifGeometry geometry, Vector2[] points)
    {
        var colors = new Color[points.Length];
        for (int i = 0; i < points.Length; i++)
            colors[i] = ArtMesh.ColorAt(geometry, points[i]);
        return colors;
    }

    /// <summary>A bent part's points in its own space, built once per detail or skin, its weights, and where it was last drawn.</summary>
    private sealed record MeshCache(int Detail, Vector2[] Local, Color[] Colors, int[] Triangles, int Columns, int Rows,
        bool[]? Covered)
    {
        public Vector2[] Drawn { get; set; } = Array.Empty<Vector2>();

        public Vector2[] RestPoints { get; set; } = Array.Empty<Vector2>();

        public float[][]? Weights { get; set; }

        public WeightKey? Key { get; set; }

        /// <summary>The stored skin this was built from, or null for the grid.</summary>
        public RigSkinMesh? Skin { get; set; }
    }

    /// <summary>What a mesh's weights were worked out from: they hold while it does.</summary>
    private sealed record WeightKey(Transform2D RestAttachment, float Blend, List<(int Bone, Transform2D Pose)> Bones)
    {
        public bool Matches(WeightKey other) =>
            RestAttachment == other.RestAttachment && Blend == other.Blend &&
            Bones.Count == other.Bones.Count && Bones.SequenceEqual(other.Bones);
    }
}
