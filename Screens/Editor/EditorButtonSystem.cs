using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;
using Xanadu.Logic.Editor.Commands;

namespace Xanadu.Screens.Editor;

/// <summary>Executes the ECS editor toolbar and keeps its labels current.</summary>
public sealed class EditorButtonSystem : QuerySystem<UiCommand>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;
    private readonly ButtonPress _presses = new();

    public EditorButtonSystem(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<Playhead, EditorMetronome, EditorUiRequest>(_entities);

        if (editor.IsNull)
            return;

        CommandBuffer buffer = CommandBuffer;
        UpdateLabels(editor, buffer);

        Query.EachEntity(new PressButtons(this, Query.Store, editor));
    }

    private void UpdateLabels(Entity editor, CommandBuffer buffer)
    {
        Playhead playhead = editor.GetComponent<Playhead>();
        EditorMetronome metronome = editor.GetComponent<EditorMetronome>();

        foreach (Entity entity in _entities.Query<UiCommand, Text>().ToEntityList())
        {
            EditorButtonKind kind = entity.GetComponent<UiCommand>().As<EditorButtonKind>();
            string label = kind switch
            {
                EditorButtonKind.Play => playhead.Playing ? "II" : "▶",
                EditorButtonKind.Metronome => metronome.Enabled ? "Click On" : "Click Off",
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

            case EditorButtonKind.Metronome:
                _commands.Enqueue(EditorCommand.SetMetronome(
                    !editor.GetComponent<EditorMetronome>().Enabled));
                break;

            case EditorButtonKind.ZoomIn:
                _commands.Enqueue(EditorCommand.ZoomTimeline(1));
                break;

            case EditorButtonKind.ZoomOut:
                _commands.Enqueue(EditorCommand.ZoomTimeline(-1));
                break;

            case EditorButtonKind.Undo:
                _commands.Enqueue(EditorCommand.Undo());
                break;

            case EditorButtonKind.Redo:
                _commands.Enqueue(EditorCommand.Redo());
                break;

            case EditorButtonKind.Preview:
            case EditorButtonKind.PreviewHere:
            case EditorButtonKind.ToggleMetadata:
            case EditorButtonKind.Save:
            case EditorButtonKind.LoadSong:
            case EditorButtonKind.BackgroundColor:
            case EditorButtonKind.ResetBackgroundColor:
            case EditorButtonKind.OutsideColor:
            case EditorButtonKind.ResetOutsideColor:
            case EditorButtonKind.FrameColor:
            case EditorButtonKind.ResetFrameColor:
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

    private readonly record struct PressButtons(EditorButtonSystem System, EntityStore Store, Entity Editor) : IEachEntity<UiCommand>
    {
        public void Execute(ref UiCommand button, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (button.Is(out EditorButtonKind kind) && System._presses.Started(entity))
                System.Apply(kind, Editor);
        }
    }
}
