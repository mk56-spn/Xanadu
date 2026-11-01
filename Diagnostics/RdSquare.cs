using System;
using System.Collections.Generic;
using Godot;

namespace Xanadu.Diagnostics;

/// <summary>
/// Puts a RenderingDevice square between two RenderingServer quads.
/// </summary>
/// <remarks>
/// <para>
/// The compositor pass cannot sit between canvas items: it runs inside the 3D
/// frame, and the whole canvas composites afterwards, so a compositor square is
/// always below every canvas quad regardless of draw index. To get in between,
/// the square has to become a canvas item itself: the main RenderingDevice
/// renders it to a texture each frame, and the canvas draws that texture at draw
/// index 1.
/// </para>
/// <para>
/// Nothing in the game references this scene or script; run it directly:
/// </para>
/// <code>
/// godot --path . res://Diagnostics/RdSquare.tscn
/// godot --path . res://Diagnostics/RdSquare.tscn -- --shot=&lt;file&gt;
/// </code>
/// </remarks>
public partial class RdSquare : Node3D
{
    private const string ShotPrefix = "--shot=";
    private const int ShotFrame = 60;
    private const int TextureSize = 512;

    private readonly List<Rid> _quads = new();
    private RdSquareTexture _square = null!;
    private Label _legend = null!;
    private int _frames;
    private string _shotPath = "";

    public override void _Ready()
    {
        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument.StartsWith(ShotPrefix, StringComparison.Ordinal))
                _shotPath = argument[ShotPrefix.Length..];
        }

        _square = new RdSquareTexture(TextureSize);
        AddRenderingServerQuads();
        AddLegend();
    }

    /// <summary>
    /// Magenta at draw index 0, the RenderingDevice square at 1 and yellow at 2,
    /// overlapping so the composite order is visible.
    /// </summary>
    private void AddRenderingServerQuads()
    {
        var host = new Node2D { Name = "RenderingServerQuads" };
        AddChild(host);
        Rid parent = host.GetCanvasItem();

        AddColorQuad(parent, new Color(0.9f, 0.3f, 0.8f), new Vector2(-30f, -30f), 0);

        AddTextureQuad(parent, _square.Texture.GetRid(), Vector2.Zero, 1);

        AddColorQuad(parent, new Color(1f, 0.9f, 0.2f), new Vector2(30f, 30f), 2);
    }

    private void AddColorQuad(Rid parent, Color color, Vector2 offset, int drawIndex)
    {
        Rid quad = CreateQuad(parent, offset, drawIndex);
        RenderingServer.CanvasItemAddRect(quad, QuadRect(offset), color);
        _quads.Add(quad);
    }

    private void AddTextureQuad(Rid parent, Rid texture, Vector2 offset, int drawIndex)
    {
        Rid quad = CreateQuad(parent, offset, drawIndex);
        RenderingServer.CanvasItemAddTextureRect(
            quad, QuadRect(offset), texture, false, null, false);
        _quads.Add(quad);
    }

    private static Rid CreateQuad(Rid parent, Vector2 offset, int drawIndex)
    {
        Rid quad = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(quad, parent);
        RenderingServer.CanvasItemSetDrawIndex(quad, drawIndex);
        return quad;
    }

    private static Rect2 QuadRect(Vector2 offset)
    {
        var center = new Vector2(
            RdSquareLayout.DesignWidth * 0.5f + RdSquareLayout.CenterOffset,
            RdSquareLayout.DesignHeight * 0.5f) + offset;
        var size = new Vector2(RdSquareLayout.Size, RdSquareLayout.Size);
        return new Rect2(center - size * 0.5f, size);
    }

    /// <summary>Corner key for the squares, same HUD style as CanvasThreadStress.</summary>
    private void AddLegend()
    {
        _legend = new Label
        {
            Position = new Vector2(24, 20),
            Text =
                "orange   RenderingDevice square (main device texture, draw index 1)\n" +
                "magenta  RenderingServer quad, draw index 0 (behind)\n" +
                "yellow   RenderingServer quad, draw index 2 (above)",
        };
        _legend.AddThemeFontSizeOverride("font_size", 20);
        AddChild(_legend);
    }

    public override void _ExitTree()
    {
        foreach (Rid quad in _quads)
        {
            if (quad.IsValid)
                RenderingServer.FreeRid(quad);
        }

        _square.Free();
    }

    public override void _Process(double delta)
    {
        _square.Draw();

        if (_shotPath.Length == 0 || ++_frames < ShotFrame)
            return;

        Image image = GetViewport().GetTexture().GetImage();
        Error error = image.SavePng(_shotPath);
        GD.Print($"RdSquare: wrote {_shotPath} ({error})");
        GetTree().Quit(error == Error.Ok ? 0 : 1);
    }
}

/// <summary>
/// One size and overlap for the squares. Design space is 1920x1080 because the
/// project stretches the whole canvas as 16:9.
/// </summary>
internal static class RdSquareLayout
{
    public const float DesignWidth = 1920f;
    public const float DesignHeight = 1080f;
    public const float HeightFraction = 0.35f;
    public const float GapScale = 1.1f;

    /// <summary>Pull the stack left so the squares overlap a little.</summary>
    public const float Overlap = 90f;

    public static float Size => DesignHeight * HeightFraction;
    public static float CenterOffset => Size * GapScale * 0.5f - Overlap;
}
