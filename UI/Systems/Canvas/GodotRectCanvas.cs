using System;
using Godot;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Systems.Canvas;

/// <summary>The Godot canvas: <see cref="ServerRectCanvas"/>'s shapes plus text and glows through Godot's fonts.</summary>
public sealed class GodotRectCanvas : ServerRectCanvas, IRectCanvas
{
    private const int FontSize = 16;
    private const string BoldFontPath = "res://Assets/Fonts/SourceSans3-Variable.ttf";
    private const int BoldWeight = 700;
    private const string MonoFontPath = "res://Assets/Fonts/JetBrainsMono-Regular.ttf";

    private static Font? _boldFont;
    private static Font? _monoFont;

    /// <summary>JetBrains Mono, or the fallback font if the file cannot be loaded.</summary>
    private static Font MonoFont => _monoFont ??= LoadMonoFont();

    private static Font LoadMonoFont()
    {
        if (ResourceLoader.Exists(MonoFontPath) && GD.Load<FontFile>(MonoFontPath) is { } file)
            return file;

        GD.PushError($"Mono font '{MonoFontPath}' not found; using the default font.");
        return ThemeDB.FallbackFont;
    }

    /// <summary>
    /// Source Sans 3 at weight 700, or the fallback font if the file cannot
    /// be loaded. The file is a variable font whose default weight is light.
    /// </summary>
    private static Font BoldFont => _boldFont ??= LoadBoldFont();

    private static Font LoadBoldFont()
    {
        if (GD.Load<FontFile>(BoldFontPath) is not { } file)
        {
            GD.PushError($"Bold font '{BoldFontPath}' not found; using the default font.");
            return ThemeDB.FallbackFont;
        }

        return new FontVariation
        {
            BaseFont = file,
            VariationOpentype = new Godot.Collections.Dictionary
            {
                { TextServerManager.GetPrimaryInterface().NameToTag("wght"), BoldWeight },
            },
        };
    }
    private const float TextPadding = 4;

    private readonly GodotTextGlow _glow = new();
    private readonly GodotShapeGlow _shapeGlow = new();

    public override void Clear(Rid canvas)
    {
        base.Clear(canvas);
        _glow.Clear(canvas);
        _shapeGlow.Clear(canvas);
        if (!_rotated.TryGetValue(canvas, out RotatedPool? pool))
            return;

        foreach (Rid item in pool.Items)
            RenderingServer.CanvasItemClear(item);
        pool.Used = 0;
    }

    public void DrawTexture(Rid canvas, Rect2 bounds, Texture2D texture) =>
        RenderingServer.CanvasItemAddTextureRect(
            canvas,
            bounds,
            texture.GetRid());

    public override void DrawText(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Center);

    public void DrawTextStyled(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Center);

    public void DrawTextStyledLeft(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Left);

    public void DrawTextStyledRight(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize) =>
        DrawTextAligned(canvas, bounds, text, color, fontSize, HorizontalAlignment.Right);

    public void DrawTextTurned(Rid canvas, Rect2 bounds, float degrees, string text, Color color, int fontSize)
    {
        if (degrees == 0f)
        {
            DrawTextStyled(canvas, bounds, text, color, fontSize);
            return;
        }

        // A transform command turns what follows on this item, so the text keeps its place in the draw order.
        Vector2 center = bounds.GetCenter();
        RenderingServer.CanvasItemAddSetTransform(canvas, new Transform2D(Mathf.DegToRad(degrees), center));
        DrawTextStyled(canvas, new Rect2(bounds.Position - center, bounds.Size), text, color, fontSize);
        RenderingServer.CanvasItemAddSetTransform(canvas, Transform2D.Identity);
    }

    /// <summary>
    /// Rotated child items for vertical text, one per label drawn since the
    /// canvas was last cleared, so several labels can be on screen at once.
    /// </summary>
    private readonly System.Collections.Generic.Dictionary<Rid, RotatedPool> _rotated = new();

    private sealed class RotatedPool
    {
        public readonly System.Collections.Generic.List<Rid> Items = new();
        public int Used;
    }

    public void DrawTextVertical(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (!_rotated.TryGetValue(canvas, out RotatedPool? pool))
        {
            pool = new RotatedPool();
            _rotated.Add(canvas, pool);
        }

        if (pool.Used == pool.Items.Count)
        {
            Rid created = RenderingServer.CanvasItemCreate();
            RenderingServer.CanvasItemSetParent(created, canvas);
            RenderingServer.CanvasItemSetZIndex(created, 2);
            pool.Items.Add(created);
        }

        Rid item = pool.Items[pool.Used++];
        RenderingServer.CanvasItemClear(item);
        RenderingServer.CanvasItemSetTransform(
            item,
            new Transform2D(-Mathf.Pi / 2, new Vector2(
                bounds.Position.X + 2,
                bounds.End.Y - 2)));

        Font font = ThemeDB.FallbackFont;
        font.DrawString(
            item,
            new Vector2(0, fontSize),
            text,
            HorizontalAlignment.Left,
            Mathf.Max(0, bounds.Size.Y - 4),
            fontSize,
            color);
    }

    public void DrawTextWeighted(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold) =>
        DrawTextAligned(
            canvas,
            bounds,
            text,
            color,
            fontSize,
            align switch
            {
                TextAlign.Left => HorizontalAlignment.Left,
                TextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
            bold ? BoldFont : ThemeDB.FallbackFont);

    public void DrawTextMono(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align) =>
        DrawTextAligned(
            canvas,
            bounds,
            text,
            color,
            fontSize,
            align switch
            {
                TextAlign.Left => HorizontalAlignment.Left,
                TextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
            MonoFont);

    public void DrawTextGlow(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        TextGlow glow,
        TextOutline? outline = null) =>
        _glow.Draw(
            canvas,
            bounds,
            text,
            color,
            fontSize,
            align switch
            {
                TextAlign.Left => HorizontalAlignment.Left,
                TextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
            bold ? BoldFont : ThemeDB.FallbackFont,
            glow,
            outline);

    public void DrawShapeGlow(
        Rid canvas,
        ReadOnlySpan<Vector2> points,
        bool closed,
        bool filled,
        float strokeWidth,
        ShapeGlow glow) =>
        _shapeGlow.Draw(canvas, points, closed, filled, strokeWidth, glow);

    public void DrawTextOutline(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        TextAlign align,
        bool bold,
        int size) =>
        DrawTextAligned(
            canvas,
            bounds,
            text,
            color,
            fontSize,
            align switch
            {
                TextAlign.Left => HorizontalAlignment.Left,
                TextAlign.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Center,
            },
            bold ? BoldFont : ThemeDB.FallbackFont,
            size);

    public override void DrawTextLeft(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Left);

    public override void DrawTextRight(Rid canvas, Rect2 bounds, string text, Color color) =>
        DrawTextAligned(canvas, bounds, text, color, FontSize, HorizontalAlignment.Right);

    internal static void DrawTextAligned(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        HorizontalAlignment alignment,
        Font? face = null,
        int outlineSize = 0)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Font font = face ?? ThemeDB.FallbackFont;
        float baseline = bounds.Position.Y +
                         (bounds.Size.Y - font.GetHeight(fontSize)) / 2 +
                         font.GetAscent(fontSize);

        var origin = new Vector2(bounds.Position.X + TextPadding, baseline);
        float width = Mathf.Max(0, bounds.Size.X - TextPadding * 2);
        if (outlineSize > 0)
        {
            font.DrawStringOutline(
                canvas, origin, text, alignment, width, fontSize, outlineSize, color);
            return;
        }

        font.DrawString(canvas, origin, text, alignment, width, fontSize, color);
    }
}
