using System.Collections.Generic;
using Godot;
using Xanadu.Ecs;
using Xanadu.Levels;
using Xanadu.Settings;

namespace Xanadu.Editor;

/// <summary>Stable editable-state snapshot used by undo/redo.</summary>
public readonly record struct EditorSnapshot(
    List<NoteSnapshot> Notes,
    List<MineSnapshot> Mines,
    List<StartPointSnapshot> StartPoints,
    List<BlockSnapshot> Blocks,
    List<SpikeSnapshot> Spikes,
    GroupDefinition[]? Groups = null,
    List<TextSnapshot>? Texts = null,
    List<GrassSnapshot>? Grass = null,
    List<PolygonSnapshot>? Polygons = null,
    LevelLayers? Layers = null,
    List<ScreenReadSnapshot>? ScreenReads = null);

/// <summary>Stable polygon-item snapshot used by undo/redo; it owns a copy of the item.</summary>
public readonly record struct PolygonSnapshot(
    Vector2 Position,
    float Rotation,
    Xanadu.Items.Xif Item,
    uint Groups = 0,
    int Shader = 0,
    uint Modulate = SelfModulate.White,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable screen-read-region snapshot used by undo/redo.</summary>
public readonly record struct ScreenReadSnapshot(
    Vector2 Position,
    Vector2 Size,
    ItemLayer Layer = default);

/// <summary>Stable grass-patch snapshot used by undo/redo.</summary>
public readonly record struct GrassSnapshot(
    GrassPatch Patch,
    uint Groups = 0,
    int Shader = 0,
    uint Modulate = SelfModulate.White,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable serializable level-text snapshot used by undo/redo.</summary>
public readonly record struct TextSnapshot(
    Vector2 Position,
    string Text,
    float Size,
    uint Groups = 0,
    int Shader = 0,
    uint Modulate = SelfModulate.White,
    float Rotation = 0f,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable serializable start-point snapshot used by undo/redo.</summary>
public readonly record struct StartPointSnapshot(
    string Name,
    long TimeMs,
    Vector2 Position,
    System.Numerics.Vector2 Momentum,
    Vector2 Direction,
    uint Groups = 0,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable serializable block snapshot used by undo/redo.</summary>
public readonly record struct BlockSnapshot(
    Vector2 Position,
    Vector2 Size,
    uint Groups = 0,
    int Shader = 0,
    int ShapeId = 0,
    int ShapePiece = 0,
    uint Modulate = SelfModulate.White,
    BlockOneWay OneWay = BlockOneWay.Off,
    float OutlineWidth = Block.DefaultOutlineWidth,
    uint OutlineColor = 0,
    uint FillGroups = Block.AllGroups,
    uint OutlineGroups = Block.AllGroups,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable serializable spike snapshot used by undo/redo.</summary>
public readonly record struct SpikeSnapshot(
    Vector2 Position,
    Vector2 Size,
    uint Groups = 0,
    int Shader = 0,
    uint Modulate = SelfModulate.White,
    float RotationDegrees = 0f,
    float OutlineWidth = Block.DefaultOutlineWidth,
    uint OutlineColor = 0,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable serializable mine snapshot used by undo/redo.</summary>
public readonly record struct MineSnapshot(
    long TimeMs,
    Vector2 Position,
    long FadeInMs,
    long FadeOutMs,
    uint Groups = 0,
    int Shader = 0,
    uint Modulate = SelfModulate.White,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);

/// <summary>Stable serializable note snapshot used by undo/redo.</summary>
public readonly record struct NoteSnapshot(
    long TimeMs,
    Vector2 Position,
    KeybindAction Target,
    NoteKind Kind,
    DashDirection DashDirection,
    int ContactRequired,
    CharacterKind Character,
    uint Groups = 0,
    int Shader = 0,
    DashStrength DashStrength = DashStrength.Single,
    PairIndex PairIndex = PairIndex.One,
    float Momentum = Note.DefaultMomentum,
    int ContactLeft = 0,
    int ContactRight = 0,
    ChannelSide ChannelSide = ChannelSide.Left,
    long HoldMs = 0,
    int ShapeId = 0,
    int ShapePiece = 0,
    uint Modulate = SelfModulate.White,
    ItemLayer Layer = default,
    Xanadu.Rendering.BlendMode Blend = Xanadu.Rendering.BlendMode.Mix,
    int ShaderSource = -1);
