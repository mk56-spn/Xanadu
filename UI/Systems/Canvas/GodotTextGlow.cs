using System.Collections.Generic;
using Godot;
using Godot.Collections;
using Xanadu.Ecs;

namespace Xanadu.Systems.Canvas;

/// <summary>
/// Draws text with a blurred glow behind it, entirely on drawable textures.
/// The text is shaped by the text server and each glyph is blitted from the
/// font atlas into a drawable texture, which two blur blits then spread into
/// the glow. No viewport is involved, so the glow is ready the frame the text
/// is drawn.
/// </summary>
internal sealed class GodotTextGlow
{
    private const float TextPadding = 4;

    private sealed class State
    {
        public Rid GlowItem, CrispItem;
        public readonly DrawableTexture2D Glyphs = new();
        public readonly GodotBlur Blur = new();

        // Queued blits read these textures when the frame renders.
        public readonly List<ImageTexture> Atlases = new();
    }

    private readonly System.Collections.Generic.Dictionary<Rid, State> _states = new();

    public void Clear(Rid canvas)
    {
        if (!_states.TryGetValue(canvas, out State? state))
            return;

        RenderingServer.CanvasItemClear(state.GlowItem);
        RenderingServer.CanvasItemClear(state.CrispItem);
    }

    public void Draw(
        Rid canvas,
        Rect2 bounds,
        string text,
        Color color,
        int fontSize,
        HorizontalAlignment alignment,
        Font face,
        TextGlow glow,
        TextOutline? outline = null)
    {
        if (string.IsNullOrEmpty(text))
            return;

        float pad = GodotBlur.Padding(glow.Radius);
        var size = new Vector2I(
            Mathf.CeilToInt(bounds.Size.X + pad * 2),
            Mathf.CeilToInt(bounds.Size.Y + pad * 2));

        State state = StateFor(canvas);
        state.Atlases.Clear();

        Vector2 origin = TextOrigin(bounds, text, fontSize, alignment, face);
        state.Glyphs.Setup(size.X, size.Y, DrawableTexture2D.DrawableFormat.Rgba8,
            new Color(0, 0, 0, 0), false);
        GodotGlyphBlitter.Blit(
            state.Glyphs,
            state.Atlases,
            text,
            fontSize,
            face,
            origin - bounds.Position + new Vector2(pad, pad));

        state.Blur.Run(state.Glyphs, size, glow.Radius, glow.Color, glow.Intensity);

        // Setup can replace the texture's RID, so the quad is added afterwards.
        RenderingServer.CanvasItemClear(state.GlowItem);
        RenderingServer.CanvasItemAddTextureRect(
            state.GlowItem,
            new Rect2(bounds.Position - new Vector2(pad, pad), size),
            state.Blur.Result.GetRid());

        RenderingServer.CanvasItemClear(state.CrispItem);
        if (outline is { } line)
        {
            GodotRectCanvas.DrawTextAligned(
                state.CrispItem, bounds, text, line.Color, fontSize, alignment, face, line.Size);
        }

        GodotRectCanvas.DrawTextAligned(
            state.CrispItem, bounds, text, color, fontSize, alignment, face);
    }

    /// <summary>Where the text's pen starts, on the baseline, in canvas space.</summary>
    private static Vector2 TextOrigin(
        Rect2 bounds, string text, int fontSize, HorizontalAlignment alignment, Font face)
    {
        float baseline = bounds.Position.Y +
                         (bounds.Size.Y - face.GetHeight(fontSize)) / 2 +
                         face.GetAscent(fontSize);
        float width = face.GetStringSize(text, HorizontalAlignment.Left, -1, fontSize).X;
        float room = bounds.Size.X - TextPadding * 2;
        float x = alignment switch
        {
            HorizontalAlignment.Right => room - width,
            HorizontalAlignment.Center => (room - width) / 2,
            _ => 0,
        };
        return new Vector2(bounds.Position.X + TextPadding + Mathf.Max(0, x), baseline);
    }

    private State StateFor(Rid canvas)
    {
        if (_states.TryGetValue(canvas, out State? existing))
            return existing;

        // Children draw after the parent in creation order: glow, then text.
        var state = new State
        {
            GlowItem = CreateChild(canvas),
            CrispItem = CreateChild(canvas),
        };
        _states.Add(canvas, state);
        return state;
    }

    private static Rid CreateChild(Rid parent)
    {
        Rid item = RenderingServer.CanvasItemCreate();
        RenderingServer.CanvasItemSetParent(item, parent);
        RenderingServer.CanvasItemSetDefaultTextureFilter(
            item, RenderingServer.CanvasItemTextureFilter.Linear);
        RenderingServer.CanvasItemSetDefaultTextureRepeat(
            item, RenderingServer.CanvasItemTextureRepeat.Disabled);
        return item;
    }
}
