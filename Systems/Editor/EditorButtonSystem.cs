using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Writer.Ecs;
using Writer.Editor.Commands;
using Writer.Settings;

namespace Writer.Systems.Editor;

/// <summary>Executes the ECS editor toolbar and keeps its labels current.</summary>
public sealed class EditorButtonSystem : QuerySystem<EditorButton>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly HashSet<int> _held = new();

    public EditorButtonSystem(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<Playhead, EditorMetronome, EditorUiRequest>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        CommandBuffer buffer = CommandBuffer;
        UpdateLabels(editor, buffer);

        Query.ForEachEntity((ref button, entity) =>
        {
            bool pressed = entity.Tags.Has<Pressed>();
            bool wasPressed = _held.Contains(entity.Id);

            if (pressed)
                _held.Add(entity.Id);
            else
                _held.Remove(entity.Id);

            if (!pressed || wasPressed)
                return;

            Apply(button.Kind, editor);
        });
    }

    private void UpdateLabels(Entity editor, CommandBuffer buffer)
    {
        Playhead playhead = editor.GetComponent<Playhead>();
        EditorMetronome metronome = editor.GetComponent<EditorMetronome>();
        EditorPreview preview = editor.HasComponent<EditorPreview>()
            ? editor.GetComponent<EditorPreview>()
            : default;
        bool mineMode = editor.HasComponent<EditorMineMode>() &&
                        editor.GetComponent<EditorMineMode>().Active;

        foreach (Entity entity in _entities.Query<EditorButton, Text>().ToEntityList())
        {
            EditorButtonKind kind = entity.GetComponent<EditorButton>().Kind;
            string label = kind switch
            {
                EditorButtonKind.Play => playhead.Playing ? "II" : "▶",
                EditorButtonKind.Preview => preview.Active ? "Exit Preview" : "Preview",
                EditorButtonKind.Metronome => metronome.Enabled ? "Click On" : "Click Off",
                EditorButtonKind.AddMine => mineMode ? "Mine: On" : "Mine: Off",
                _ => entity.GetComponent<Text>().Value,
            };

            if (entity.GetComponent<Text>().Value == label)
                continue;

            entity.GetComponent<Text>().Value = label;
            buffer.AddTag<Dirty>(entity.Id);
        }
    }

    private void Apply(EditorButtonKind kind, Entity editor)
    {
        switch (kind)
        {
            case EditorButtonKind.Play:
                _commands.Enqueue(EditorCommand.TogglePlayback());
                break;

            case EditorButtonKind.Stop:
                _commands.Enqueue(EditorCommand.StopPlayback());
                break;

            case EditorButtonKind.Preview:
                _commands.Enqueue(
                    editor.GetComponent<EditorPreview>().Active
                        ? EditorCommand.EndPreview()
                        : EditorCommand.BeginPreview());
                break;

            case EditorButtonKind.Metronome:
                _commands.Enqueue(EditorCommand.SetMetronome(
                    !editor.GetComponent<EditorMetronome>().Enabled));
                break;

            case EditorButtonKind.AddMine:
                if (editor.HasComponent<EditorMineMode>())
                {
                    ref EditorMineMode mode =
                        ref editor.GetComponent<EditorMineMode>();
                    mode.Active = !mode.Active;
                }
                break;

            case EditorButtonKind.ZoomIn:
                _commands.Enqueue(EditorCommand.ZoomTimeline(1));
                break;

            case EditorButtonKind.ZoomOut:
                _commands.Enqueue(EditorCommand.ZoomTimeline(-1));
                break;

            case EditorButtonKind.ToggleMetadata:
            case EditorButtonKind.Save:
            case EditorButtonKind.SaveAs:
            case EditorButtonKind.LoadLevel:
            case EditorButtonKind.LoadSong:
                Request(kind, editor);
                break;
        }
    }

    private static void Request(EditorButtonKind kind, Entity editor)
    {
        if (!editor.HasComponent<EditorUiRequest>())
            return;

        editor.GetComponent<EditorUiRequest>().Kind = kind;
    }
}
