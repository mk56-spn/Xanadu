using System;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Labs.Experiments.Particles;

/// <summary>
/// One of a module's numbers as the panel shows it: a name, a shape (a float,
/// a float2, a switch, four switches), where it sits in the module's eight
/// numbers (the shader's <c>a</c> then <c>b</c>), the range a drag covers, and its default.
/// </summary>
internal sealed record ModuleParam(string Label, UiValueShape Shape, int Slot, (float Min, float Max) Range, float[] Default, string Tip);

/// <summary>A module kind as the panel sees it: its name, what it does, its modes and its numbers.</summary>
internal sealed record ModuleKindInfo(string Name, string Tip, string[] Modes, ModuleParam[] Params)
{
    /// <summary>The eight numbers a new module of this kind starts with.</summary>
    public float[] Defaults()
    {
        var values = new float[8];
        foreach (ModuleParam p in Params)
            p.Default.CopyTo(values, p.Slot);
        return values;
    }
}

/// <summary>
/// Every module kind, in <see cref="ModuleKind"/> order: the one table both the
/// panel and the defaults read, so a new kind is a row here and a case in the
/// shader's <c>apply</c>.
/// </summary>
internal static class ModuleKinds
{
    private static UiValueShape F => UiValueShape.Float;
    private static UiValueShape F2 => UiValueShape.Vector(2);

    private static readonly ModuleKindInfo[] All =
    [
        new("FORCE", "A steady push: gravity, wind, buoyancy.", [],
        [
            new("accel", F2, 0, (-1500, 1500), [0, 400], "px/s² added to the velocity"),
        ]),
        new("DRAG", "Slows particles down: linear for air, quadratic for fast ones.", [],
        [
            new("linear", F, 0, (0, 5), [0.5f], "share of the speed lost each second"),
            new("quadratic", F, 1, (0, 2), [0], "more loss the faster they go"),
        ]),
        new("NOISE", "A swirling curl-noise flow with no sources or sinks.", ["PUSH", "CARRY"],
        [
            new("strength", F, 0, (0, 1500), [200], "PUSH: px/s² along the flow. CARRY: the speed the flow carries them at"),
            new("scale", F, 1, (10, 800), [180], "px across one eddy"),
            new("evolve", F, 2, (0, 2), [0.2f], "how fast the flow changes in place"),
            new("grip", F, 4, (0, 20), [3], "CARRY only: how quickly particles take the flow's speed, per second"),
        ]),
        new("ATTRACT", "Pulls towards a point (negative pushes away), softened near it.", [],
        [
            new("at", F2, 0, (-640, 640), [0, 0], "px from the middle"),
            new("strength", F, 2, (-20000, 20000), [3000], "px/s² at the core; negative repels"),
            new("reach", F, 3, (0, 1500), [0], "px it reaches; 0 is everywhere"),
            new("core", F, 4, (1, 400), [80], "px over which the pull eases off"),
        ]),
        new("VORTEX", "Spins particles round a point, and pulls them in or out.", [],
        [
            new("at", F2, 0, (-640, 640), [0, 0], "px from the middle"),
            new("swirl", F, 2, (-3000, 3000), [600], "px/s² round it; negative turns the other way"),
            new("reach", F, 3, (0, 1500), [500], "px it reaches, fading out; 0 is everywhere"),
            new("pull", F, 4, (-2000, 2000), [0], "px/s² towards it"),
        ]),
        new("POINTER", "The pointer over the view pushes, pulls or swirls.", ["PUSH", "PULL", "SWIRL"],
        [
            new("strength", F, 0, (0, 20000), [4000], "px/s² right under it"),
            new("reach", F, 1, (10, 800), [160], "px it reaches"),
            new("held only", UiValueShape.Bool, 4, (0, 1), [0], "only while the button is down"),
        ]),
        new("FIELD SLOPE", "Climbs the field (positive: gather where others were) or runs down it (negative: spread out).", [],
        [
            new("strength", F, 0, (-50000, 50000), [-4000], "px/s² per unit of slope"),
            new("probe", F, 1, (1, 40), [6], "px either side it compares"),
        ]),
        new("FIELD STEER", "Physarum: looks ahead, left and right in the field and turns to the most.", [],
        [
            new("sense", F, 0, (2, 80), [14], "px ahead it looks"),
            new("angle", F, 1, (0, 1.5f), [0.45f], "radians left and right of ahead"),
            new("turn", F, 2, (0, 30), [8], "radians a second it turns"),
            new("speed", F, 3, (0, 400), [60], "px/s it keeps; 0 keeps its own"),
            new("wander", F, 4, (0, 20), [1], "random turning, radians a second"),
        ]),
        new("BOUNDS", "The view's edges: bounce off, wrap round, or die there.", ["BOUNCE", "WRAP", "KILL"],
        [
            new("bounce", F, 0, (0, 1.2f), [0.5f], "share of the speed kept off a wall"),
            new("friction", F, 1, (0, 1), [0.1f], "share of the speed along the wall lost"),
            new("inset", F, 2, (-200, 200), [0], "px the walls stand in from the edge"),
            new("walls", UiValueShape.Vector(4, UiScalar.Bool), 4, (0, 1), [1, 1, 1, 1], "which walls: X left, Y right, Z top, W bottom"),
        ]),
        new("SPEED", "Keeps speed between a least and a most.", [],
        [
            new("limits", F2, 0, (0, 2000), [0, 600], "least and most px/s"),
        ]),
    ];

    public static ModuleKindInfo Of(ModuleKind kind) => All[(int)kind - 1];

    /// <summary>Every kind's name, in order: the ADD list.</summary>
    public static string[] Names => Array.ConvertAll(All, k => k.Name);

    public static ModuleKind FromIndex(int index) => (ModuleKind)(index + 1);
}
