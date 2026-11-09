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

    public int PlayfieldWidth { get; set; } = LevelDocument.DefaultFieldSize;

    public int PlayfieldHeight { get; set; } = LevelDocument.DefaultFieldSize;

    /// <summary>The playfield background as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string BackgroundColor { get; set; } = "";

    /// <summary>The colour around the playfield as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string OutsideColor { get; set; } = "";

    /// <summary>The playfield's frame and border line as #rrggbb or #rrggbbaa, or "" for the theme default.</summary>
    public string FrameColor { get; set; } = "";

    public List<NoteDocument> Notes { get; set; } = new();

    public List<MineDocument> Mines { get; set; } = new();

    public List<StartPointDocument> StartPoints { get; set; } = new();

    public List<BlockDocument> Blocks { get; set; } = new();

    /// <summary>Spike tiles: permanent hazards that kill on contact.</summary>
    public List<SpikeDocument> Spikes { get; set; } = new();

    /// <summary>Text written into the level.</summary>
    /// <summary>Friction bars: crossing one turns the ground's friction on.</summary>
    public List<FrictionBarDocument> FrictionBars { get; set; } = new();

    public List<TextDocument> Texts { get; set; } = new();

    /// <summary>Grass patches: swaying blades with no collision.</summary>
    public List<GrassDocument> Grass { get; set; } = new();

    /// <summary>Lens flares: shader-drawn light with no collision.</summary>
    public List<FlareDocument> Flares { get; set; } = new();

    /// <summary>Filled shapes with no collision.</summary>
    public List<PolygonDocument> Polygons { get; set; } = new();

    /// <summary>Screen-read regions: rects that refresh the back buffer where the draw order reaches them.</summary>
    public List<ScreenReadDocument> ScreenReads { get; set; } = new();

    /// <summary>The groups that have a name or effects; the rest are empty slots.</summary>
    public List<GroupDocument> Groups { get; set; } = new();

    /// <summary>The variant's layer table: the layers that have a name or a pass.</summary>
    public List<LayerDocument> Layers { get; set; } = new();

    /// <summary>The mapset shader slot the player is drawn with, or 0 for none.</summary>
    public int PlayerShader { get; set; }

    /// <summary>The mapset shader slot the playfield background is drawn with, or 0 for none.</summary>
    public int FieldShader { get; set; }

    /// <summary>The variant a level document's chart makes; its lists are shared, not copied.</summary>
    public static XmfVariant From(LevelDocument document) => new()
    {
        Id = document.Id,
        Slug = document.Slug,
        Name = document.Name,
        SnapDivision = document.SnapDivision,
        PlayfieldWidth = document.PlayfieldWidth,
        PlayfieldHeight = document.PlayfieldHeight,
        BackgroundColor = document.BackgroundColor,
        OutsideColor = document.OutsideColor,
        FrameColor = document.FrameColor,
        Notes = document.Notes,
        Mines = document.Mines,
        StartPoints = document.StartPoints,
        Blocks = document.Blocks,
        Spikes = document.Spikes,
        FrictionBars = document.FrictionBars,
        Texts = document.Texts,
        Grass = document.Grass,
        Flares = document.Flares,
        Polygons = document.Polygons,
        ScreenReads = document.ScreenReads,
        Groups = document.Groups,
        Layers = document.Layers,
        PlayerShader = document.PlayerShader,
        FieldShader = document.FieldShader,
    };

    /// <summary>The level document this variant makes, with a song and shaders from outside it.</summary>
    public LevelDocument ToDocument(int version, string songId, List<ShaderDocument> shaders) => new()
    {
        Version = version,
        Id = Id,
        Slug = Slug,
        SongId = songId,
        Name = Name,
        SnapDivision = SnapDivision,
        PlayfieldWidth = PlayfieldWidth,
        PlayfieldHeight = PlayfieldHeight,
        BackgroundColor = BackgroundColor,
        OutsideColor = OutsideColor,
        FrameColor = FrameColor,
        Notes = Notes,
        Mines = Mines,
        StartPoints = StartPoints,
        Blocks = Blocks,
        Spikes = Spikes,
        FrictionBars = FrictionBars,
        Texts = Texts,
        Grass = Grass,
        Flares = Flares,
        Polygons = Polygons,
        ScreenReads = ScreenReads,
        Groups = Groups,
        Layers = Layers,
        Shaders = shaders,
        PlayerShader = PlayerShader,
        FieldShader = FieldShader,
    };
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
