using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Core.Layering;
using Xanadu.Core.Particles;
using Xanadu.Model.Levels;

namespace Xanadu.Model.Animation;

/// <summary>Authoring and rules for a rig's parts that start hidden (streaks, flashes, particle streams), and for every part's look.</summary>
public static class RigEffects
{
    /// <summary>How long a new streak shows from the key that turns it on.</summary>
    public const float DefaultShowSeconds = 0.15f;

    /// <summary>How long a new particle stream flows from the key that turns it on.</summary>
    public const float DefaultStreamSeconds = 0.5f;

    /// <summary>
    /// A new particle node's look: a small four-pointed spark, white so the
    /// effect's colour ramp shows true. Each particle draws it (through the
    /// node's shader, when it has one); its size here is a particle's at scale 1.
    /// </summary>
    public static XifGeometry SparkArt() => new()
    {
        Polygons =
        {
            new XifPolygon
            {
                Fill = Colors.White,
                Points =
                {
                    new Vector2(0f, -8f), new Vector2(1.6f, -1.6f), new Vector2(8f, 0f), new Vector2(1.6f, 1.6f),
                    new Vector2(0f, 8f), new Vector2(-1.6f, 1.6f), new Vector2(-8f, 0f), new Vector2(-1.6f, -1.6f),
                },
            },
        },
    };

    /// <summary>
    /// A new particle stream's own settings: a copy of the library's sparks
    /// preset when there is one, else sparks made here.
    /// </summary>
    public static XpfParticle StreamParticle()
    {
        const string name = "sparks";
        if (ParticleLibrary.Find(name) is { } preset)
            return preset.Clone();

        var sparks = new XpfParticle
        {
            Name = name, Amount = 40, Lifetime = 0.5f, Explosiveness = 0f, Direction = -150f, Spread = 25f,
            SpeedMin = 100f, SpeedMax = 300f, Gravity = new Vector2(0, 900), ScaleMin = 0.4f, ScaleMax = 0.9f,
            Color = new Color(1f, 0.8f, 0.4f),
        };
        XpfEditing.SetFadePreset(sparks, XpfFadePreset.Out);
        return sparks;
    }

    /// <summary>
    /// The outline of a speed streak: a long sliver pointing back along the
    /// bone's +x, widest at its head and tapering to a point at its tail.
    /// </summary>
    public static List<Vector2> StreakOutline() =>
    [
        new Vector2(0, -6),
        new Vector2(24, -10),
        new Vector2(120, -2),
        new Vector2(120, 2),
        new Vector2(24, 10),
        new Vector2(0, 6),
    ];

    /// <summary>A white streak drawn as one polygon, slightly see-through so the art shows under it.</summary>
    public static Xif Streak() => new()
    {
        Name = "streak",
        Modules =
        {
            new XifGeometry
            {
                Polygons = { new XifPolygon { Points = StreakOutline(), Fill = new Color(1f, 1f, 1f, 0.85f) } },
            },
        },
    };

    /// <summary>
    /// Adds a part on a bone that starts hidden, such as a streak, drawn above
    /// the rig's art. <paramref name="look"/> becomes the part's node; returns
    /// the part's index.
    /// </summary>
    public static int Add(RigPackage package, string boneId, Xif look, string baseId)
    {
        int top = 0;
        foreach (RigAttachment attachment in package.Attachments)
            top = Math.Max(top, attachment.ZOrder + 1);

        look.Id = package.UniqueId(baseId);
        var part = new RigAttachment
        {
            BoneId = boneId,
            Hidden = true,
            ZOrder = Math.Min(top, DrawOrder.MaxLayer),
        };
        part.Node = look;
        package.Add(part);
        return package.Attachments.IndexOf(part);
    }

    /// <summary>Removes a part, everything under it, and every channel that keys them.</summary>
    public static bool Remove(RigPackage package, int index)
    {
        if (index < 0 || index >= package.Attachments.Count)
            return false;

        Xif node = package.Attachments[index].Node;
        var ids = new HashSet<string>();
        Collect(node, ids);
        if (!package.Remove(node))
            return false;
        foreach (RigClip clip in package.Clips)
        {
            clip.Channels.RemoveAll(channel =>
                channel.Target == RigChannelTarget.Attachment && ids.Contains(channel.TargetId));
        }
        return true;
    }

    private static void Collect(Xif node, HashSet<string> ids)
    {
        ids.Add(node.Id);
        foreach (Xif child in node.Children)
            Collect(child, ids);
    }

    /// <summary>
    /// The rules a part's look must follow to be drawn: polygons need an
    /// outline, and shaders must be common ones, since a rig belongs to no map
    /// whose shader slots it could use.
    /// </summary>
    public static void Validate(RigAttachment part, List<string> issues)
    {
        string name = $"Part '{part.Id}'";
        Xif item = part.Node;
        if (!item.ShaderRef.IsNone)
            CheckShader(name, item.ShaderRef, issues);

        for (int art = 0; art < part.ArtCount; art++)
        {
            foreach (XifPolygon polygon in part.Art(art).Polygons)
            {
                if (polygon.Points.Count < 3)
                    issues.Add($"{name} has a polygon with fewer than three points.");
                foreach (Vector2 point in polygon.Points)
                {
                    if (!float.IsFinite(point.X) || !float.IsFinite(point.Y))
                    {
                        issues.Add($"{name} has a non-finite point.");
                        break;
                    }
                }
            }
        }
    }

    private static void CheckShader(string name, XifShaderRef shader, List<string> issues)
    {
        if (shader.Scope != XifShaderScope.Common)
            issues.Add($"{name} uses a map shader slot; rigs can only use common shaders.");
        else if (!CommonShaders.IsKnown(shader.Path))
            issues.Add($"{name} uses an unknown common shader '{shader.Path}'.");
    }
}
