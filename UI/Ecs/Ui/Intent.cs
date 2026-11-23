using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Friflo.Engine.ECS;

namespace Xanadu.Ui.Ecs.Ui;

/// <summary>
/// A typed signal payload that says what to do: one small record per verb,
/// carrying exactly its data. Implement it through <see cref="IIntent{TSelf}"/>,
/// which sends the record as its own signal type, so a handler added with
/// <c>AddSignalHandler&lt;TSelf&gt;</c> hears it.
/// </summary>
public interface IIntent
{
    /// <summary>Sends this intent as a signal of its own type to <paramref name="target"/>.</summary>
    void EmitOn(Entity target);
}

/// <summary>An <see cref="IIntent"/> that emits as <typeparamref name="TSelf"/>.</summary>
public interface IIntent<TSelf> : IIntent
    where TSelf : struct, IIntent<TSelf>
{
    void IIntent.EmitOn(Entity target) => target.EmitSignal((TSelf)(object)this);
}

/// <summary>
/// What pressing this entity asks for. The feature that owns the button
/// decides which entity hears it (its session, its panel) and emits it once
/// per press.
/// </summary>
public struct Intent(IIntent value) : IComponent
{
    public IIntent Value = value;
}

/// <summary>Reads <see cref="Intent"/>s off entities.</summary>
public static class UiIntents
{
    /// <summary>The entity's intent when it carries one of type <typeparamref name="T"/>.</summary>
    public static bool TryGetIntent<T>(this Entity entity, out T intent)
        where T : IIntent
    {
        if (entity.TryGetComponent(out Intent carried) && carried.Value is T typed)
        {
            intent = typed;
            return true;
        }

        intent = default!;
        return false;
    }

    /// <summary>Every entity carrying an intent, hidden ones included.</summary>
    public static List<(Entity Entity, IIntent Intent)> All(EntityStore store)
    {
        var found = new List<(Entity, IIntent)>();
        foreach (var (intents, chunk) in Queries.GetValue(store, static s => s.Query<Intent>().WithDisabled()).Chunks)
        {
            Span<Intent> span = intents.Span;
            for (int i = 0; i < span.Length; i++)
                found.Add((chunk.EntityAt(i), span[i].Value));
        }

        return found;
    }

    /// <summary>Kept per store, as systems ask every frame.</summary>
    private static readonly ConditionalWeakTable<EntityStore, ArchetypeQuery<Intent>> Queries = [];
}
