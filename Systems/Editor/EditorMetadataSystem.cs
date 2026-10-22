using System;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Godot;
using Writer.Ecs;
using Writer.Editor.Commands;
using Position = Writer.Ecs.Position;

namespace Writer.Systems.Editor;

/// <summary>Shows the ECS metadata panel and syncs its text fields to editor state.</summary>
public sealed class EditorMetadataSystem : QuerySystem<EditorMetadataPanel>
{
    private static readonly Vector2 Hidden = new(-10_000, -10_000);
    private readonly EntityStore _entities;
    private readonly EditorCommandBus _commands;

    public EditorMetadataSystem(EntityStore entities, EditorCommandBus commands)
    {
        _entities = entities;
        _commands = commands;
    }

    protected override void OnUpdate()
    {
        Entity editor = default;
        foreach (Entity candidate in _entities
                     .Query<EditorMetadataPanel, LevelInfo, Playhead>()
                     .ToEntityList())
        {
            editor = candidate;
            break;
        }

        if (editor.IsNull)
            return;

        ref EditorMetadataPanel panel =
            ref editor.GetComponent<EditorMetadataPanel>();

        if (editor.HasComponent<EditorUiRequest>())
        {
            ref EditorUiRequest request = ref editor.GetComponent<EditorUiRequest>();
            if (request.Kind == EditorButtonKind.ToggleMetadata)
            {
                request.Kind = EditorButtonKind.None;
                panel.Visible = !panel.Visible;
                if (!panel.Panel.IsNull &&
                    panel.Panel.HasComponent<Position>())
                {
                    ref Position position =
                        ref panel.Panel.GetComponent<Position>();
                    position.Value = panel.Visible ? panel.Home : Hidden;
                    CommandBuffer buffer = CommandBuffer;
                    buffer.AddTag<Moved>(panel.Panel.Id);
                }
            }
        }

        LevelInfo level = editor.GetComponent<LevelInfo>();
        Playhead playhead = editor.GetComponent<Playhead>();

        foreach (Entity entity in _entities
                     .Query<EditorField, TextField>()
                     .ToEntityList())
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
                    EditorFieldKind.Bpm => playhead.Bpm.ToString(),
                    EditorFieldKind.Offset => playhead.OffsetMs.ToString(),
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
                case EditorFieldKind.Bpm:
                    if (int.TryParse(field.Value, out int bpm) &&
                        bpm != playhead.Bpm)
                    {
                        _commands.Enqueue(EditorCommand.SetBpm(bpm));
                    }
                    break;
                case EditorFieldKind.Offset:
                    if (long.TryParse(field.Value, out long offset) &&
                        offset != playhead.OffsetMs)
                    {
                        _commands.Enqueue(EditorCommand.SetOffset(offset));
                    }
                    break;
            }
        }
    }
}
