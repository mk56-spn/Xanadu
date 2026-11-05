using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Rendering;
using Xanadu.Systems.Canvas;
using Xanadu.Systems.Shaders;

namespace Xanadu.Systems.Playfields;

/// <summary>What drawing a frame's items needs, the same for the editor and play.</summary>
/// <param name="Editor">
/// The editor's view: both rail lanes, start points, and selection
/// (<paramref name="IsSelected"/>, <paramref name="SelectedNote"/>).
/// </param>
/// <param name="ShowsLayer">Which layers draw; the editor hides some. Every layer draws without it.</param>
public readonly record struct PlayfieldItemFrame(
    PlayfieldView View,
    Playfield Playfield,
    Playhead Playhead,
    float Seconds,
    bool Editor,
    Func<Entity, bool>? IsSelected = null,
    Entity SelectedNote = default,
    Func<int, bool>? ShowsLayer = null,
    GroupDefinition[]? Groups = null,
    IReadOnlyDictionary<int, LevelLayer>? Layers = null,
    IPassSurfaces? Passes = null);

/// <summary>
/// Draws every placed item and the effects they leave in layer order, with the
/// player wedge between the layers behind it and those in front
/// (docs/render-architecture.md §3, step 3).
/// </summary>
public static partial class PlayfieldRenderer
{
    private static readonly List<PlayfieldDrawEntry> DrawEntries = new();

    /// <summary>
    /// Draws the items back to front: each layer in use is begun on the canvas
    /// (<see cref="IRectCanvas.BeginLayer"/>), and <paramref name="drawWedge"/>
    /// draws the player wedge, layer 0, before the first layer in front of it.
    /// The canvas is left drawing over every layer.
    /// </summary>
    public static void DrawLayers(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        in PlayfieldItemFrame frame,
        Action? drawWedge)
    {
        // Items that follow a group's shader track read it while they draw.
        SetGroupTracks(frame.Groups, frame.Seconds);
        (bool Left, bool Right) rails = RailsShown(entities, previewBoth: frame.Editor);
        PlayfieldDrawList.Gather(entities, DrawEntries, startPoints: frame.Editor, railLanes: rails.Left || rails.Right);
        bool playing = IsPlaying(entities);

        // A buffered group's members draw together at its lowest slot instead
        // of in their own places (D12/D13).
        Dictionary<int, int> bufferedGroups = BufferedEntries(frame);
        var drawnBuffers = new HashSet<int>();

        bool wedgeDrawn = false;
        bool inLayer = false;
        int layer = 0;
        LevelLayer? pass = null;
        IPassSurface? surface = null;
        for (int i = 0; i < DrawEntries.Count; i++)
        {
            PlayfieldDrawEntry entry = DrawEntries[i];
            if (frame.ShowsLayer?.Invoke(entry.Layer) == false)
                continue;

            if (bufferedGroups.TryGetValue(i, out int bufferedGroup))
            {
                if (!drawnBuffers.Add(bufferedGroup))
                    continue;
                DrawBufferedGroup(canvas, rid, entities, bufferedGroup, i, frame, rails, playing);
                canvas.SelectShader(0);
                continue;
            }

            if (!wedgeDrawn && entry.Layer > 0)
            {
                FinishPassLayer(canvas, rid, frame, pass, surface);
                inLayer = false;
                pass = null;
                surface = null;
                DrawWedge(canvas, drawWedge);
                wedgeDrawn = true;
            }

            if (!inLayer || entry.Layer != layer)
            {
                FinishPassLayer(canvas, rid, frame, pass, surface);
                layer = entry.Layer;
                StartPassLayer(canvas, frame, layer, out pass, out surface);
                inLayer = true;
            }

            if (entry.Kind == PlayfieldDrawKind.Note && StartsNoteStretch(i))
                DrawStretchGlows(canvas, rid, i, frame);

            DrawEntry(canvas, rid, entities, entry, frame, rails, playing);
            canvas.SelectShader(0);
        }

        FinishPassLayer(canvas, rid, frame, pass, surface);
        if (!wedgeDrawn)
            DrawWedge(canvas, drawWedge);
        canvas.EndLayers();
    }

    /// <summary>
    /// Which entries are members of a buffered group, keyed by their index in
    /// the draw list. Only items in exactly one group can be buffered (D16).
    /// </summary>
    private static Dictionary<int, int> BufferedEntries(in PlayfieldItemFrame frame)
    {
        var groupsByEntry = new Dictionary<int, int>();
        for (int i = 0; i < DrawEntries.Count; i++)
        {
            PlayfieldDrawEntry entry = DrawEntries[i];
            if (entry.Entity.IsNull ||
                !entry.Entity.TryGetComponent(out ItemGroups mask) ||
                ItemGroups.Count(mask.Mask) != 1)
            {
                continue;
            }

            // A region is a boundary, not something that can be drawn into a buffer.
            if (entry.Kind == PlayfieldDrawKind.ScreenRead)
                continue;

            int group = System.Numerics.BitOperations.TrailingZeroCount(mask.Mask);
            if (group >= _groupTracks.Length || !_groupTracks[group].Buffered)
                continue;

            groupsByEntry[i] = group;
        }

        return groupsByEntry;
    }


    /// <summary>
    /// Draws one buffered group's members into its buffer, in their own order,
    /// and composites it where its lowest member sat.
    /// </summary>
    private static void DrawBufferedGroup(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        int group,
        int first,
        in PlayfieldItemFrame frame,
        (bool Left, bool Right) rails,
        bool playing)
    {
        GroupDefinition definition = _groupTracks[group];
        int layer = DrawEntries[first].Layer;
        bool drawn = false;
        canvas.BeginBuffer(
            definition.BufferShader,
            definition.BufferBlend,
            definition.BufferTint,
            definition.BufferAlpha);
        foreach ((string param, Vector4 value, int size) in ShaderKeyTrack.Evaluate(
                     definition.BufferKeys,
                     frame.Seconds))
        {
            canvas.SetShaderValue(param, value, size);
        }

        // The members are the run of entries after the first that belong to the
        // group; effects never join one.
        for (int i = first; i < DrawEntries.Count; i++)
        {
            PlayfieldDrawEntry entry = DrawEntries[i];
            if (entry.Kind != PlayfieldDrawKind.ScreenRead &&
                !entry.Entity.IsNull &&
                entry.Entity.TryGetComponent(out ItemGroups mask) &&
                mask.Has(group) &&
                ItemGroups.Count(mask.Mask) == 1 &&
                entry.Layer == layer &&
                frame.ShowsLayer?.Invoke(entry.Layer) != false)
            {
                DrawEntry(canvas, rid, entities, entry, frame, rails, playing);
                canvas.SelectShader(0);
                drawn = true;
            }
        }

        canvas.EndBuffer();
        if (!drawn && DrawEntries[first].Entity.IsNull)
            return;
    }

    /// <summary>
    /// Starts a layer on the canvas, with the target its pass needs: a
    /// self-contained layer's own viewport canvas, or the canvas itself. A
    /// layer's surface is freed as soon as its pass is no longer self-contained.
    /// </summary>
    private static void StartPassLayer(
        IRectCanvas canvas,
        in PlayfieldItemFrame frame,
        int layer,
        out LevelLayer? pass,
        out IPassSurface? surface)
    {
        pass = null;
        surface = null;
        if (frame.Layers is not null && frame.Layers.TryGetValue(layer, out LevelLayer? settings))
            pass = settings;

        Rid? target = null;
        if (pass?.Pass == LayerPassKind.SelfContained && frame.Passes is not null)
        {
            surface = frame.Passes.For(layer, frame.View.ScreenSize);
            target = surface.Canvas;
        }
        else
        {
            frame.Passes?.Release(layer);
        }

        canvas.BeginLayer(layer, target);
    }

    /// <summary>
    /// Ends a layer's pass: a self-contained layer's viewport is composited
    /// through the pass shader, and a background reader copies the screen just
    /// before its quad draws (docs/render-architecture.md D9).
    /// </summary>
    private static void FinishPassLayer(
        IRectCanvas canvas,
        Rid rid,
        in PlayfieldItemFrame frame,
        LevelLayer? pass,
        IPassSurface? surface)
    {
        if (pass is null)
            return;

        if (pass.Pass == LayerPassKind.SelfContained && surface is not null)
        {
            canvas.EndLayers();
            DrawLayerPass(canvas, rid, frame, pass, surface.Texture, selfContained: true);
        }
        else if (pass.Pass == LayerPassKind.BackgroundReader)
        {
            canvas.EndLayers();
            DrawLayerPass(canvas, rid, frame, pass, null, selfContained: false);
        }
    }

    /// <summary>The full-screen pass quad, with the layer's keyed instance values written.</summary>
    private static void DrawLayerPass(
        IRectCanvas canvas,
        Rid rid,
        in PlayfieldItemFrame frame,
        LevelLayer pass,
        Texture2D? texture,
        bool selfContained)
    {
        if (!selfContained && pass.PassShader == 0)
            return;

        Rect2 screen = new(Vector2.Zero, frame.View.ScreenSize);
        canvas.SelectShader(
            pass.PassShader,
            selfContained ? BlendMode.Premultiplied : BlendMode.Mix);
        foreach ((string param, Vector4 value, int size) in ShaderKeyTrack.Evaluate(pass.PassKeys, frame.Seconds))
            canvas.SetShaderValue(param, value, size);

        if (!selfContained)
            canvas.CopyScreen();

        if (texture is not null)
            canvas.DrawTexture(rid, screen, texture);
        else
            canvas.DrawRect(rid, screen, Colors.White);

        canvas.SelectShader(0);
    }

    private static void DrawWedge(IRectCanvas canvas, Action? drawWedge)
    {
        canvas.BeginLayer(0);
        drawWedge?.Invoke();
        canvas.SelectShader(0);
    }

    private static void DrawEntry(
        IRectCanvas canvas,
        Rid rid,
        EntityStore entities,
        PlayfieldDrawEntry entry,
        in PlayfieldItemFrame frame,
        (bool Left, bool Right) rails,
        bool playing)
    {
        Entity entity = entry.Entity;
        // An item draws where its groups have moved it; effects were spawned where it was shown.
        PlayfieldView view = entity.IsNull || entry.Kind >= PlayfieldDrawKind.NotePop
            ? frame.View
            : GroupOffset.View(entity, frame.View);
        bool selected = !entity.IsNull && frame.IsSelected?.Invoke(entity) == true;
        switch (entry.Kind)
        {
            case PlayfieldDrawKind.RailLanes:
                DrawRailLanes(canvas, rid, entities, view, frame.Playfield, frame.Playhead, rails);
                break;
            case PlayfieldDrawKind.Polygon:
                DrawPolygon(canvas, rid, entity, view, selected);
                break;
            case PlayfieldDrawKind.Block:
                DrawBlock(canvas, rid, entity, view, selected, playing);
                break;
            case PlayfieldDrawKind.Spike:
                DrawSpike(canvas, rid, entity, view, selected);
                break;
            case PlayfieldDrawKind.Grass:
                DrawGrass(canvas, rid, entity, view, frame.Seconds, selected);
                break;
            case PlayfieldDrawKind.Text:
                DrawLevelText(canvas, rid, entity, view, selected);
                break;
            case PlayfieldDrawKind.ScreenRead:
                DrawScreenRead(canvas, entity, view);
                break;
            case PlayfieldDrawKind.NoteGuides:
                DrawNoteGuides(canvas, rid, entities, view, frame.Playhead);
                break;
            case PlayfieldDrawKind.Note:
                DrawNote(canvas, rid, entity, view, frame.Playhead, frame.SelectedNote);
                break;
            case PlayfieldDrawKind.RailNote:
                // A rail note rides its lane, which groups don't move.
                DrawRailNote(canvas, rid, entity, frame.View, frame.Playfield, frame.Playhead, rails);
                break;
            case PlayfieldDrawKind.Mine:
                DrawMine(canvas, rid, entity, view, frame.Playhead);
                break;
            case PlayfieldDrawKind.StartPoint:
                DrawStartPoint(canvas, rid, entity, view, selected);
                break;
            case PlayfieldDrawKind.NotePop:
                DrawNotePop(canvas, rid, entity);
                break;
            case PlayfieldDrawKind.DashLinkFade:
                DrawDashLinkFade(canvas, rid, entity);
                break;
            case PlayfieldDrawKind.DashTrail:
                DrawDashTrail(canvas, rid, entity);
                break;
            case PlayfieldDrawKind.HitTrail:
                DrawHitTrail(canvas, rid, entity);
                break;
        }
    }

    /// <summary>
    /// Refreshes the back buffer for a screen-read region where the draw order
    /// reaches it (docs/render-architecture.md step 11, D10). The editor draws
    /// its outline separately, over every item, so the boundary's own mark can
    /// never feed a later screen read.
    /// </summary>
    private static void DrawScreenRead(IRectCanvas canvas, Entity entity, PlayfieldView view)
    {
        ScreenReadRegion region = entity.GetComponent<ScreenReadRegion>();
        Vector2 corner = view.PlayfieldToScreen(GroupOffset.Shown(entity, region.Position));
        canvas.CopyScreen(new Rect2(corner, region.Size * view.Scale));
    }

    /// <summary>Whether the note at <paramref name="index"/> is the first of a run of notes on one layer.</summary>
    private static bool StartsNoteStretch(int index)
    {
        if (index == 0)
            return true;
        PlayfieldDrawEntry previous = DrawEntries[index - 1];
        return previous.Kind != PlayfieldDrawKind.Note || previous.Layer != DrawEntries[index].Layer;
    }

    /// <summary>
    /// The glows of every note in the run starting at <paramref name="first"/>,
    /// before any of their bodies: one run of the glow shader under them.
    /// </summary>
    private static void DrawStretchGlows(IRectCanvas canvas, Rid rid, int first, in PlayfieldItemFrame frame)
    {
        int layer = DrawEntries[first].Layer;
        for (int i = first; i < DrawEntries.Count; i++)
        {
            PlayfieldDrawEntry entry = DrawEntries[i];
            if (entry.Kind != PlayfieldDrawKind.Note || entry.Layer != layer)
                break;
            DrawNoteGlow(canvas, rid, entry.Entity, GroupOffset.View(entry.Entity, frame.View), frame.Playhead);
        }

        canvas.SelectShader(0);
    }
}
