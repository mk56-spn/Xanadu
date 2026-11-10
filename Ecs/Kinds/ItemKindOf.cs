using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Levels;
using Xanadu.Systems.Playfields;

namespace Xanadu.Ecs;

/// <summary>
/// The parts of an <see cref="ItemKind"/> that follow from its component
/// <typeparamref name="T"/> and its document <typeparamref name="TDoc"/>:
/// finding, copying, restoring, sorting and mapping to and from the level
/// document. A kind writes how its value maps to its document and back, and
/// how it sorts; the header is handled here for every kind.
/// </summary>
public abstract class ItemKind<T, TDoc> : ItemKind
    where T : struct, IComponent
    where TDoc : ItemDocument, new()
{
    public override bool Has(Entity entity) => !entity.IsNull && entity.HasComponent<T>();

    public override EntityList All(EntityStore entities) => entities.Query<T>().ToEntityList();

    public override void AddDrawEntries(EntityStore entities, List<PlayfieldDrawEntry> into)
    {
        if (DrawKind is not { } drawKind)
            return;

        foreach (var (_, chunk) in entities.Query<T>().Chunks)
        {
            foreach (Entity entity in chunk)
            {
                ItemLayer layer = ItemLayer.Of(entity);
                into.Add(new PlayfieldDrawEntry(layer.Layer, layer.Z, drawKind, entity));
            }
        }
    }

    protected static T Get(Entity entity) => entity.GetComponent<T>();

    protected static ref T Ref(Entity entity) => ref entity.GetComponent<T>();

    // --- copies -------------------------------------------------------------

    protected sealed override object Capture(Entity entity) => Detach(entity.GetComponent<T>());

    /// <summary>A value that shares nothing with the entity's; only kinds holding references override it.</summary>
    protected virtual T Detach(T value) => value;

    protected sealed override Entity Create(EntityStore entities, object value, in ItemHeader header)
    {
        Entity created = EditorItems.Create(entities, Made(Detach((T)value), header), header.Shader);
        Made(created);
        return created;
    }

    /// <summary>The value as it goes into a new item; start points copy their groups into it.</summary>
    protected virtual T Made(T value, in ItemHeader header) => value;

    /// <summary>Runs on every new item of this kind, however it was made.</summary>
    protected virtual void Made(Entity created) { }

    public sealed override int Compare(object a, object b) => Compare((T)a, (T)b);

    protected abstract int Compare(in T a, in T b);

    public sealed override bool Same(object a, object b) => Same((T)a, (T)b);

    protected virtual bool Same(in T a, in T b) => EqualityComparer<T>.Default.Equals(a, b);

    public sealed override object? Pasted(object value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities) =>
        Pasted((T)value, offset, timeMs, playfield, entities);

    /// <summary>The value moved for a paste, or null when it can't go there.</summary>
    protected abstract T? Pasted(T value, Vector2 offset, long timeMs, Playfield playfield, EntityStore entities);

    // --- the level document -------------------------------------------------

    /// <summary>The chart's list of this kind, made when it is missing.</summary>
    protected abstract List<TDoc> List(IChartItems chart);

    /// <summary>The item's own fields as a document; the header is written after.</summary>
    protected abstract TDoc ToDocument(in T value, Entity entity);

    /// <summary>The value a document describes.</summary>
    protected abstract T FromDocument(TDoc document);

    /// <summary>Orders the document's list; by position unless a kind is timed.</summary>
    protected virtual int CompareDocuments(TDoc a, TDoc b) => 0;

    /// <summary>Makes an item from a document; notes override it to take their shaders from their Xif.</summary>
    protected virtual Entity CreateFromDocument(EntityStore entities, TDoc document)
    {
        ItemHeader header = ItemHeader.Of(document);
        Entity created = EditorItems.Create(entities, Made(FromDocument(document), header), document.Shader);
        Made(created);
        return created;
    }

    public override void CaptureInto(EntityStore entities, IChartItems chart)
    {
        List<TDoc> list = List(chart);
        foreach (Entity entity in All(entities))
        {
            TDoc document = ToDocument(entity.GetComponent<T>(), entity);
            ItemHeader.Of(entity).WriteTo(document);
            list.Add(document);
        }

        list.Sort(CompareDocuments);
    }

    public override void ApplyFrom(EntityStore entities, IChartItems chart)
    {
        DeleteAll(entities);
        foreach (TDoc document in List(chart))
            ItemHeader.Of(document).ApplyTo(CreateFromDocument(entities, document), Tintable);
    }

    public override IEnumerable<ItemDocument> Documents(IChartItems chart) => List(chart);

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
