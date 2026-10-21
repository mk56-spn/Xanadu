using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Writer.Ecs;
using LayerComponent = Writer.Ecs.Layer;
using Position = Writer.Ecs.Position;

namespace Writer.Ui;

/// <summary>Chainable builder for layers, containers, and rectangles.</summary>
public sealed class UiBuilder
{
    private static readonly Color DefaultContainerColor = new("#1e293b");

    private readonly EntityStore _entities;
    private readonly IVisualRidFactory _visuals;
    private readonly Stack<Entity> _parents = new();

    private Entity _current;
    private int _layerZIndex;

    public UiBuilder(EntityStore entities, IVisualRidFactory visuals)
    {
        _entities = entities;
        _visuals = visuals;
    }

    public static UiBuilder Begin(EntityStore entities, IVisualRidFactory? visuals = null) =>
        new(entities, visuals ?? new GodotVisualRidFactory());

    /// <summary>The entity currently receiving children.</summary>
    public Entity Current => _current;

    public UiBuilder Layer(int zIndex)
    {
        var layer = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new LayerComponent(zIndex),
            Tags.Get<Uninitialised>());

        _layerZIndex = zIndex;
        _parents.Clear();
        _current = layer;
        return this;
    }

    public UiBuilder Container(
        Vector2 position,
        Vector2 size,
        StackDirection direction = StackDirection.Horizontal,
        float spacing = 0,
        bool clip = false,
        Color? color = null,
        string? text = null,
        bool takesInput = false)
    {
        RequireLayer();

        Tags tags = takesInput
            ? Tags.Get<Uninitialised, Dirty, LayoutDirty, TakesInput>()
            : Tags.Get<Uninitialised, Dirty, LayoutDirty>();

        var container = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(_layerZIndex),
            new Position(position),
            new FillColor(color ?? DefaultContainerColor),
            new UiContainer(direction, spacing, clip),
            tags);

        if (text is not null)
            container.AddComponent(new Text(text));

        Attach(container);
        _parents.Push(_current);
        _current = container;
        return this;
    }

    public UiBuilder Rect(
        Vector2 position,
        Vector2 size,
        Color color,
        string? text = null,
        bool takesInput = false)
    {
        RectEntity(position, size, color, text, takesInput);
        return this;
    }

    /// <summary>Creates a rectangle and returns its entity for further setup.</summary>
    public Entity RectEntity(
        Vector2 position,
        Vector2 size,
        Color color,
        string? text = null,
        bool takesInput = false)
    {
        RequireLayer();

        Tags tags = takesInput
            ? Tags.Get<Uninitialised, Dirty, TakesInput>()
            : Tags.Get<Uninitialised, Dirty>();

        var rect = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(_layerZIndex),
            new Position(position),
            new FillColor(color),
            tags);

        if (text is not null)
            rect.AddComponent(new Text(text));

        Attach(rect);
        return rect;
    }

    public UiBuilder End()
    {
        if (_parents.Count > 0)
            _current = _parents.Pop();

        return this;
    }

    private void Attach(Entity child)
    {
        _current.AddChild(child);

        if (_current.HasComponent<UiContainer>())
            _current.AddTag<LayoutDirty>();
    }

    private void RequireLayer()
    {
        if (_current.IsNull)
            throw new InvalidOperationException("Call Layer(...) before adding UI entities.");
    }
}
