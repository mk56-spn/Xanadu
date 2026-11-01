using System;
using Godot;

namespace Xanadu.Diagnostics;

/// <summary>
/// Conway's Game of Life, ping-ponged between two <see cref="DrawableTexture2D"/>s
/// by a texture blit shader and painted by a Slang shader on the RenderingDevice.
/// </summary>
/// <remarks>
/// <para>
/// Each generation blits the front texture into the back one through
/// RdLife.gdshader, then the two swap. <see cref="RdLifePainter"/> samples the new
/// front directly, and the canvas draws what it painted.
/// </para>
/// <para>
/// Nothing in the game references this scene or script; build the shaders as
/// described in RdLife.slang, then run it directly:
/// </para>
/// <code>
/// godot --path . res://Diagnostics/RdLife.tscn
/// godot --path . res://Diagnostics/RdLife.tscn -- --shot=&lt;file&gt;
/// </code>
/// </remarks>
public partial class RdLife : Node2D
{
    /// <summary>Matches Cells in RdLife.slang.</summary>
    private const int Cells = 128;
    private const int PaintSize = 1024;
    private const int FramesPerGeneration = 5;
    private const float DisplaySize = 980f;
    private const float DesignWidth = 1920f;
    private const float DesignHeight = 1080f;
    private const float SeedDensity = 0.3f;
    private const ulong Seed = 20261031;
    private const string ShotPrefix = "--shot=";
    private const int ShotFrame = 180;

    private readonly DrawableTexture2D[] _states = { new(), new() };
    private ShaderMaterial _step = null!;
    private RdLifePainter _painter = null!;
    private Rid _quad;
    private int _front;
    private int _frames;
    private string _shotPath = "";

    public override void _Ready()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(ShotPrefix, StringComparison.Ordinal))
                _shotPath = argument[ShotPrefix.Length..];
        }

        foreach (DrawableTexture2D state in _states)
        {
            state.Setup(Cells, Cells, DrawableTexture2D.DrawableFormat.Rgba8, Colors.Black, false);
        }

        _step = new ShaderMaterial { Shader = GD.Load<Shader>("res://Diagnostics/RdLife.gdshader") };

        using ImageTexture seed = ImageTexture.CreateFromImage(CreateSeed());
        _states[_front].BlitRect(new Rect2I(0, 0, Cells, Cells), seed);

        _painter = new RdLifePainter(PaintSize, _states[0].GetRid(), _states[1].GetRid());

        _quad = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(_quad, GetCanvasItem());
        var size = new Vector2(DisplaySize, DisplaySize);
        var center = new Vector2(DesignWidth, DesignHeight) * 0.5f;
        RenderingServer.CanvasItemAddTextureRect(
            _quad, new Rect2(center - size * 0.5f, size), _painter.Texture.GetRid(), false, null, false);
    }

    public override void _Process(double delta)
    {
        if (++_frames % FramesPerGeneration == 0)
            StepGeneration();

        _painter.Draw(_front);

        if (_shotPath.Length == 0 || _frames < ShotFrame)
            return;

        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_shotPath);
        GD.Print($"RdLife: wrote {_shotPath} ({error})");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }

    public override void _ExitTree()
    {
        if (_quad.IsValid)
            RenderingServer.FreeRid(_quad);

        _painter.Free();
    }

    private void StepGeneration()
    {
        int back = 1 - _front;
        _states[back].BlitRect(new Rect2I(0, 0, Cells, Cells), _states[_front], null, 0, _step);
        _front = back;
    }

    private static Image CreateSeed()
    {
        var random = new RandomNumberGenerator { Seed = Seed };
        Image image = Image.CreateEmpty(Cells, Cells, false, Image.Format.Rgba8);
        for (int y = 0; y < Cells; y++)
        {
            for (int x = 0; x < Cells; x++)
            {
                bool alive = random.Randf() < SeedDensity;
                image.SetPixel(x, y, alive ? new Color(1f, 0f, 1f) : Colors.Black);
            }
        }

        return image;
    }
}
