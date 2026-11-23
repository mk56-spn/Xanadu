using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Model.Ecs.Components;
using Xanadu.Model.Editor;
using Xanadu.Model.Levels;

namespace Xanadu.Logic.Editor.Kinds;

/// <summary>
/// The parts of an <see cref="ItemKind"/> that follow from its component
/// <typeparamref name="T"/> and its document <typeparamref name="TDoc"/>:
/// finding, copying, restoring, sorting and mapping to and from the level
/// document. A kind writes how its value maps to its document and back, and
/// how it sorts.
/// </summary>
public abstract class ItemKind<T, TDoc> : ItemKind
    where T : struct, IComponent
    where TDoc : class
{
    public override bool Has(Entity entity) => !entity.IsNull && entity.HasComponent<T>();

    public override EntityList All(EntityStore entities) => entities.Query<T>().ToEntityList();

    protected static T Get(Entity entity) => entity.GetComponent<T>();

    protected static ref T Ref(Entity entity) => ref entity.GetComponent<T>();

    // --- copies -------------------------------------------------------------

    protected sealed override object Capture(Entity entity) => Detach(entity.GetComponent<T>());

    /// <summary>A value that shares nothing with the entity's; only kinds holding references override it.</summary>
    protected virtual T Detach(T value) => value;

    protected sealed override Entity Create(EntityStore entities, object value) =>
        EditorItems.Create(entities, Detach((T)value));

    public sealed override int Compare(object a, object b) => Compare((T)a, (T)b);

    protected abstract int Compare(in T a, in T b);

    public sealed override bool Same(object a, object b) => Same((T)a, (T)b);

    protected virtual bool Same(in T a, in T b) => EqualityComparer<T>.Default.Equals(a, b);

    public sealed override object? Pasted(object value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        Pasted((T)value, offset, timeMs, playfield, entities);

    /// <summary>The value moved for a paste, or null when it can't go there.</summary>
    protected abstract T? Pasted(T value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities);

    // --- the level document -------------------------------------------------

    /// <summary>The level's list of this kind.</summary>
    protected abstract List<TDoc> List(LevelDocument level);

    /// <summary>The item as a document.</summary>
    protected abstract TDoc ToDocument(in T value);

    /// <summary>The value a document describes.</summary>
    protected abstract T FromDocument(TDoc document);

    /// <summary>Orders the document's list.</summary>
    protected virtual int CompareDocuments(TDoc a, TDoc b) => 0;

    public override void CaptureInto(EntityStore entities, LevelDocument level)
    {
        List<TDoc> list = List(level);
        foreach (Entity entity in All(entities))
            list.Add(ToDocument(entity.GetComponent<T>()));
        list.Sort(CompareDocuments);
    }

    public override void ApplyFrom(EntityStore entities, LevelDocument level)
    {
        DeleteAll(entities);
        foreach (TDoc document in List(level))
            EditorItems.Create(entities, FromDocument(document));
    }

    // --- shared orders ------------------------------------------------------

    protected static int ByPosition(Vector2 a, Vector2 b)
    {
        int x = a.X.CompareTo(b.X);
        return x != 0 ? x : a.Y.CompareTo(b.Y);
    }

    protected static int ByTimeThenPosition(long timeA, Vector2 a, long timeB, Vector2 b)
    {
        int time = timeA.CompareTo(timeB);
        return time != 0 ? time : ByPosition(a, b);
    }

    protected static int ByXThenY(float ax, float ay, float bx, float by)
    {
        int x = ax.CompareTo(bx);
        return x != 0 ? x : ay.CompareTo(by);
    }
}
