using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Ecs;
using Xanadu.Theme;
using LayerComponent = Xanadu.Ecs.Layer;
using Position = Xanadu.Ecs.Position;

namespace Xanadu.Ecs;

/// <summary>Chainable builder for layers, containers, and rectangles.</summary>
public sealed partial class UiBuilder
{
    private static readonly Color DefaultContainerColor = Palette.SurfaceAlt;

    private readonly EntityStore _entities;
    private readonly IVisualRidFactory _visuals;
    private readonly Stack<Entity> _parents = new();

    private Entity _current;
    private Entity _layer;
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
        Entity layer = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new LayerComponent(zIndex),
            Tags.Get<Uninitialised>());

        _layerZIndex = zIndex;
        _layer = layer;
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
        bool takesInput = false,
        float padding = 0,
        StackCrossAlignment cross = StackCrossAlignment.Center,
        bool autoWidth = false,
        bool autoHeight = false,
        bool anchorTop = false,
        StackMainAlignment main = StackMainAlignment.Center,
        float skew = 0,
        bool anchorLeft = false)
    {
        RequireLayer();

        Tags tags = takesInput
            ? Tags.Get<Uninitialised, Dirty, LayoutDirty, TakesInput>()
            : Tags.Get<Uninitialised, Dirty, LayoutDirty>();

        Entity container = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(_layerZIndex),
            new Position(position),
            new FillColor(color ?? DefaultContainerColor),
            new UiContainer(
                direction,
                spacing,
                clip,
                padding,
                cross,
                autoWidth,
                autoHeight,
                anchorTop,
                main,
                skew,
                anchorLeft),
            tags);

        if (text is not null)
            container.AddComponent(new Text(text));

        Attach(container);
        _parents.Push(_current);
        _current = container;
        return this;
    }

    /// <summary>An unstyled/attached text drawable.</summary>
    public Entity Text(
        string value,
        Vector2 size,
        int fontSize = UiTheme.FontSize,
        Color? color = null,
        TextAlign align = TextAlign.Center,
        Vector2? position = null)
    {
        Entity label = RectEntity(
            position ?? Vector2.Zero,
            size,
            new Color(0, 0, 0, 0),
            value);
        label.AddComponent(new TextStyle(
            fontSize,
            color ?? UiTheme.Text));
        label.AddComponent(new TextAlignment(align));
        return label;
    }

    /// <summary>A button with fill, text style, and hover/flash visual state.</summary>
    public Entity Button(
        string value,
        Vector2 size,
        Color? color = null,
        Vector2? position = null,
        int fontSize = UiTheme.FontSize)
    {
        Color normal = color ?? UiTheme.Button;
        Entity button = RectEntity(
            position ?? Vector2.Zero,
            size,
            normal,
            value,
            takesInput: true);
        button.AddComponent(new TextStyle(fontSize, UiTheme.Text));
        button.AddComponent(ButtonVisual.Create(normal));
        return button;
    }

    /// <summary>A panel container with sensible padding, spacing, and colors.</summary>
    public UiBuilder Panel(
        Vector2 position,
        Vector2 size,
        StackDirection direction = StackDirection.Vertical,
        StackCrossAlignment cross = StackCrossAlignment.Start,
        bool autoHeight = false,
        StackMainAlignment main = StackMainAlignment.Center)
    {
        return Container(
            position,
            size,
            direction,
            spacing: UiTheme.ContainerSpacing,
            color: UiTheme.Panel,
            padding: UiTheme.ContainerPadding,
            cross: cross,
            autoHeight: autoHeight,
            main: main);
    }

    /// <summary>
    /// Creates a labeled dropdown in the current parent. The list and options
    /// are created as siblings on the layer so they do not participate in the
    /// parent container's stack.
    /// </summary>
    public UiDropdownControl Dropdown(
        string label,
        IReadOnlyList<string> options,
        int selected = 0,
        Vector2? size = null,
        bool closeOthers = true,
        Color? buttonColor = null,
        Func<int, Color>? optionColor = null)
    {
        RequireLayer();

        float width = size?.X ?? 280f;
        float buttonHeight = size?.Y ?? UiTheme.ButtonHeight;
        Entity button = Button(
            label,
            new Vector2(width, buttonHeight),
            buttonColor ?? UiTheme.Button);

        return DropdownList(button, options, selected, closeOthers, optionColor);
    }

    /// <summary>
    /// Gives an existing button a dropdown list built on this builder's layer.
    /// The button can live elsewhere, such as a header, while its list floats
    /// on a higher layer and draws above the panels beneath it.
    /// </summary>
    public UiDropdownControl DropdownList(
        Entity button,
        IReadOnlyList<string> options,
        int selected = 0,
        bool closeOthers = true,
        Func<int, Color>? optionColor = null)
    {
        RequireLayer();

        float width = button.GetComponent<Rectangle>().Bounds.Size.X;
        float listHeight = Math.Max(
            4f,
            options.Count *
            (UiTheme.DropdownOptionHeight + UiTheme.DropdownOptionSpacing) + 4f);

        // Closed lists start off-screen, as the dropdown system leaves them on close.
        Entity list = RectEntity(
            UiLayout.Hidden,
            new Vector2(width, listHeight),
            UiTheme.Panel,
            takesInput: true);
        list.AddTag<LayoutIgnore>();
        list.AddTag<DropdownListPanel>();
        var optionEntities = new List<Entity>();
        for (int i = 0; i < options.Count; i++)
        {
            Color optionFill = optionColor?.Invoke(i) ?? UiTheme.DropdownOption;
            Entity option = RectEntity(
                UiLayout.Hidden,
                new Vector2(width - 12, UiTheme.DropdownOptionHeight),
                optionFill,
                options[i],
                takesInput: true);
            option.AddTag<LayoutIgnore>();
            option.AddComponent(new TextStyle(
                UiTheme.FontSize,
                UiTheme.Text));
            option.AddComponent(ButtonVisual.Create(optionFill));
            option.AddComponent(new DropdownOption(button, i));
            optionEntities.Add(option);
        }


        button.AddComponent(new Dropdown(
            list,
            selected,
            new Vector2(width, listHeight),
            closeOthers));
        return new UiDropdownControl(button, list, optionEntities);
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

        Entity rect = _entities.CreateEntity(
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

    /// <summary>Creates an image drawable backed by a texture.</summary>
    public Entity ImageEntity(
        Vector2 position,
        Vector2 size,
        Texture2D texture)
    {
        RequireLayer();

        Entity image = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(_layerZIndex),
            new Position(position),
            new UiImage(texture),
            Tags.Get<Uninitialised, Dirty>());
        Attach(image);
        return image;
    }

    /// <summary>Creates a gradient-filled rectangle with no solid FillColor.</summary>
    public Entity LinearGradientEntity(
        Vector2 position,
        Vector2 size,
        LinearGradient gradient,
        bool takesInput = false)
    {
        RequireLayer();

        Tags tags = takesInput
            ? Tags.Get<Uninitialised, Dirty, TakesInput>()
            : Tags.Get<Uninitialised, Dirty>();

        Entity entity = _entities.CreateEntity(
            new VisualRid(_visuals.CreateCanvasItem()),
            new Rectangle(new Rect2(-size / 2, size)),
            new Drawable(_layerZIndex),
            new Position(position),
            gradient,
            tags);

        Attach(entity);
        return entity;
    }

    /// <summary>Resumes adding children under an existing entity.</summary>
    public UiBuilder Use(Entity parent)
    {
        if (parent.IsNull)
            throw new ArgumentException("Parent entity is null.", nameof(parent));

        _current = parent;
        _parents.Clear();
        if (parent.TryGetComponent<LayerComponent>(out LayerComponent layer))
        {
            // Resuming on a layer itself: new entities belong to that layer.
            _layer = parent;
            _layerZIndex = layer.ZIndex;
        }
        else if (parent.TryGetComponent<Drawable>(out Drawable drawable))
        {
            _layerZIndex = drawable.ZIndex;
        }
        return this;
    }

    public UiBuilder End()
    {
        if (_parents.Count > 0)
            _current = _parents.Pop();

        return this;
    }

    /// <summary>Adds an existing entity to a container and flags the container.</summary>
    public void AddChild(Entity parent, Entity child)
    {
        parent.AddChild(child);

        if (parent.HasComponent<UiContainer>())
            parent.AddTag<LayoutDirty>();
    }

    private void Attach(Entity child)
    {
        AddChild(_current, child);
    }

    private void RequireLayer()
    {
        if (_current.IsNull)
            throw new InvalidOperationException("Call Layer(...) before adding UI entities.");
    }
}
