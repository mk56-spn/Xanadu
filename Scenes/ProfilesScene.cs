using System;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Strings;
using Xanadu.Profiles;

namespace Xanadu.Scenes;

/// <summary>Local profile picker shown before the main menu.</summary>
public static class ProfilesScene
{
    public static void Build(
        EntityStore entities,
        Entity root,
        Vector2 screen,
        ProfileStore profiles,
        IVisualRidFactory? visuals = null)
    {
        UiBuilder ui = SceneUi.Layer(entities, root, SceneUi.MenuLayer, visuals);
        SceneUi.Background(ui, screen);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 110),
            new Vector2(900, 58),
            "WHO'S PLAYING?",
            42,
            SceneUi.AccentColor);

        SceneUi.Text(
            ui,
            new Vector2(screen.X / 2, 164),
            new Vector2(900, 28),
            "Pick a profile to load its saved scores.",
            18,
            SceneUi.DimTextColor);

        float startY = 246;
        for (int i = 0; i < profiles.Profiles.Count; i++)
        {
            UserProfile profile = profiles.Profiles[i];
            bool active = profile.Id == profiles.ActiveProfileId;
            Entity button = SceneUi.Button(
                ui,
                new Vector2(screen.X / 2, startY + i * 70),
                new Vector2(440, 54),
                $"{(active ? "▶  " : "")}{profile.Name}",
                SceneKind.None,
                20,
                active ? SceneUi.ButtonColor : SceneUi.MutedButtonColor);
            button.AddComponent(new ProfileSelect(profile.Id));
        }

        float createY = Math.Min(
            screen.Y - 120,
            startY + profiles.Profiles.Count * 70 + 30);
        if (profiles.Profiles.Count == 0)
        {
            SceneUi.Text(
                ui,
                new Vector2(screen.X / 2, createY + 62),
                new Vector2(900, 32),
                "No profiles yet. Create one to start playing.",
                18,
                SceneUi.DimTextColor);
        }

        Entity create = SceneUi.Button(
            ui,
            new Vector2(screen.X / 2, createY),
            new Vector2(360, 54),
            LocalizedText.Get(ProfilesStrings.NewProfile),
            SceneKind.None,
            18,
            SceneUi.ButtonColor);
        create.AddComponent(new ProfileCreate());

        UserProfile? current = profiles.ActiveProfile;
        if (current is not null)
        {
            SceneUi.Text(
                ui,
                new Vector2(screen.X / 2, screen.Y - 44),
                new Vector2(900, 24),
                $"CURRENT PROFILE  ·  {current.Name}",
                14,
                SceneUi.DimTextColor);
        }
    }
}
