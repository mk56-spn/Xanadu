using System.Collections.Generic;
using System.Linq;
using MoonWorks.Graphics;
using Xanadu.Rendering;
using Xanadu.Ui.Systems.Canvas;
using Color = Xanadu.Core.Maths.Color;
using Handle = Xanadu.Core.Maths.Handle;

namespace Xanadu.Tools.Shaders;

/// <summary>
/// Lab effects (raymarchers and the like) are too heavy to draw live in the grid on every frame, so each gets a still:
/// drawn into a frame-sized texture for <see cref="Frames"/> frames when the tool opens or the stack is saved, then left
/// as it is. By file index: the texture its thumbnail shows, and the offscreen item drawing it while it's live.
/// </summary>
internal sealed class ShaderStills(CanvasRenderer renderer, CanvasTree canvas, uint width, uint height)
{
    /// <summary>Frames a still is drawn for (its update fills its state first), then it stops.</summary>
    private const int Frames = 4;

    private readonly Dictionary<int, (Texture Target, Handle Root, int Left)> _stills = [];

    /// <summary>A still for stack <paramref name="index"/> at <paramref name="path"/>: the texture the thumbnail shows, drawing now.</summary>
    public Handle Make(int index, string path)
    {
        Texture target = renderer.CreateTarget("still " + path);
        Handle texture = canvas.MintHandle();
        renderer.Textures[texture.Id] = target;
        _stills[index] = (target, default, 0);
        Redraw(index, path);
        return texture;
    }

    /// <summary>Draws a still again from the stack at <paramref name="path"/>: the whole frame, so the effect keeps its own coordinates.</summary>
    public void Redraw(int index, string path)
    {
        if (!_stills.TryGetValue(index, out var still))
            return;
        Handle root = still.Left > 0 ? still.Root : canvas.CreateItem();
        CanvasServer.Current.Clear(root);
        CanvasServer.Current.AddRect(root, new Rect2(0, 0, width, height), new Color(1, 1, 1));
        CanvasServer.Current.SetMaterial(root, new CanvasMaterial(path));
        if (still.Left == 0)
            renderer.AddOffscreen(canvas.Item(root)!, still.Target);
        _stills[index] = (still.Target, root, Frames);
    }

    /// <summary>Counts the live stills down; each stops drawing when it reaches 0.</summary>
    public void Settle()
    {
        foreach ((int index, var still) in _stills.ToArray())
        {
            if (still.Left == 0)
                continue;
            if (still.Left > 1)
            {
                _stills[index] = still with { Left = still.Left - 1 };
                continue;
            }

            if (canvas.Item(still.Root) is { } root)
                renderer.RemoveOffscreen(root);
            canvas.Free(still.Root);
            _stills[index] = (still.Target, default, 0);
        }
    }
}
