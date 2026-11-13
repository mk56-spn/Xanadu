using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Xanadu.Timeline;

namespace Xanadu.Slang;

/// <summary>
/// A clip found in shader code (<c>xanadu.anim</c>), worked out from its text
/// alone: where it sits on its clock, how it eases, and the keys that follow it.
/// Times are in the clock's own units (seconds, or beats when <see cref="Beats"/>).
/// </summary>
/// <param name="Name">The local it is declared as.</param>
/// <param name="Line">0-based line of its declaration.</param>
/// <param name="Column">0-based column of its name there.</param>
/// <param name="Loop">How long its clock's loop is; 0 when the clock doesn't loop.</param>
/// <param name="Start">Where it starts on its clock; NaN when the code doesn't fix it.</param>
/// <param name="Length">How long one pass is; NaN when the code doesn't fix it.</param>
/// <param name="Easing">Its easing, numbered as <c>ease.by</c>.</param>
/// <param name="StaggerStep">Each copy's delay when it is staggered by a value only the pixel knows; 0 when it isn't.</param>
/// <param name="Keys">The progress (0..1) of each key of every <c>Keys</c> read at it.</param>
/// <param name="Pressed">What its clock counts from: a play key (<c>clock.since(keys.left)</c>), numbered as <see cref="SlangClips.PlayKeys"/>; a play event (<c>clock.since(events.hit)</c>), <see cref="SlangClips.FirstEvent"/> plus its id in <see cref="SlangClips.Events"/>; -1 for the song's time.</param>
public sealed record SlangClip(
    string Name,
    int Line,
    int Column,
    bool Beats,
    double Loop,
    double Start,
    double Length,
    int Easing,
    bool Bounced,
    bool Reversed,
    double StaggerStep,
    IReadOnlyList<double> Keys,
    int Pressed = -1)
{
    /// <summary>Whether it plays from a key's press or an event rather than on the song's time.</summary>
    public bool OnKey => Pressed >= 0;

    /// <summary>Whether it plays from a play event (<c>events.</c>) rather than a key.</summary>
    public bool OnEvent => Pressed >= SlangClips.FirstEvent;

    /// <summary>Whether the code fixes where it is, so it can be drawn.</summary>
    public bool Fixed => double.IsFinite(Start) && double.IsFinite(Length);

    /// <summary>Start to end: twice its length when bounced.</summary>
    public double Span => Bounced ? Length * 2 : Length;

    public double End => Start + Span;

    /// <summary>Eased progress at a fraction of its <see cref="Span"/>, as the library works it out.</summary>
    public float ProgressAt(float fraction)
    {
        float p = Bounced ? 1f - MathF.Abs(1f - Math.Clamp(fraction * 2f, 0f, 2f)) : Math.Clamp(fraction, 0f, 1f);
        return SlangClips.Ease(Easing, Reversed ? 1f - p : p);
    }
}

/// <summary>
/// Reads the clips out of shader code for the shader editor's timeline: every
/// local declared as a <c>Clip</c> (or a <c>let</c>/<c>var</c> that turns out
/// to be one), built from <c>clock</c> and its builders. Numbers, number locals,
/// <c>PI</c>/<c>TAU</c> and plain sums are worked out; anything else (a
/// uniform, a pixel's value) leaves that part unknown. No Slang compile is
/// needed, so it follows the text as it is typed.
/// </summary>
public static partial class SlangClips
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

    private sealed record ClockValue(bool Beats, double Loop, int Key = -1);

    private sealed record KeyValue(int Id);

    private sealed record EventValue(int Id);

    /// <summary>A note of the chart (<c>notes[i]</c>, a <c>Note</c> variable): which one is up to the running shader.</summary>
    private sealed record NoteValue;

    private sealed record ClipValue(ClockValue Clock, double Start, double Length, int Easing, bool Bounced, bool Reversed, double StaggerStep)
    {
        public double End => Start + (Bounced ? Length * 2 : Length);
    }

    private sealed record KeysValue(List<double> Places);

    private sealed record EaseValue(int Id);

    /// <summary>Not worked out: the code decides it while the shader runs.</summary>
    private sealed class Unknown
    {
        public static readonly Unknown Value = new();
    }

    /// <summary>A namespace or type name on its way to a member (<c>eases.</c>).</summary>
    private sealed record Name(string Text);

    [GeneratedRegex(@"(?<![\w.])(?:(?:static\s+)?const\s+)?(Clip|Clock|Note|float|int|let|var|Keys\s*<\s*\w+\s*>)\s+([A-Za-z_]\w*)\s*=")]
    private static partial Regex Declaration();

    [GeneratedRegex(@"\b([A-Za-z_]\w*)\s*\.\s*at\s*\(\s*([A-Za-z_]\w*)\s*(?:\.\s*progress\s*)?\)")]
    private static partial Regex KeysAt();

    /// <summary>The clips in the code, in the order they are declared.</summary>
    public static List<SlangClip> Find(IReadOnlyList<string> lines)
    {
        string code = Blank(string.Join('\n', lines));
        var starts = new List<int> { 0 };
        for (int i = 0; i < code.Length; i++)
        {
            if (code[i] == '\n')
                starts.Add(i + 1);
        }

        var values = new Dictionary<string, object>(StringComparer.Ordinal);
        var clips = new List<(string Name, int At, ClipValue Clip)>();
        foreach (Match match in Declaration().Matches(code))
        {
            int from = match.Index + match.Length;
            int end = StatementEnd(code, from);
            if (end < 0)
                continue;

            string name = match.Groups[2].Value;
            object value;
            try
            {
                value = new Evaluator(code[from..end], values).Expression();
            }
            catch (FormatException)
            {
                value = Unknown.Value;
            }

            string type = match.Groups[1].Value;
            if (type == "Note")
                value = new NoteValue();
            values[name] = value;
            if (value is ClipValue clip && type is "Clip" or "let" or "var")
                clips.Add((name, match.Groups[2].Index, clip));
        }

        // Keys read at a clip lay their keys on its lane.
        var keys = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        foreach (Match match in KeysAt().Matches(code))
        {
            if (values.GetValueOrDefault(match.Groups[1].Value) is not KeysValue list)
                continue;
            string clip = match.Groups[2].Value;
            if (!keys.TryGetValue(clip, out List<double>? places))
                keys[clip] = places = [];
            foreach (double place in list.Places)
            {
                if (!places.Contains(place))
                    places.Add(place);
            }
        }

        var found = new List<SlangClip>();
        foreach ((string name, int at, ClipValue clip) in clips)
        {
            int line = starts.BinarySearch(at);
            if (line < 0)
                line = ~line - 1;
            List<double> places = keys.GetValueOrDefault(name) ?? [];
            places.Sort();
            found.Add(new SlangClip(
                name, line, at - starts[line], clip.Clock.Beats, clip.Clock.Loop, clip.Start, clip.Length,
                clip.Easing, clip.Bounced, clip.Reversed, clip.StaggerStep, places, clip.Clock.Key));
        }

        return found;
    }

    /// <summary>The code with comments and strings turned to spaces, so places stay where they were.</summary>
    private static string Blank(string code)
    {
        var text = new StringBuilder(code);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '/')
            {
                while (i < text.Length && text[i] != '\n')
                    text[i++] = ' ';
            }
            else if (text[i] == '/' && i + 1 < text.Length && text[i + 1] == '*')
            {
                int close = code.IndexOf("*/", i + 2, StringComparison.Ordinal);
                int stop = close < 0 ? text.Length : close + 2;
                for (; i < stop; i++)
                {
                    if (text[i] != '\n')
                        text[i] = ' ';
                }

                i--;
            }
            else if (text[i] == '"')
            {
                text[i++] = ' ';
                while (i < text.Length && text[i] != '"' && text[i] != '\n')
                    text[i++] = ' ';
                if (i < text.Length && text[i] == '"')
                    text[i] = ' ';
            }
        }

        return text.ToString();
    }

    /// <summary>The <c>;</c> that ends a statement begun at <paramref name="from"/>, outside brackets; -1 when there is none.</summary>
    private static int StatementEnd(string code, int from)
    {
        int depth = 0;
        for (int i = from; i < code.Length; i++)
        {
            switch (code[i])
            {
                case '(' or '[':
                    depth++;
                    break;
                case ')' or ']':
                    depth--;
                    break;
                case '{' or '}':
                    return -1;
                case ';' when depth <= 0:
                    return i;
            }
        }

        return -1;
    }

    /// <summary>Works out one expression: numbers, sums, clocks, clips, keys and easings.</summary>
    private sealed class Evaluator(string text, Dictionary<string, object> values)
    {
        private int _at;

        public object Expression()
        {
            object value = Term();
            while (Peek() is '+' or '-')
            {
                char op = text[_at++];
                object right = Term();
                value = Arithmetic(value, right, op);
            }

            return value;
        }

        private object Term()
        {
            object value = Unary();
            while (Peek() is '*' or '/' or '%')
            {
                char op = text[_at++];
                object right = Unary();
                value = Arithmetic(value, right, op);
            }

            return value;
        }

        private static object Arithmetic(object left, object right, char op) =>
            left is double a && right is double b
                ? op switch
                {
                    '+' => a + b,
                    '-' => a - b,
                    '*' => a * b,
                    '/' => b == 0 ? Unknown.Value : a / b,
                    _ => b == 0 ? Unknown.Value : a % b,
                }
                : Unknown.Value;

        private object Unary()
        {
            if (Peek() == '-')
            {
                _at++;
                return Unary() is double value ? -value : Unknown.Value;
            }

            if (Peek() == '+')
                _at++;
            return Postfix(Primary());
        }

        private object Primary()
        {
            char? next = Peek();
            if (next == '(')
            {
                _at++;
                object inner = Expression();
                Expect(')');
                return inner;
            }

            if (next is { } digit && (char.IsDigit(digit) || digit == '.'))
                return Number();

            if (next is not { } letter || !(char.IsLetter(letter) || letter == '_'))
                throw new FormatException();

            string name = Word();
            if (name == "notes" && Peek() == '[')
            {
                _at = Close(text, _at, '[', ']');
                return new NoteValue();
            }

            if (name == "Keys" && Peek() == '<')
            {
                _at = text.IndexOf('>', _at) is var close and >= 0 ? close + 1 : throw new FormatException();
                List<object> first = Arguments();
                return first.Count == 1 ? new KeysValue([0]) : Unknown.Value;
            }

            if (Peek() == '(')
            {
                List<object> arguments = Arguments();
                return name == "Ease" && arguments is [double id] ? new EaseValue((int)id) : Unknown.Value;
            }

            return name switch
            {
                "clock" => new ClockValue(false, 0),
                "PI" => Math.PI,
                "TAU" => Math.Tau,
                "eases" or "keys" or "events" => new Name(name),
                _ => values.GetValueOrDefault(name) ?? Unknown.Value,
            };
        }

        private object Postfix(object value)
        {
            while (Peek() == '.')
            {
                _at++;
                string member = Word();
                List<object>? arguments = Peek() == '(' ? Arguments() : null;
                value = Member(value, member, arguments);
            }

            return value;
        }

        private static object Member(object value, string member, List<object>? a)
        {
            switch (value)
            {
                case Name { Text: "eases" } when a is null:
                    int id = Array.IndexOf(Easings, member);
                    return id >= 0 ? new EaseValue(id) : Unknown.Value;

                case Name { Text: "events" } when a is null:
                    int found = Array.FindIndex(Events, e => e.Name == member);
                    return found >= 0 ? new EventValue(Events[found].Id) : Unknown.Value;

                case Name { Text: "keys" } when a is null:
                    int key = Array.IndexOf(PlayKeys, member);
                    return key >= 0 ? new KeyValue(key) : Unknown.Value;

                case ClockValue clock:
                    return (member, a) switch
                    {
                        ("beats", null) => clock with { Beats = true, Key = -1 },
                        ("since", [KeyValue pressed]) => new ClockValue(false, clock.Loop, pressed.Id),
                        ("since", [EventValue happened]) => new ClockValue(false, clock.Loop, FirstEvent + happened.Id),
                        // Which note is the shader's business; the timeline draws it from each judgement.
                        ("since", [NoteValue]) => new ClockValue(false, clock.Loop, FirstEvent + NoteJudged),
                        ("looped", [double length]) => clock with { Loop = length },
                        ("looped", [_]) => clock with { Loop = double.NaN },
                        ("clip", [var start, var length]) => new ClipValue(clock, Number(start), Number(length), 0, false, false, 0),
                        _ => Unknown.Value,
                    };

                case ClipValue clip:
                    return (member, a) switch
                    {
                        ("eased", [EaseValue ease]) => clip with { Easing = ease.Id },
                        ("eased", [_]) => clip,
                        ("delayed", [var by]) => clip with { Start = clip.Start + Number(by) },
                        ("staggered", [double index, double step]) => clip with { Start = clip.Start + index * step },
                        ("staggered", [_, double step]) => clip with { StaggerStep = clip.StaggerStep + step },
                        ("staggered", [_, _]) => clip with { Start = double.NaN },
                        ("bounced", []) => clip with { Bounced = true },
                        ("reversed", []) => clip with { Reversed = !clip.Reversed },
                        ("then", [var length]) => new ClipValue(clip.Clock, clip.End, Number(length), 0, false, false, clip.StaggerStep),
                        ("part", [var from, var to]) => new ClipValue(
                            clip.Clock, clip.Start + Number(from) * (clip.End - clip.Start), (Number(to) - Number(from)) * (clip.End - clip.Start),
                            0, false, false, clip.StaggerStep),
                        _ => Unknown.Value,
                    };

                case KeysValue keys when member == "to" && a is { Count: >= 2 }:
                    // A copy, so a Keys built from another keeps the other's keys as they were.
                    var places = new List<double>(keys.Places);
                    if (a[0] is double place)
                        places.Add(place);
                    return new KeysValue(places);

                default:
                    return Unknown.Value;
            }
        }

        private static double Number(object value) => value is double number ? number : double.NaN;

        /// <summary>Just past the bracket that closes the one at <paramref name="at"/>.</summary>
        private static int Close(string text, int at, char open, char close)
        {
            int depth = 0;
            for (int i = at; i < text.Length; i++)
            {
                if (text[i] == open)
                    depth++;
                else if (text[i] == close && --depth == 0)
                    return i + 1;
            }

            throw new FormatException();
        }

        private List<object> Arguments()
        {
            Expect('(');
            var arguments = new List<object>();
            if (Peek() == ')')
            {
                _at++;
                return arguments;
            }

            while (true)
            {
                arguments.Add(ArgumentOrSkip());
                if (Peek() == ',')
                {
                    _at++;
                    continue;
                }

                Expect(')');
                return arguments;
            }
        }

        /// <summary>One argument; one it can't read (a <c>float2(…)</c> of pixels, a ternary) is skipped to its comma as unknown.</summary>
        private object ArgumentOrSkip()
        {
            int start = _at;
            try
            {
                object value = Expression();
                if (Peek() is ',' or ')')
                    return value;
            }
            catch (FormatException)
            {
            }

            _at = start;
            int depth = 0;
            for (; _at < text.Length; _at++)
            {
                char c = text[_at];
                if (c is '(' or '[')
                    depth++;
                else if (c is ')' or ']' && depth-- == 0)
                    break;
                else if (c == ',' && depth == 0)
                    break;
            }

            return Unknown.Value;
        }

        private double Number()
        {
            int start = _at;
            while (_at < text.Length && (char.IsDigit(text[_at]) || text[_at] == '.'))
                _at++;
            if (_at < text.Length && text[_at] is 'e' or 'E')
            {
                _at++;
                if (_at < text.Length && text[_at] is '-' or '+')
                    _at++;
                while (_at < text.Length && char.IsDigit(text[_at]))
                    _at++;
            }

            double value = double.Parse(text[start.._at], NumberStyles.Float, CultureInfo.InvariantCulture);
            // A suffix (1.0f, 2u, 0.5h) changes nothing here.
            while (_at < text.Length && text[_at] is 'f' or 'F' or 'h' or 'H' or 'u' or 'U' or 'l' or 'L')
                _at++;
            return value;
        }

        private string Word()
        {
            SkipSpace();
            int start = _at;
            while (_at < text.Length && (char.IsLetterOrDigit(text[_at]) || text[_at] == '_'))
                _at++;
            if (_at == start)
                throw new FormatException();
            return text[start.._at];
        }

        private void Expect(char c)
        {
            if (Peek() != c)
                throw new FormatException();
            _at++;
        }

        private char? Peek()
        {
            SkipSpace();
            return _at < text.Length ? text[_at] : null;
        }

        private void SkipSpace()
        {
            while (_at < text.Length && char.IsWhiteSpace(text[_at]))
                _at++;
        }
    }
}
