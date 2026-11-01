using Friflo.Engine.ECS;
using Xanadu.Ecs;

namespace Xanadu.Scenes;

/// <summary>Builds the backgrounds that scenes share, outside any scene root.</summary>
public static class SceneBackgrounds
{
    /// <summary>Builds the layer for a background; it has no parent scene to die with.</summary>
    public static Entity Build(
        EntityStore entities,
        SceneBackgroundKind kind,
        IVisualRidFactory? visuals = null)
    {
        UiNode ui = UiNode.Layer(entities, default, UiLayers.MenuBackground, visuals);
        ui.Entity.AddComponent(new SceneBackground(kind));

        Entity fill = ui.Rect(SceneUi.BackgroundColor).Cover().Below().Entity;
        bool loadAssets = visuals is GodotVisualRidFactory;
        if (SongSelectScene.NoteMaterial(loadAssets, aspect: 1920f / 1080f, zoom: 1f) is { } material)
        {
            fill.AddComponent(new UiMaterial(material));
            fill.AddComponent(new SpectrumEnergy());
        }

        return ui.Entity;
    }
}
