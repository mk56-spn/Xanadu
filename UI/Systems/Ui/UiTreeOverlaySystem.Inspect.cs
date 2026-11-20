using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Friflo.Engine.ECS;
using Xanadu.Core.Maths;
using Xanadu.Ui.Ecs;
using Xanadu.Ui.Ecs.Ui;
using Xanadu.Ui.Ecs.Ui.Tree;
using Xanadu.Ui.Systems.Input;
using Position = Xanadu.Core.Position;

namespace Xanadu.Ui.Systems.Ui;

/// <summary>Picking what is under the cursor, and the inspector's live listing of the inspected entity.</summary>
public sealed partial class UiTreeOverlaySystem
{
    private readonly List<(Entity Entity, int Z)> _hits = new();
    private readonly List<(Label Name, Label Value)> _propRows = new();
    private readonly List<(string Name, string Value)> _props = new();
    private readonly Dictionary<Type, MemberInfo[]> _members = new();

    // ---- picking --------------------------------------------------------------------

    /// <summary>
    /// What a click at <paramref name="point"/> would land on, as osu picks: the
    /// topmost thing that draws (text, an image, a filled box), and a container only
    /// where nothing drawn inside it is under the point.
    /// </summary>
    private Entity PickAt(Vector2 point)
    {
        if (float.IsNaN(point.X))
            return default;

        _hits.Clear();
        Query.EachEntity(new HitsAt(Query.Store, _hits, point));

        Entity best = default;
        int bestZ = 0;
        bool bestDraws = false;
        foreach ((Entity entity, int z) in _hits)
        {
            if (IsDebug(entity) || !IsShown(entity) || InputSystem.IsClippedOut(entity, point))
                continue;

            bool draws = Draws(entity);
            bool better = best.IsNull ||
                          (draws && !bestDraws) ||
                          (draws == bestDraws && (z > bestZ || (z == bestZ && UiTreeOrder.IsPaintedAfter(entity, best))));
            if (!better)
                continue;

            best = entity;
            bestZ = z;
            bestDraws = draws;
        }
        return best;
    }

    /// <summary>Whether an entity puts something visible on screen itself, rather than only holding others.</summary>
    private static bool Draws(Entity entity)
    {
        if (entity.TryGetComponent(out Text text) && !string.IsNullOrWhiteSpace(text.Value))
            return true;
        if (entity.HasComponent<UiImage>() || entity.HasComponent<ButtonVisual>())
            return true;
        if (entity.HasComponent<UiContainer>())
            return false;
        return entity.TryGetComponent(out FillColor fill) && fill.Value.A > 0.01f;
    }

    // ---- the inspector --------------------------------------------------------------

    /// <summary>The inspected entity's state, read again every frame so it is live.</summary>
    private void ShowInspector()
    {
        _props.Clear();
        Entity entity = _inspected;
        if (entity.IsNull)
        {
            _inspectTitle.Node.SetText(_picking ? "POINT AT SOMETHING AND CLICK IT" : "CLICK A ROW'S NAME TO INSPECT IT");
        }
        else
        {
            _inspectTitle.Node.SetText(Name(entity));
            Describe(entity);
        }

        UiList view = _propList.Node.Entity.GetComponent<UiList>();
        if (view.Count != _props.Count)
            UiLists.SetCount(_propList, _props.Count, keepPlace: true);
        FillProps();
    }

    private void FillProps()
    {
        if (_propList is null || _propList.Node.IsNull)
            return;

        UiList view = _propList.Node.Entity.GetComponent<UiList>();
        for (int i = 0; i < view.Shown && i < _propRows.Count; i++)
        {
            int index = view.First + i;
            (string name, string value) = index < _props.Count ? _props[index] : ("", "");
            _propRows[i].Name.Node.SetText(name);
            _propRows[i].Value.Node.SetText(value);
        }
    }

    private void Describe(Entity entity)
    {
        if (entity.TryGetComponent(out Position position))
        {
            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            _props.Add(("on screen", Format(world)));
        }
        _props.Add(("shown", IsShown(entity) ? "yes" : "no — it or a parent is hidden"));
        _props.Add(("children", entity.ChildCount.ToString(CultureInfo.InvariantCulture)));

        var tags = new StringBuilder();
        foreach (TagType tag in entity.Tags)
        {
            if (tag.Type == typeof(UiTreeOverlaySelected) || tag.Type == typeof(UiTreeOverlayHovered))
                continue;
            tags.Append(tags.Length > 0 ? ", " : "").Append(tag.Name);
        }
        _props.Add(("tags", tags.Length > 0 ? tags.ToString() : "none"));

        foreach (EntityComponent component in entity.Components)
        {
            if (component.Type.Type == typeof(TreeNode))
                continue;

            object? value = component.Value;
            MemberInfo[] members = Members(component.Type.Type);
            _props.Add(("▪ " + component.Type.Name, members.Length == 0 ? Format(value) : ""));
            foreach (MemberInfo member in members)
                _props.Add(("    " + member.Name, Format(Read(member, value))));
        }
    }

    /// <summary>A component's public fields and plain properties, looked up once per type.</summary>
    private MemberInfo[] Members(Type type)
    {
        if (_members.TryGetValue(type, out MemberInfo[]? members))
            return members;

        var list = new List<MemberInfo>();
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            list.Add(field);
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length == 0 && property.CanRead && property.DeclaringType == type)
                list.Add(property);
        }
        return _members[type] = list.ToArray();
    }

    private static object? Read(MemberInfo member, object? owner)
    {
        if (owner is null)
            return null;

        try
        {
            return member switch
            {
                FieldInfo field => field.GetValue(owner),
                PropertyInfo property => property.GetValue(owner),
                _ => null,
            };
        }
        catch (Exception error)
        {
            return error.GetBaseException().GetType().Name;
        }
    }

    private static string Format(object? value) => value switch
    {
        null => "null",
        string text => $"\"{(text.Length > 80 ? text[..79] + "…" : text.ReplaceLineEndings("⏎"))}\"",
        Entity entity => entity.IsNull ? "none" : $"#{entity.Id}",
        float number => number.ToString("0.###", CultureInfo.InvariantCulture),
        double number => number.ToString("0.###", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        Vector2 v => string.Create(CultureInfo.InvariantCulture, $"{v.X:0.##}, {v.Y:0.##}"),
        Rect2 r => string.Create(CultureInfo.InvariantCulture,
            $"at {r.Position.X:0.##}, {r.Position.Y:0.##}  size {r.Size.X:0.##}×{r.Size.Y:0.##}"),
        Color c => string.Create(CultureInfo.InvariantCulture, $"{c.R:0.##} {c.G:0.##} {c.B:0.##}  alpha {c.A:0.##}"),
        ICollection items => $"{items.Count} items",
        IEnumerable => "sequence",
        _ => Shorten(value.ToString() ?? ""),
    };

    private readonly record struct HitsAt(EntityStore Store, List<(Entity Entity, int Z)> Hits, Vector2 Point) : IEachEntity<Rectangle, Position>
    {
        public void Execute(ref Rectangle rect, ref Position position, int id)
        {
            Entity entity = Store.GetEntityById(id);

            if (rect.Bounds.Size.X <= 0 || rect.Bounds.Size.Y <= 0)
                return;

            Vector2 world = UiTransform.WorldPosition(entity, position.Value);
            if (!new Rect2(world + rect.Bounds.Position, rect.Bounds.Size).HasPoint(Point))
                return;

            Hits.Add((entity, entity.TryGetComponent(out Drawable drawable) ? drawable.ZIndex : 0));
        }
    }
}
