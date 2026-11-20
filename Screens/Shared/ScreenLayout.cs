using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using Xanadu.Logic.Screens;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Screens.Shared;

/// <summary>
/// One way of drawing a screen over its logic. It builds its own tree into the
/// layer it is handed (background, theme and all), puts the logic's entrances on
/// its controls as <see cref="UiCommand"/>s, and refreshes what it shows from
/// the logic's read-only values. The logic never sees it.
/// </summary>
public interface IScreenLayout<in TLogic>
{
    /// <summary>A short name, shown on the layout switch when a screen has several.</summary>
    string Name => GetType().Name;

    void Build(UiNode layer, TLogic logic);

    /// <summary>Runs every frame; writes only what changed.</summary>
    void Refresh(TLogic logic) { }
}

/// <summary>Raised when a layout leaves a mandatory entrance unbound or binds one that doesn't exist.</summary>
public sealed class ScreenContractException(string layout, IReadOnlyList<string> problems)
    : Exception($"Layout '{layout}' breaks its screen's contract:\n  " + string.Join("\n  ", problems))
{
    public IReadOnlyList<string> Problems { get; } = problems;
}

/// <summary>Checks a built layout against its logic's contract.</summary>
public static class ScreenBindings
{
    /// <summary>Every <typeparamref name="T"/> command under <paramref name="root"/>, with its argument.</summary>
    public static List<(T Id, int Arg)> Bound<T>(Entity root) where T : struct, Enum
    {
        var bound = new List<(T, int)>();
        Walk(root, bound);
        return bound;
    }

    /// <summary>Throws <see cref="ScreenContractException"/> when the layout under <paramref name="root"/> breaks <paramref name="contract"/>.</summary>
    public static void Check<T>(Entity root, ScreenContract<T> contract, string layout) where T : struct, Enum
    {
        List<string> problems = contract.Problems(Bound<T>(root));
        if (problems.Count > 0)
            throw new ScreenContractException(layout, problems);
    }

    private static void Walk<T>(Entity entity, List<(T, int)> bound) where T : struct, Enum
    {
        if (entity.TryGetComponent(out UiCommand command) && command.Is(out T id))
            bound.Add((id, command.Arg));

        foreach (Entity child in entity.ChildEntities)
            Walk(child, bound);
    }
}

/// <summary>Asks the host to show a screen's next layout.</summary>
public enum ScreenLayoutCommand
{
    Next,
}
