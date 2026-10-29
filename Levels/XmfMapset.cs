using System;
using System.Collections.Generic;

namespace Xanadu.Levels;

/// <summary>One playable chart within an <see cref="XmfMapset"/>, for example a difficulty.</summary>
public sealed class XmfVariant : IChartItems
{
    /// <summary>Stable variant identity, unrelated to its position in the mapset.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Stable human-readable variant slug.</summary>
    public string Slug { get; set; } = "";

    public string Name { get; set; } = "Untitled";

    public int SnapDivision { get; set; } = 4;

    public int PlayfieldWidth { get; set; } = 1000;

    public int PlayfieldHeight { get; set; } = 1000;

    /// <summary>The playfield background as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string BackgroundColor { get; set; } = "";

    public List<NoteDocument> Notes { get; set; } = new();

    public List<MineDocument> Mines { get; set; } = new();

    public List<StartPointDocument> StartPoints { get; set; } = new();

    public List<BlockDocument> Blocks { get; set; } = new();

    /// <summary>Spike tiles: permanent hazards that kill on contact.</summary>
    public List<SpikeDocument> Spikes { get; set; } = new();

    /// <summary>The groups that have a name or effects; the rest are empty slots.</summary>
    public List<GroupDocument> Groups { get; set; } = new();

    /// <summary>The mapset shader slot the player is drawn with, or 0 for none.</summary>
    public int PlayerShader { get; set; }

    /// <summary>The mapset shader slot the playfield background is drawn with, or 0 for none.</summary>
    public int FieldShader { get; set; }
}

/// <summary>
/// A map that can have multiple variants (for example difficulties), one
/// song, and the custom shaders its variants share.
/// </summary>
public sealed class XmfMapset
{
    public int Version { get; set; } = 1;

    /// <summary>Stable mapset identity; unrelated to its file path.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Stable human-readable mapset slug.</summary>
    public string Slug { get; set; } = "";

    /// <summary>The program-owned song every variant plays to.</summary>
    public string SongId { get; set; } = "";

    /// <summary>Custom shaders the mapset's variants may use, in slots 1 to <see cref="Xanadu.Ecs.ShaderLimits.Count"/>.</summary>
    public List<ShaderDocument> Shaders { get; set; } = new();

    public List<XmfVariant> Variants { get; set; } = new();
}
