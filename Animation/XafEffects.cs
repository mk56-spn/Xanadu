using System.Collections.Generic;
using Godot;
using Xanadu.Items;
using Xanadu.Levels;

namespace Xanadu.Animation;

/// <summary>Authoring and rules for a package's effects.</summary>
public static class XafEffects
{
    /// <summary>How long a new streak shows from the key that turns it on.</summary>
    public const float DefaultShowSeconds = 0.15f;

    /// <summary>
    /// The outline of a speed streak: a long sliver pointing back along the
    /// bone's +x, widest at its head and tapering to a point at its tail.
    /// </summary>
    public static List<Vector2> StreakOutline() => new()
    {
        new Vector2(0, -6),
        new Vector2(24, -10),
        new Vector2(120, -2),
        new Vector2(120, 2),
        new Vector2(24, 10),
        new Vector2(0, 6),
    };

    /// <summary>A white streak drawn as one polygon, slightly see-through so the art shows under it.</summary>
    public static Xif Streak() => new()
    {
        Name = "streak",
        Primitives =
        {
            new XifPrimitive
            {
                Kind = XifPrimitive.PolygonKind,
                Points = StreakOutline(),
                Fill = new Color(1f, 1f, 1f, 0.85f),
            },
        },
    };

    /// <summary><paramref name="baseId"/>, or the first of <c>baseId-2</c>, <c>baseId-3</c>... not yet used.</summary>
    public static string UniqueId(XafPackage package, string baseId)
    {
        string id = baseId;
        for (int n = 2; package.Effects.Exists(effect => effect.Id == id); n++)
            id = $"{baseId}-{n}";
        return id;
    }

    /// <summary>Adds an effect on a bone, drawn above the rig's art; returns its index.</summary>
    public static int Add(XafPackage package, string boneId, Xif item, string baseId)
    {
        int top = 0;
        foreach (XafAttachment attachment in package.Attachments)
            top = Mathf.Max(top, attachment.ZOrder + 1);
        foreach (XafEffect other in package.Effects)
            top = Mathf.Max(top, other.ZOrder + 1);

        package.Effects.Add(new XafEffect
        {
            Id = UniqueId(package, baseId),
            BoneId = boneId,
            Item = item,
            ZOrder = Mathf.Min(top, XafAttachment.MaxLayer),
        });
        return package.Effects.Count - 1;
    }

    /// <summary>Removes an effect and every channel that keys it.</summary>
    public static bool Remove(XafPackage package, int index)
    {
        if (index < 0 || index >= package.Effects.Count)
            return false;

        string id = package.Effects[index].Id;
        package.Effects.RemoveAt(index);
        foreach (XafClip clip in package.Clips)
        {
            clip.Channels.RemoveAll(channel =>
                channel.Target == XafChannelTarget.Effect && channel.TargetId == id);
        }
        return true;
    }

    /// <summary>
    /// The rules an effect's item must follow to be drawn: polygons need an
    /// outline, and shaders must be common ones, since a rig belongs to no map
    /// whose shader slots it could use.
    /// </summary>
    public static void Validate(XafEffect effect, List<string> issues)
    {
        string name = $"Effect '{effect.Id}'";
        foreach (XifShaderRef shader in effect.Item.Shaders)
            CheckShader(name, shader, issues);

        foreach (XifPrimitive primitive in effect.Item.Primitives)
        {
            if (!primitive.Shader.IsNone)
                CheckShader(name, primitive.Shader, issues);
            if (primitive.IsPolygon && primitive.Points.Count < 3)
                issues.Add($"{name} has a polygon with fewer than three points.");
            foreach (Vector2 point in primitive.Points)
            {
                if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                {
                    issues.Add($"{name} has a non-finite point.");
                    break;
                }
            }
        }
    }

    private static void CheckShader(string name, XifShaderRef shader, List<string> issues)
    {
        if (shader.Scope != XifShaderScope.Common)
            issues.Add($"{name} uses a map shader slot; effects can only use common shaders.");
        else if (!CommonShaders.IsKnown(shader.Path))
            issues.Add($"{name} uses an unknown common shader '{shader.Path}'.");
    }
}
