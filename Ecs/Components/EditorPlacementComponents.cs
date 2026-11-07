using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Settings;
using Xanadu.Systems.Audio;
using Xanadu.Systems.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Ecs;

/// <summary>What clicking the field places: a note kind, or another playfield item.</summary>
public enum EditorPlacementMode
{
    Notes,
    Mines,
    Dash,
    Start,
    Contact,
    CharacterSwitch,
    Blocks,
    Spikes,
    Pair,
    Momentum,
    WeaponSwitch,
    Channel,
    FormStep,
    Text,
    Grass,
    Polygon,

    /// <summary>A screen-read region: a rect that refreshes the back buffer where it is reached.</summary>
    ScreenRead,
}

/// <summary>The block the block tool lays down, as fractions of a grid cell.</summary>
public enum BlockBrushSize
{
    /// <summary>One cell square: the default.</summary>
    Cell,

    /// <summary>Half a cell each way, a quarter of a cell's area.</summary>
    Quarter,

    /// <summary>Two cells wide and one high: a small platform.</summary>
    Platform,
}

/// <summary>
/// The editor's placement brush: what clicking the field places, and everything
/// a newly placed item gets.
/// </summary>
public struct EditorPlacement() : IComponent
{
    public EditorPlacementMode Mode = EditorPlacementMode.Notes;

    /// <summary>The size of the block the block tool places.</summary>
    public BlockBrushSize BlockSize = BlockBrushSize.Cell;

    /// <summary>The note type new notes get.</summary>
    public KeybindAction Note = KeybindAction.M;

    /// <summary>The character new character-switch notes switch to.</summary>
    public CharacterKind Character = CharacterKind.Contact;

    /// <summary>The turn given to new blocks and spikes, in degrees clockwise from up.</summary>
    public float Degrees;

    public DashDirection Direction = DashDirection.UpRight;

    /// <summary>The dash strength new movement notes get.</summary>
    public DashStrength Strength = DashStrength.Single;

    /// <summary>The left and right charge new contact barriers ask for.</summary>
    public int ContactLeft = 1;
    public int ContactRight = 1;

    /// <summary>The hold length new rail notes get, in milliseconds.</summary>
    public long RailHoldMs;

    /// <summary>The Form construction shape new steps are authored into, or 0 for none.</summary>
    public int ShapeId;

    /// <summary>
    /// The settings new grass patches get; the grass panel keeps it matching
    /// the patch last stepped, so the next patch looks like it.
    /// </summary>
    public GrassPatch Grass = new();

    /// <summary>
    /// The self modulate new items get. The colour field sets it when a colour is
    /// picked or typed with nothing tintable selected, and shows it then, so the
    /// pick sticks instead of falling back to white.
    /// </summary>
    public Color Modulate = Colors.White;
}

/// <summary>A part of the left panel that only shows for the tools that use it.</summary>
public struct EditorToolSection(EditorPlacementMode[] modes) : IComponent
{
    public EditorPlacementMode[] Modes = modes;
}

/// <summary>Where the pointer would place the current tool's item this frame, if anywhere.</summary>
public struct EditorPlacementGhost : IComponent
{
    public bool Visible;
    public Vector2 Screen;
}
