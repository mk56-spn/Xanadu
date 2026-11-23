using System;
using System.Globalization;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Core;
using Xanadu.Core.Maths;
using Xanadu.Logic.Editor;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor.Intents;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Core.Position;

namespace Xanadu.Screens.Editor;

/// <summary>Shows the ECS metadata panel and syncs its text fields to editor state.</summary>
public sealed class EditorMetadataSystem(EntityStore entities) : QuerySystem<LevelInfo, Playhead>
{
    private readonly EntityStore _entities = entities;

    /// <summary>LEVEL INFO (<see cref="ToggleLevelInfo"/>): shows the panel at its home, or hides it.</summary>
    public static void Toggle(Entity panel)
    {
        if (panel.IsNull || !panel.HasComponent<Position>())
            return;

        panel.GetComponent<Position>().Value = panel.GetComponent<EditorPanel>().Home;
        panel.AddTag<Moved>();
        UiLayout.SetHidden(panel, !UiLayout.IsHidden(panel));
    }

    protected override void OnUpdate()
    {
        Entity editor = EditorSceneQuery.First<LevelInfo, Playhead>(_entities);

        Entity panel = EditorPanel.Find(_entities, EditorPanelKind.Metadata);
        if (editor.IsNull || panel.IsNull)
            return;

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
                        editor.EmitSignal(new SetLevelName(field.Value));
                    break;
                case EditorFieldKind.Artist:
                    if (field.Value != level.Artist)
                        editor.EmitSignal(new SetLevelArtist(field.Value));
                    break;
                case EditorFieldKind.Difficulty:
                    if (field.Value != level.Difficulty)
                        editor.EmitSignal(new SetLevelDifficulty(field.Value));
                    break;
            }
        }
    }

}
