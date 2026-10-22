using Friflo.Engine.ECS;
using Godot;

namespace Writer.Ecs;

/// <summary>
/// Generic dropdown state attached to its button entity.
/// The list is an indexed link so deleting it cleans up the dropdown.
/// </summary>
public struct Dropdown(
    Entity list,
    int selected,
    Vector2? listSize = null) : ILinkComponent
{
    public Entity List = list;
    public int Selected = selected;
    public bool Open;
    public Vector2 ListSize = listSize ?? Vector2.Zero;

    public readonly Entity GetIndexedValue() => List;
}

/// <summary>
/// One selectable option inside a dropdown, linked to its owning button.
/// </summary>
public struct DropdownOption(Entity owner, int value) : ILinkComponent
{
    public Entity Owner = owner;
    public int Value = value;

    public readonly Entity GetIndexedValue() => Owner;
}
