using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Friflo.Engine.ECS;

namespace Xanadu.Ecs;

/// <summary>
/// What pressing an entity does: one value of a feature's own command enum, plus
/// optional arguments. Every feature's buttons share this one component, so a
/// new kind of button is a new enum, not a new component. <see cref="Scope"/>
/// says which enum <see cref="Id"/> belongs to, so each system only sees its own.
/// Build one with <see cref="Of{T}"/> and read it with <see cref="Is{T}"/>.
/// </summary>
public struct UiCommand : IComponent
{
    /// <summary>Which enum <see cref="Id"/> is a value of; see <see cref="UiCommandScope{T}"/>.</summary>
    public int Scope;

    /// <summary>The enum value, as its integer.</summary>
    public int Id;

    /// <summary>A number the command acts on, such as a row, slot or step.</summary>
    public int Arg;

    /// <summary>Text the command acts on, such as a path or profile id.</summary>
    public string Text;

    /// <summary>The panel or control the command belongs to, when there can be more than one.</summary>
    public Entity Owner;

    public static UiCommand Of<T>(T id, int arg = 0, string text = "", Entity owner = default)
        where T : struct, Enum =>
        new()
        {
            Scope = UiCommandScope<T>.Id,
            Id = ToInt(id),
            Arg = arg,
            Text = text,
            Owner = owner,
        };

    /// <summary>Whether this is one of <typeparamref name="T"/>'s commands, and which.</summary>
    public readonly bool Is<T>(out T id)
        where T : struct, Enum
    {
        if (Scope != UiCommandScope<T>.Id)
        {
            id = default;
            return false;
        }

        int value = Id;
        id = Unsafe.As<int, T>(ref value);
        return true;
    }

    /// <summary>The command as one of <typeparamref name="T"/>'s values; default when it is another enum's.</summary>
    public readonly T As<T>()
        where T : struct, Enum =>
        Is(out T id) ? id : default;

    /// <summary>Whether this is exactly <paramref name="id"/>.</summary>
    public readonly bool Is<T>(T id)
        where T : struct, Enum =>
        Scope == UiCommandScope<T>.Id && Id == ToInt(id);

    /// <summary>
    /// Every entity carrying one of <typeparamref name="T"/>'s commands, hidden
    /// ones too, optionally only those of <paramref name="owner"/>.
    /// </summary>
    public static List<(Entity Entity, T Id, UiCommand Command)> All<T>(EntityStore store, Entity owner = default)
        where T : struct, Enum
    {
        var found = new List<(Entity, T, UiCommand)>();
        int scope = UiCommandScope<T>.Id;
        foreach (var (commands, chunk) in Everything(store).Chunks)
        {
            Span<UiCommand> span = commands.Span;
            for (int i = 0; i < span.Length; i++)
            {
                UiCommand command = span[i];
                if (command.Scope != scope || (!owner.IsNull && command.Owner.Id != owner.Id))
                    continue;

                command.Is(out T id);
                found.Add((chunk.EntityAt(i), id, command));
            }
        }

        return found;
    }

    /// <summary>The first entity carrying exactly <paramref name="id"/>, hidden or not; null if none.</summary>
    public static Entity Find<T>(EntityStore store, T id)
        where T : struct, Enum
    {
        foreach (var (commands, chunk) in Everything(store).Chunks)
        {
            Span<UiCommand> span = commands.Span;
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i].Is(id))
                    return chunk.EntityAt(i);
            }
        }

        return default;
    }

    /// <summary>Every command entity, hidden ones included: kept per store, as systems ask every frame.</summary>
    private static ArchetypeQuery<UiCommand> Everything(EntityStore store) =>
        Queries.GetValue(store, static s => s.Query<UiCommand>().WithDisabled());

    private static readonly ConditionalWeakTable<EntityStore, ArchetypeQuery<UiCommand>> Queries = new();

    private static int ToInt<T>(T id)
        where T : struct, Enum
    {
        if (Unsafe.SizeOf<T>() != sizeof(int))
            throw new NotSupportedException($"{typeof(T).Name} must be int-backed to be a command.");

        return Unsafe.As<T, int>(ref id);
    }
}

/// <summary>Hands each command enum its own <see cref="UiCommand.Scope"/>, once per run.</summary>
public static class UiCommandScope<T>
    where T : struct, Enum
{
    public static readonly int Id = UiCommandScopes.Next();
}

internal static class UiCommandScopes
{
    private static int _last;

    public static int Next() => Interlocked.Increment(ref _last);
}
