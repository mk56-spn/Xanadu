using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Model.Ecs.Components;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Logic.Editor;
using Xanadu.Logic.Editor.Commands;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Editor;

/// <summary>Shows the ECS metadata panel and syncs its text fields to editor state.</summary>
public sealed class EditorMetadataSystem : QuerySystem<LevelInfo, Playhead>
{
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;

    public EditorMetadataSystem(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<LevelInfo, Playhead>(_entities);

        Entity panel = EditorPanel.Find(_entities, EditorPanelKind.Metadata);
        if (editor.IsNull || panel.IsNull)
            return;

        if (editor.HasComponent<EditorUiRequest>())
        {
            ref EditorUiRequest request = ref editor.GetComponent<EditorUiRequest>();
            if (request.Kind == EditorButtonKind.ToggleMetadata)
            {
                request.Kind = EditorButtonKind.None;
                if (panel.HasComponent<Position>())
                {
                    panel.GetComponent<Position>().Value = panel.GetComponent<EditorPanel>().Home;
                    CommandBuffer buffer = CommandBuffer;
                    buffer.AddTag<Moved>(panel.Id);
                    UiLayout.SetHidden(panel, buffer, !UiLayout.IsHidden(panel));
                }
            }
        }

        LevelInfo level = editor.GetComponent<LevelInfo>();

        foreach (Entity entity in _entities
                     .Query<EditorField, TextField>()
                     .WithDisabled()
                     .Entities)
        {
            EditorField binding = entity.GetComponent<EditorField>();
            ref TextField field = ref entity.GetComponent<TextField>();
            bool focused = entity.Tags.Has<TextFieldFocused>();

            if (!focused)
            {
                string expected = binding.Kind switch
                {
                    EditorFieldKind.LevelName => level.Name,
                    EditorFieldKind.Artist => level.Artist,
                    EditorFieldKind.Difficulty => level.Difficulty,
                    _ => "",
                };
                if (field.Value != expected)
                    field.Value = expected;
                continue;
            }

            switch (binding.Kind)
            {
                case EditorFieldKind.LevelName:
                    if (field.Value != level.Name)
                        _commands.Enqueue(EditorCommand.SetName(field.Value));
                    break;
                case EditorFieldKind.Artist:
                    if (field.Value != level.Artist)
                        _commands.Enqueue(EditorCommand.SetArtist(field.Value));
                    break;
                case EditorFieldKind.Difficulty:
                    if (field.Value != level.Difficulty)
                        _commands.Enqueue(EditorCommand.SetDifficulty(field.Value));
                    break;
            }
        }
    }

}
