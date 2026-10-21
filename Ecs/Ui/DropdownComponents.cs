using Friflo.Engine.ECS;

namespace Writer.Ecs;

/// <summary>Generic dropdown state attached to its button entity.</summary>
public struct Dropdown(Entity list, int selected) : IComponent
{
    public Entity List = list;
    public int Selected = selected;
    public bool Open;
}

/// <summary>One selectable option inside a dropdown.</summary>
public struct DropdownOption(Entity owner, int value) : IComponent
{
    public Entity Owner = owner;
    public int Value = value;
}
