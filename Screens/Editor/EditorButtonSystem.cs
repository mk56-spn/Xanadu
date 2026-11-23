using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Editor;

/// <summary>Executes the ECS editor toolbar and keeps its labels current.</summary>
public sealed class EditorButtonSystem : QuerySystem<UiCommand>
{
    private readonly EntityStore _entities;
    private readonly List<IIntent> _asked = [];
    private readonly ButtonPress _presses = new();

    public EditorButtonSystem(EntityStore entities)
    {
        _entities = entities;

        // A press can land on a control that hides itself the same frame.
        Filter.WithDisabled();
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<Playhead, EditorMetronome>(_entities);

        if (editor.IsNull)
            return;

        CommandBuffer buffer = CommandBuffer;
        UpdateLabels(editor, buffer);

        Query.EachEntity(new PressButtons(this, Query.Store, editor));

        // Asked after the loop: a listener may show or hide panels.
        foreach (IIntent intent in _asked)
            intent.EmitOn(editor);
        _asked.Clear();
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

    private void Apply(EditorButtonKind kind, int arg, Entity editor)
    {
        switch (kind)
        {
            case EditorButtonKind.GroupPick:
                editor.EmitSignal(new PickGroup(arg));
                break;

            case EditorButtonKind.GroupNew:
                editor.EmitSignal(new NewGroup());
                break;

            case EditorButtonKind.GroupAdd:
                editor.EmitSignal(new AddToGroup());
                break;

            case EditorButtonKind.GroupRemove:
                editor.EmitSignal(new RemoveFromGroup());
                break;

            case EditorButtonKind.GroupDelete:
                editor.EmitSignal(new DeleteGroup());
                break;

            case EditorButtonKind.GroupLoop when arg != 0:
                editor.EmitSignal(new StepGroupLoop(arg));
                break;

            case EditorButtonKind.GroupPage when arg != 0:
                editor.GetComponent<EditorGroupEdit>().FirstRow += arg * EditorGroupPanel.Rows;
                break;

            case EditorButtonKind.GroupOneMember:
                editor.EmitSignal(new ToggleOneMember());
                break;

            case EditorButtonKind.GroupEase:
                editor.EmitSignal(new ShowGroupEase(!editor.GetComponent<EditorGroupEdit>().Easing));
                break;

            case EditorButtonKind.GroupDeleteKeys:
                editor.EmitSignal(new DeleteGroupKeys());
                break;

            case EditorButtonKind.Play:
                editor.EmitSignal(new TogglePlayback());
                break;

            case EditorButtonKind.Stop:
                editor.EmitSignal(new StopPlayback());
                break;

            case EditorButtonKind.Metronome:
                editor.EmitSignal(new SetMetronome(!editor.GetComponent<EditorMetronome>().Enabled));
                break;

            case EditorButtonKind.ZoomIn:
                editor.EmitSignal(new ZoomTimeline(1));
                break;

            case EditorButtonKind.ZoomOut:
                editor.EmitSignal(new ZoomTimeline(-1));
                break;

            case EditorButtonKind.Undo:
                editor.EmitSignal(new Undo());
                break;

            case EditorButtonKind.Redo:
                editor.EmitSignal(new Redo());
                break;

            // Screen work: these listeners show or hide panels, so they hear it after the loop.
            case EditorButtonKind.Preview:
                _asked.Add(new PreviewLevel(FromPlayhead: false));
                break;

            case EditorButtonKind.PreviewHere:
                _asked.Add(new PreviewLevel(FromPlayhead: true));
                break;

            case EditorButtonKind.ToggleMetadata:
                _asked.Add(new ToggleLevelInfo());
                break;

            case EditorButtonKind.Save:
                _asked.Add(new SaveLevel());
                break;

            case EditorButtonKind.LoadSong:
                _asked.Add(new OpenSongPicker());
                break;
        }
    }

    private readonly record struct PressButtons(EditorButtonSystem System, EntityStore Store, Entity Editor) : IEachEntity<UiCommand>
    {
        public void Execute(ref UiCommand button, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (button.Is(out EditorButtonKind kind) && System._presses.Started(entity))
                System.Apply(kind, button.Arg, Editor);
        }
    }
}
