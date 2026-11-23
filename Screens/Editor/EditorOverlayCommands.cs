using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Levels;

namespace Xanadu.Screens.Editor;

/// <summary>Editor overlay command and preview controls.</summary>
public sealed partial class EditorOverlay
{
    private Entity _listening;
    private readonly List<Action> _asked = [];

    /// <summary>Acts on what the editor asked for since the last tick (<see cref="Listen"/>).</summary>
    private void ProcessUiRequest()
    {
        if (_asked.Count == 0)
            return;

        Action[] asked = [.. _asked];
        _asked.Clear();
        foreach (Action act in asked)
            act();
    }

    /// <summary>Opens the real play scene on the current level, from a start point or the playhead.</summary>
    private void StartPreview(bool fromPlayhead = false)
    {
        Action<LevelDocument, string, long>? launch = fromPlayhead ? _onPreviewHere : _onPreview;
        if (_editor.IsNull || launch is null)
            return;

        // The play scene runs on the song's clock: without one there is nothing to preview.
        if (!HasPlayableSong())
        {
            SetStatus("Load a song before previewing");
            return;
        }

        // Preview rebuilds the editor from its in-memory level when it returns.
        // Saving first makes that copy the clean state, so the autosave file is
        // cleared instead of being offered back as a recovery on every preview.
        if (!SaveCurrent(out string path))
            return;

        LevelDocument document = LevelDocumentMapper.Capture(_entities, _editor);
        long playheadMs = _editor.GetComponent<Playhead>().TimeMs;
        launch(document, path, playheadMs);
    }

    /// <summary>Whether the level has a song opened from the library.</summary>
    private bool HasPlayableSong() => _editor.GetComponent<LevelInfo>().Song is not null;
}
