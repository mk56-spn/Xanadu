using Xanadu.Core.Timeline;

namespace Xanadu.Slang;

/// <summary>
/// What the editor's clip timeline knows about <c>xanadu.anim</c> by name: the easings in <c>ease.by</c>'s numbering
/// and their curves in C#, the play keys and events a clock can count from. The clips themselves are not read from
/// the text: they report themselves when the probe runs the code (<see cref="ClipProbes"/>).
/// </summary>
public static class SlangClips
{
    /// <summary>The <c>eases.</c> names, in <c>ease.by</c>'s numbering.</summary>
    public static readonly string[] Easings =
    [
        "linear",
        "inQuad", "outQuad", "inOutQuad",
        "inCubic", "outCubic", "inOutCubic",
        "inQuart", "outQuart", "inOutQuart",
        "inQuint", "outQuint", "inOutQuint",
        "inSine", "outSine", "inOutSine",
        "inExpo", "outExpo", "inOutExpo",
        "inCirc", "outCirc", "inOutCirc",
        "inElastic", "outElastic", "inOutElastic",
        "inBack", "outBack", "inOutBack",
        "inBounce", "outBounce", "inOutBounce",
        "smooth",
        "step",
    ];

    /// <summary>The <c>keys.</c> names, numbered as the library's <c>Key</c> (and the game's keybinds).</summary>
    public static readonly string[] PlayKeys =
    [
        "left", "right", "up", "down", "action1", "action2", "action3", "anyDirection", "anyAction",
    ];

    /// <summary>Where events start in <see cref="SlangClip.Pressed"/>'s numbering (the library's Clock does the same).</summary>
    public const int FirstEvent = 1000;

    /// <summary>
    /// The event a note's clock (<c>clock.since(notes[i])</c>) is drawn as: any
    /// judgement, since every hit or miss judges a note.
    /// </summary>
    public const int NoteJudged = 8;

    /// <summary>The <c>events.</c> names with the library's <c>Event</c> ids (the game's <c>ShaderEvent</c>; 100 and up are "either of").</summary>
    public static readonly (string Name, int Id)[] Events =
    [
        ("hit", 0), ("miss", 1), ("flawlessPlus", 2), ("flawless", 3), ("clean", 4), ("fair", 5),
        ("deficient", 6), ("terrible", 7), ("judged", 8), ("comboBroken", 9), ("dash", 10), ("momentum", 11),
        ("songStart", 12), ("countdownDone", 13), ("death", 14), ("barrierPaid", 15), ("barrierFailed", 16),
        ("moved", 100), ("barrier", 101),
    ];

    /// <summary>The events an "either of" event (<c>moved</c>, <c>barrier</c>) stands for; the event alone otherwise.</summary>
    public static int[] EventParts(int id) => id switch
    {
        100 => [10, 11],
        101 => [15, 16],
        _ => [id],
    };

    /// <summary>The <c>events.</c> name for an id, or null.</summary>
    public static string? EventName(int id) => Array.Find(Events, e => e.Id == id).Name;

    private static readonly CurveFamily?[] Families =
    [
        CurveFamily.Quad, CurveFamily.Cubic, CurveFamily.Quart, null, CurveFamily.Sine, CurveFamily.Expo,
        CurveFamily.Circ, CurveFamily.Elastic, CurveFamily.Back, CurveFamily.Bounce,
    ];

    /// <summary><c>ease.by(id, t)</c>, in C#: the same curves the shader uses.</summary>
    public static float Ease(int id, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        if (id <= 0 || id >= Easings.Length)
            return t;

        // smoothstep's curve, after the Penner families.
        if (id == 31)
            return t * t * (3f - 2f * t);
        if (id == 32)
            return t >= 1f ? 1f : 0f;

        int family = (id - 1) / 3;
        var direction = (CurveDirection)((id - 1) % 3);
        if (Families[family] is { } known)
            return EaseCurves.Evaluate(known, direction, t);

        // Quint, which the shared curves don't have.
        static float In(float x) => x * x * x * x * x;
        return direction switch
        {
            CurveDirection.In => In(t),
            CurveDirection.Out => 1f - In(1f - t),
            _ => t < 0.5f ? In(t * 2f) / 2f : 1f - In((1f - t) * 2f) / 2f,
        };
    }
}
