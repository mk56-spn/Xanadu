using System.Collections.Generic;

namespace Xanadu.Logic.Screens;

/// <summary>
/// One way into a screen's logic: an entrance a layout binds controls to.
/// A mandatory entrance needs at least one control; with
/// <see cref="RequiredArgs"/> it needs one per argument (every key on the
/// rebind entrance, say). Optional ones a layout may leave out.
/// </summary>
public sealed record Entrance<T>(T Id, bool Mandatory, IReadOnlyList<int>? RequiredArgs = null, Func<int, string>? ArgName = null)
    where T : struct, Enum;

/// <summary>
/// What a screen's logic offers its layouts: every entrance, mandatory or not.
/// A layout that leaves a mandatory entrance unbound is refused when it is
/// built (see the screen host); what else it draws is its own business.
/// </summary>
public sealed class ScreenContract<T> where T : struct, Enum
{
    private readonly Dictionary<T, Entrance<T>> _entrances = [];

    public IEnumerable<Entrance<T>> Entrances => _entrances.Values;

    /// <param name="argName">How a missing argument reads in the error; its number when left out.</param>
    public ScreenContract<T> Mandatory(T id, IReadOnlyList<int>? args = null, Func<int, string>? argName = null)
    {
        _entrances[id] = new Entrance<T>(id, true, args, argName);
        return this;
    }

    public ScreenContract<T> Optional(T id)
    {
        _entrances[id] = new Entrance<T>(id, false);
        return this;
    }

    public bool Declares(T id) => _entrances.ContainsKey(id);

    /// <summary>
    /// What is wrong with a layout that bound <paramref name="bound"/>: each
    /// mandatory entrance (or required argument) missing, and each binding to
    /// an entrance the logic never declared. Empty when the layout is good.
    /// </summary>
    public List<string> Problems(IEnumerable<(T Id, int Arg)> bound)
    {
        var ids = new HashSet<T>();
        var pairs = new HashSet<(T, int)>();
        var problems = new List<string>();
        foreach ((T id, int arg) in bound)
        {
            ids.Add(id);
            pairs.Add((id, arg));
            if (!Declares(id))
                problems.Add($"{typeof(T).Name}.{id} is bound but not an entrance");
        }

        foreach (Entrance<T> entrance in _entrances.Values)
        {
            if (!entrance.Mandatory)
                continue;

            if (entrance.RequiredArgs is null)
            {
                if (!ids.Contains(entrance.Id))
                    problems.Add($"{typeof(T).Name}.{entrance.Id} is mandatory and unbound");
                continue;
            }

            foreach (int arg in entrance.RequiredArgs)
            {
                if (!pairs.Contains((entrance.Id, arg)))
                    problems.Add($"{typeof(T).Name}.{entrance.Id}({entrance.ArgName?.Invoke(arg) ?? arg.ToString()}) is mandatory and unbound");
            }
        }

        return problems;
    }
}

/// <summary>
/// A screen's logic as its layouts and host see it: the entrances it declares,
/// one way in for all of them, and the frame and close hooks. The values a
/// layout shows are the logic class's own read-only members.
/// </summary>
public interface IScreenLogic<T> where T : struct, Enum
{
    ScreenContract<T> Contract { get; }

    void Do(T entrance, int arg);

    /// <summary>Runs once a frame while the screen is up.</summary>
    void Update() { }

    /// <summary>The screen is going away, by whatever route.</summary>
    void Close() { }
}

/// <summary>Where a screen's logic may send the player; the game decides what each means.</summary>
public interface IScreenNavigator
{
    void Go(Model.Ecs.Components.SceneKind scene);

    /// <summary>The screen's own way back, as Escape would take it.</summary>
    void Back();
}
