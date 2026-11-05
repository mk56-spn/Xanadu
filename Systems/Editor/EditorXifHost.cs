using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Editor;
using Xanadu.Items;
using Xanadu.Levels;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Hosts the shared XIF editor in the level editor: it edits the item of the
/// selected polygon, records each edit on the editor's undo history, and
/// offers the map's shader slots and the common shaders. The scene builds the
/// panel; <see cref="EditorXifHostSystem"/> hands the host the history, which
/// belongs to the editor's systems.
/// </summary>
public sealed class EditorXifHost(EntityStore store) : IXifEditorHost
{
    private EditorSnapshot? _gesture;

    /// <summary>Where edits are recorded; until it is set they are made but not recorded.</summary>
    public EditorHistory? History { get; set; }

    /// <summary>The primary selected polygon, if that is what is selected.</summary>
    private Entity Polygon
    {
        get
        {
            foreach (Entity editor in store.Query<EditorPlacement, NoteSelection>().ToEntityList())
            {
                Entity primary = editor.GetComponent<NoteSelection>().Note;
                return !primary.IsNull && primary.HasComponent<LevelPolygon>() ? primary : default;
            }

            return default;
        }
    }

    public Xif? Item => Polygon is { IsNull: false } polygon ? polygon.GetComponent<LevelPolygon>().Item : null;

    public string Title => "Polygon";

    public IReadOnlyList<XifShaderChoice> Shaders
    {
        get
        {
            var choices = new List<XifShaderChoice> { new("NONE", new XifShaderRef()) };
            foreach (Entity editor in store.Query<LevelShaders>().ToEntityList())
            {
                ShaderSlot?[] slots = editor.GetComponent<LevelShaders>().Slots ?? Array.Empty<ShaderSlot?>();
                for (int slot = 1; slot <= slots.Length; slot++)
                {
                    if (slots[slot - 1] is { } shader)
                    {
                        string name = shader.Name.Length > 0 ? shader.Name.ToUpperInvariant() : $"SLOT {slot}";
                        choices.Add(new XifShaderChoice(name, XifShaderRef.OfMap(slot)));
                    }
                }
                break;
            }

            foreach (string path in CommonShaders.Paths)
                choices.Add(new XifShaderChoice(Path.GetFileNameWithoutExtension(path).ToUpperInvariant(), XifShaderRef.OfCommon(path)));
            return choices;
        }
    }

    public bool CanLoad(string path) => Godot.ResourceLoader.Exists(path);

    public void Edit(Action change)
    {
        EditorSnapshot? before = History?.Capture();
        change();
        if (before is { } snapshot)
            History!.Record(snapshot, History.Capture());
    }

    public void BeginGesture() => _gesture = History?.Capture();

    public void EndGesture()
    {
        if (_gesture is { } before)
            History?.Record(before, History.Capture());
        _gesture = null;
    }

    /// <summary>The editor redraws its playfield every frame, so a change shows by itself.</summary>
    public void Changed()
    {
    }
}

/// <summary>Gives the level editor's XIF panel the undo history its edits go on.</summary>
public sealed class EditorXifHostSystem(EditorHistory history) : Friflo.Engine.ECS.Systems.QuerySystem<XifEditor>
{
    protected override void OnUpdate()
    {
        foreach (Entity panel in Query.ToEntityList())
        {
            if (panel.GetComponent<XifEditor>().Host is EditorXifHost host && host.History != history)
                host.History = history;
        }
    }
}
