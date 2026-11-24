using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

/// <summary>
/// One kind of brick in a <see cref="ShaderStack"/>, in pipeline order: what runs
/// first comes first. Each kind is one file in the stack's folder and owns one entry.
/// </summary>
public enum BrickKind
{
    /// <summary><c>update.slang</c>: one struct the GPU keeps, <c>update</c> once a frame; every later brick reads <c>state</c>.</summary>
    Update,

    /// <summary><c>step.slang</c>: a pool of <c>count</c> items the GPU keeps, <c>step</c> on each once a frame; each is drawn once, as <c>item</c>.</summary>
    Step,

    /// <summary>
    /// <c>build.slang</c>: buffers the GPU keeps, compute passes over them, and <c>build()</c>, the order
    /// the passes run in each frame; deform and shade read the buffers.
    /// </summary>
    Build,

    /// <summary><c>deform.slang</c>: <c>deform</c>, run on every corner before it lands.</summary>
    Deform,

    /// <summary><c>shade.slang</c>: <c>shade</c>, run on every pixel.</summary>
    Shade,
}

/// <summary>One brick's code: its kind and its text.</summary>
public sealed record Brick(BrickKind Kind, string Text);

/// <summary>
/// A shader stack: bricks that compose one pipeline, a folder on disk with a
/// file per brick (<see cref="FileName"/>). Most stacks are a shade brick
/// alone; some add a deform brick; some start with compute (update, or a pool its step moves).
/// Bricks run in <see cref="BrickKind"/> order and data flows down: a brick
/// reads what the bricks above it keep and declare. Blend isn't a brick: it
/// is set where the stack is placed.
/// </summary>
/// <remarks>
/// The bricks compile as one effect body (<see cref="Compose"/>), each marked
/// with <c>#line 1 "stack/kind.slang"</c>, so Slang's errors land on the
/// brick's own lines under its own label (<see cref="Label"/>).
/// </remarks>
public static partial class ShaderStack
{
    /// <summary>Every kind, in pipeline order.</summary>
    public static readonly BrickKind[] Kinds = Enum.GetValues<BrickKind>();

    public static string FileName(BrickKind kind) => kind switch
    {
        BrickKind.Update => "update.slang",
        BrickKind.Step => "step.slang",
        BrickKind.Build => "build.slang",
        BrickKind.Deform => "deform.slang",
        BrickKind.Shade => "shade.slang",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The brick's name as the editor shows it.</summary>
    public static string Title(BrickKind kind) => kind.ToString().ToUpperInvariant();

    /// <summary>What a brick of this kind is for, in a line.</summary>
    public static string Purpose(BrickKind kind) => kind switch
    {
        BrickKind.Update => "compute: one struct kept on the GPU, update(s, dt) once a frame; later bricks read state",
        BrickKind.Step => "compute: count items kept on the GPU, step(t, i, dt) on each once a frame; each is drawn once, as item",
        BrickKind.Build => "compute: buffers kept on the GPU and [Pass] functions over them; build() runs the passes in order each frame",
        BrickKind.Deform => "deform(v) on every corner before it lands",
        BrickKind.Shade => "shade(...) on every pixel",
        _ => "",
    };

    /// <summary>The functions a kind's brick declares that the stack runs: its entry (and a pool's <c>start</c>).</summary>
    public static string[] Entries(BrickKind kind) => kind switch
    {
        BrickKind.Update => ["update"],
        BrickKind.Step => ["start", "step"],
        BrickKind.Build => ["build"],
        BrickKind.Deform => ["deform"],
        BrickKind.Shade => ["shade"],
        _ => [],
    };

    /// <summary>The label Slang's errors carry for a brick of stack <paramref name="name"/>.</summary>
    public static string Label(string name, BrickKind kind) => $"{name}/{FileName(kind)}".Replace("\"", "'");

    /// <summary>The entry a kind's brick must declare, and the pattern that finds it.</summary>
    private static (string Entry, Regex Pattern) EntryOf(BrickKind kind) => kind switch
    {
        BrickKind.Update => ("void update(inout State s, float dt)", UpdateEntry()),
        BrickKind.Step => ("void step(inout Item t, uint i, float dt) (or bool step, with a rate)", StepEntry()),
        BrickKind.Build => ("void build(), calling its passes in order", BuildEntry()),
        BrickKind.Deform => ("ItemVertex deform(ItemVertex v)", DeformEntry()),
        BrickKind.Shade => ("float4 shade(Canvas canvas)", ShadeEntry()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [GeneratedRegex(@"\bvoid\s+update\s*\(")]
    private static partial Regex UpdateEntry();

    [GeneratedRegex(@"\b(?:void|bool)\s+step\s*\(")]
    private static partial Regex StepEntry();

    [GeneratedRegex(@"\bvoid\s+build\s*\(\s*\)")]
    private static partial Regex BuildEntry();

    [GeneratedRegex(@"\bItemVertex\s+deform\s*\(")]
    private static partial Regex DeformEntry();

    [GeneratedRegex(@"\bfloat4\s+shade\s*\(")]
    private static partial Regex ShadeEntry();

    /// <summary>
    /// The bricks as one effect body, in pipeline order, each under its own
    /// <c>#line</c> mark. Throws, worded as Slang's errors are (so the editor puts
    /// the problem on the brick), when the stack can't work: no bricks, a brick
    /// without its entry or with another kind's, or a pool with nothing to place its items.
    /// </summary>
    public static string Compose(string name, IReadOnlyList<Brick> bricks)
    {
        Check(name, bricks);
        return Join(name, bricks);
    }

    /// <summary>
    /// The bricks as one body without checking them (<see cref="Compose"/> checks first): for reading
    /// the code as a whole (the editor's clips and warnings) while a brick is half written.
    /// </summary>
    public static string Join(string name, IReadOnlyList<Brick> bricks)
    {
        var composed = new StringBuilder();
        foreach (Brick brick in bricks.OrderBy(brick => brick.Kind))
        {
            composed.Append("#line 1 \"").Append(Label(name, brick.Kind)).Append("\"\n");
            composed.Append(brick.Text.ReplaceLineEndings("\n"));
            if (!brick.Text.EndsWith('\n'))
                composed.Append('\n');
        }

        // Without a shade brick the item keeps its own colour.
        if (!bricks.Any(brick => brick.Kind == BrickKind.Shade))
            composed.Append($"#line 1 \"{name} (no shade brick)\"\nfloat4 shade(ItemInput i) {{ return i.colour; }}\n");
        return composed.ToString();
    }

    private static void Check(string name, IReadOnlyList<Brick> bricks)
    {
        if (bricks.Count == 0)
            throw new InvalidOperationException($"{name}(1): error: a stack needs at least one brick.");

        foreach (IGrouping<BrickKind, Brick> twice in bricks.GroupBy(brick => brick.Kind).Where(group => group.Count() > 1))
            throw new InvalidOperationException($"{Label(name, twice.Key)}(1): error: a stack has one {Title(twice.Key)} brick.");

        foreach (Brick brick in bricks)
        {
            string code = StripComments(brick.Text);
            (string entry, Regex own) = EntryOf(brick.Kind);
            if (!own.IsMatch(code))
                throw new InvalidOperationException($"{Label(name, brick.Kind)}(1): error: the {Title(brick.Kind)} brick declares {entry}.");

            foreach (BrickKind other in Kinds.Where(kind => kind != brick.Kind))
            {
                Match stray = EntryOf(other).Pattern.Match(code);
                if (stray.Success)
                    throw new InvalidOperationException(
                        $"{Label(name, brick.Kind)}({LineAt(code, stray.Index)}): error: {EntryOf(other).Entry.Split('(')[0].Split(' ')[^1]} belongs in the {Title(other)} brick ({FileName(other)}).");
            }
        }

        if (bricks.Any(brick => brick.Kind == BrickKind.Step) && !bricks.Any(brick => brick.Kind == BrickKind.Deform))
            throw new InvalidOperationException(
                $"{Label(name, BrickKind.Step)}(1): error: a pool's items are drawn by a DEFORM brick: add one, and place each item there with deform.");
    }

    private static int LineAt(string text, int index) => 1 + text.AsSpan(0, index).Count('\n');

    private static string StripComments(string source) =>
        Regex.Replace(Regex.Replace(source, @"/\*.*?\*/", match => new string('\n', match.Value.Count(c => c == '\n')), RegexOptions.Singleline),
            @"//[^\n]*", "");

    /// <summary>What a new brick of this kind starts as: a working one that changes nothing much, explained.</summary>
    public static string Template(BrickKind kind) => kind switch
    {
        BrickKind.Update => """
            // UPDATE: one struct the GPU keeps from frame to frame.
            // update runs once a frame on one GPU thread; every later brick reads it as `state`.
            import xanadu;

            // Where it eases towards, in pixels from the centre: drag its dot on the preview.
            [Default(200, 0)] uniform float2 target;

            struct State { float2 follow; };

            void update(inout State s, float dt)
            {
                s.follow = lerp(s.follow, target, 1 - exp(-dt * 4));
            }

            """,
        BrickKind.Step => """
            // STEP: `count` items the GPU keeps from frame to frame.
            // start(i) makes item i after every compile; step moves it once a frame.
            // The DEFORM brick draws each item once: it reads the one it is drawing as `item`.
            import xanadu;

            struct Item { float2 position; float2 velocity; };

            static const uint count = 1000;

            Item start(uint i)
            {
                Item t;
                t.position = (random2(float2(i, 1)) - 0.5) * 800;
                t.velocity = (random2(float2(i, 2)) - 0.5) * 200;
                return t;
            }

            void step(inout Item t, uint i, float dt)
            {
                t.position += t.velocity * dt;
            }

            """,
        BrickKind.Build => """
            // BUILD: buffers the GPU keeps from frame to frame, and passes that work on them.
            // Each [Pass] runs `threads` a group: over: n gives a thread to each of n (the rest
            // turned away), groups: g runs g whole groups (for code with barriers). build() runs
            // on the CPU each frame: it calls the passes in order, each one its own dispatch, so
            // a pass sees everything the passes before it wrote. DEFORM and SHADE read the buffers.
            import xanadu;

            static const uint cells = 4096;

            // The wrapper places every buffer: never write a binding.
            [Buffer(cells)] RWStructuredBuffer<float> heights;

            [Pass(256, over: cells)]
            void grow(uint i)
            {
                heights[i] = 0.5 + 0.5 * sin(time * 2 + i * 0.01);
            }

            void build()
            {
                grow();
            }

            """,
        BrickKind.Deform => """
            // DEFORM: deform runs on every corner before it lands; move v.position to move the shape.
            import xanadu;

            ItemVertex deform(ItemVertex v)
            {
                v.position.y += sin(time * 3 + v.uv.x * 6) * 6;
                return v;
            }

            """,
        BrickKind.Shade => """
            // SHADE: shade runs on every pixel and returns its colour.
            import xanadu;

            [Default(colours.gold)] uniform colour tint;

            float4 shade(ItemInput i, Canvas canvas)
            {
                colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
                let blob = Circle(120 + 20 * sin(time * 2));
                return lerp(sky, tint, blob.filled(canvas.centred, 2));
            }

            """,
        _ => "",
    };

    // ---- on disk -------------------------------------------------------------------

    /// <summary>Whether a folder is a stack: it holds at least one brick file.</summary>
    public static bool IsStack(string folder) =>
        Directory.Exists(folder) && Kinds.Any(kind => File.Exists(Path.Combine(folder, FileName(kind))));

    /// <summary>The bricks in a stack's folder, in pipeline order.</summary>
    public static List<Brick> Read(string folder) =>
    [
        .. Kinds.Select(kind => (kind, file: Path.Combine(folder, FileName(kind))))
            .Where(brick => File.Exists(brick.file))
            .Select(brick => new Brick(brick.kind, File.ReadAllText(brick.file))),
    ];

    /// <summary>Writes the bricks into the folder (made if needed) and deletes the files of kinds it no longer has.</summary>
    public static void Write(string folder, IReadOnlyList<Brick> bricks)
    {
        Directory.CreateDirectory(folder);
        foreach (BrickKind kind in Kinds)
        {
            string file = Path.Combine(folder, FileName(kind));
            if (bricks.FirstOrDefault(brick => brick.Kind == kind) is { } brick)
                File.WriteAllText(file, brick.Text);
            else if (File.Exists(file))
                File.Delete(file);
        }
    }

    /// <summary>Every stack under <paramref name="root"/> (full paths), outside <paramref name="skip"/>, in order.</summary>
    public static IEnumerable<string> Under(string root, string? skip = null) =>
        Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(folder => skip is null || !folder.StartsWith(skip, StringComparison.Ordinal))
            .Where(IsStack)
            .Order(StringComparer.Ordinal);
}
