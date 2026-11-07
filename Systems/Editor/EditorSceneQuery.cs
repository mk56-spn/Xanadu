using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Systems.Editor;

/// <summary>
/// Reads the one editor entity and the chrome and overlays around it. Pointer
/// tools all start by asking the same questions — is an editor open, is a panel
/// under the pointer, is a modal in the way — so those live here once.
/// </summary>
public sealed class EditorSceneQuery(EntityStore entities)
{
    public EntityStore Store { get; } = entities;

    /// <summary>The first entity carrying a component, or default when there is none.</summary>
    public static Entity First<T>(EntityStore store) where T : struct, IComponent
    {
        foreach (Entity entity in store.Query<T>().ToEntityList())
            return entity;

        return default;
    }

    /// <summary>The first entity carrying both components, or default when there is none.</summary>
    public static Entity First<T1, T2>(EntityStore store)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
    {
        foreach (Entity entity in store.Query<T1, T2>().ToEntityList())
            return entity;

        return default;
    }

    /// <summary>The first entity carrying all three components, or default when there is none.</summary>
    public static Entity First<T1, T2, T3>(EntityStore store)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
    {
        foreach (Entity entity in store.Query<T1, T2, T3>().ToEntityList())
            return entity;

        return default;
    }

    /// <summary>The editor entity, or default when none is open.</summary>
    public Entity Editor()
    {
        foreach (Entity entity in Store
                     .Query<PlayfieldView, Playfield, Playhead, NoteSelection>()
                     .ToEntityList())
        {
            return entity;
        }

        return default;
    }

    public bool IsGroupTargetEditing()
    {
        foreach (Entity entity in Store.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().TargetEdit)
                return true;
        }

        return false;
    }

    /// <summary>True while a warning, the group overlay or a scene confirmation sits over the editor.</summary>
    public bool IsModalOpen()
    {
        if (Store.Query<WarningPopup>().Count > 0 || Store.Query<SceneConfirmation>().Count > 0)
            return true;

        foreach (Entity entity in Store.Query<EditorGroupState>().ToEntityList())
        {
            if (entity.GetComponent<EditorGroupState>().Open >= 0)
                return true;
        }

        return false;
    }

    /// <summary>True when a control that takes input covers the screen point.</summary>
    public bool IsOverPanel(Vector2 screenPosition)
    {
        foreach (Entity entity in Store
                     .Query<Rectangle>()
                     .AllTags(Tags.Get<TakesInput>())
                     .ToEntityList())
        {
            Vector2 local = entity.GetComponent<Position>().Value;
            Rect2 bounds = entity.GetComponent<Rectangle>().Bounds;
            Vector2 center = UiTransform.WorldPosition(entity, local);
            if (new Rect2(center + bounds.Position, bounds.Size).HasPoint(screenPosition))
                return true;
        }

        return false;
    }

    public bool IsOverNoteBar(Vector2 screenPosition)
    {
        foreach (Entity panel in Store
                     .Query<EditorNoteBarPanel, Position, Rectangle>()
                     .ToEntityList())
        {
            Vector2 center = panel.GetComponent<Position>().Value;
            Rect2 local = panel.GetComponent<Rectangle>().Bounds;
            if (new Rect2(center + local.Position, local.Size).HasPoint(screenPosition))
                return true;
        }

        return false;
    }
}
