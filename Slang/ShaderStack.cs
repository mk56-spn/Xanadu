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

    /// <summary><c>step.slang</c>: a pool of <c>[Pool(count)]</c> items the GPU keeps, <c>step</c> on each once a frame; each is drawn once, as <c>item</c>.</summary>
    Step,

    /// <summary>
    /// <c>build.slang</c>: buffers the GPU keeps, compute passes over them, and <c>build()</c>, the order
    /// the passes run in each frame; deform and shade read the buffers.
    /// </summary>
    Build,

    /// <summary>
    /// <c>trace.slang</c>: <c>trace</c>, compute run on every pixel of the quad in tiles that may share
    /// memory; its colour and depth land first, under the stack's own triangles.
    /// </summary>
    Trace,

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
        BrickKind.Trace => "trace.slang",
        BrickKind.Deform => "deform.slang",
        BrickKind.Shade => "shade.slang",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>The brick's name as the editor shows it.</summary>
    public static string Title(BrickKind kind) => kind.ToString().ToUpperInvariant();

    /// <summary>
    /// What a brick of this kind is, at length, for hovering it in the editor: what it runs,
    /// when, what it reads, what it hands down, and what it is good for.
    /// </summary>
    public static string Description(BrickKind kind) => kind switch
    {
        BrickKind.Update =>
            "UPDATE (update.slang) is compute that runs once a frame on a single GPU thread, before anything is drawn. " +
            "It keeps one struct of yours, State, on the GPU from frame to frame: void update(inout State s, float dt) " +
            "changes it, and every brick below reads it as `state`. It starts as zeros after each compile. " +
            "Use it for things the whole effect shares and that build up over time: a value easing towards a target, " +
            "a counter, a smoothed beat. Lab and shader tool only for now.",
        BrickKind.Step =>
            "STEP (step.slang) is a pool: [Pool(count)] items of your own struct kept on the GPU, each moved once a frame by " +
            "[Pool(count)] void step(inout Item t, uint i, float dt), one GPU thread per item; start(i) makes each after a compile. " +
            "Each item is drawn once: DEFORM places it (reading it as `item`) and SHADE colours it. With [Pool(count, rate)] and a " +
            "bool step, the engine bears that many a second into dead slots and frees those whose step returns false. " +
            "Use it for swarms, sparks and particles that each move on their own. Needs a DEFORM brick. Lab only for now.",
        BrickKind.Build =>
            "BUILD (build.slang) is compute over buffers you declare ([Buffer(size)] RWStructuredBuffer<T> name), kept on " +
            "the GPU and zeroed after each compile. [Pass(threads, over: n)] functions work on them, a thread each, or " +
            "groups: g for whole groups that share groupshared memory. void build() runs on the CPU every frame and calls " +
            "the passes in order, each its own dispatch, so a pass sees everything the ones before it wrote; it may loop " +
            "and branch on constants and uniforms. TRACE, DEFORM and SHADE read the buffers, and a [Draws] buffer sets how " +
            "many copies of the quad are drawn. Use it for work no single item can do alone: sorting, building a tree, " +
            "counting into cells. Lab only for now.",
        BrickKind.Trace =>
            "TRACE (trace.slang) runs trace(...) as compute, one GPU thread per pixel of the quad, before the stack's own " +
            "triangles draw. Threads come in tiles, [Tile(w, h)] (8 × 8 by default), that share groupshared memory and " +
            "wait for each other (GroupMemoryBarrierWithGroupSync); `group` says where a thread sits. What it returns " +
            "lands on the frame first, and traced.depth (pixels into the screen) sets how deep each pixel is, so DEFORM " +
            "and SHADE copies drawn after it hide behind it or show in front. It reads the uniforms, globals, state, the " +
            "depth and the BUILD buffers. Use it for raymarchers whose rays share what a tile sees, or blurs that load a " +
            "block once. Without a SHADE brick the trace is all the stack draws. Lab only for now.",
        BrickKind.Deform =>
            "DEFORM (deform.slang) runs ItemVertex deform(ItemVertex v) on every corner of the quad before it lands on " +
            "the target: move v.position to move or bend the shape, set v.depth so overlapping parts sort themselves. " +
            "[Grid(columns, rows)] cuts the quad into cells, giving it corners inside to bend; [Translucent] makes its " +
            "triangles test the depth without writing it, for glows. With a pool or a [Draws] buffer it runs once per " +
            "copy, and `copy` (or `item`) says which. Use it for waves, 3D surfaces, and placing each particle.",
        BrickKind.Shade =>
            "SHADE (shade.slang) runs float4 shade(...) on every pixel the stack's triangles cover and returns its colour. " +
            "It takes Canvas (the quad: size, centre, `centred` for this pixel) and ItemInput (uv, the item's colour and " +
            "texture, screen place), either, both or neither. It can read the screen drawn so far, the depth, its own " +
            "textures, the uniforms, globals, state, the pool's item and the BUILD buffers. Most stacks are this brick " +
            "alone. Without one, the item keeps its own colour (or, under a TRACE, only the trace draws).",
        _ => "",
    };

    /// <summary>What the blend after the bricks is, at length, for hovering it.</summary>
    public const string BlendDescription =
        "Blend isn't a brick: it is how the stack's colour mixes with what is already on the frame (mix, add, subtract, " +
        "multiply, premultiplied, or a custom one), set where the stack is placed (on the item or its material), not in " +
        "the code. The same stack can glow additively in one place and mix plainly in another.";

    /// <summary>
    /// The bricks a kind can't work without. When one needs the other back, adding either
    /// adds both; when only one needs the other, adding it says what is missing.
    /// </summary>
    public static BrickKind[] Needs(BrickKind kind) => kind switch
    {
        BrickKind.Step => [BrickKind.Deform],
        _ => [],
    };

    /// <summary>The functions a kind's brick declares that the stack runs: its entry (and a pool's <c>start</c>).</summary>
    public static string[] Entries(BrickKind kind) => kind switch
    {
        BrickKind.Update => ["update"],
        BrickKind.Step => ["start", "step"],
        BrickKind.Build => ["graph", "build"],
        BrickKind.Trace => ["trace"],
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
        BrickKind.Step => ("[Pool(count)] void step(inout Item t, uint i, float dt) (or [Pool(count, rate)] bool step)", StepEntry()),
        BrickKind.Build => ("void graph(), calling its passes in order", BuildEntry()),
        BrickKind.Trace => ("float4 trace(Canvas canvas)", TraceEntry()),
        BrickKind.Deform => ("ItemVertex deform(ItemVertex v)", DeformEntry()),
        BrickKind.Shade => ("float4 shade(Canvas canvas)", ShadeEntry()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [GeneratedRegex(@"\bvoid\s+update\s*\(")]
    private static partial Regex UpdateEntry();

    [GeneratedRegex(@"\b(?:void|bool)\s+step\s*\(")]
    private static partial Regex StepEntry();

    [GeneratedRegex(@"\bvoid\s+(?:graph|build)\s*\(\s*\)")]
    private static partial Regex BuildEntry();

    [GeneratedRegex(@"\b(?:float4|colour)\s+trace\s*\(")]
    private static partial Regex TraceEntry();

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

        // Without a shade brick the item keeps its own colour, or, under a trace, draws nothing of its own.
        if (!bricks.Any(brick => brick.Kind == BrickKind.Shade))
            composed.Append(bricks.Any(brick => brick.Kind == BrickKind.Trace || (brick.Kind == BrickKind.Build && StackBuild.PresentsIn(brick.Text)))
                ? $"#line 1 \"{name} (no shade brick)\"\n{TracedAlone}\n"
                : $"#line 1 \"{name} (no shade brick)\"\nfloat4 shade(ItemInput i) {{ return i.colour; }}\n");
        return composed.ToString();
    }

    /// <summary>The shade a stack with a TRACE brick and no SHADE brick gets: its quad is never drawn, only the trace.</summary>
    public const string TracedAlone = "float4 shade(ItemInput i) { return float4(0); } // traced alone";

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
            // STEP: items the GPU keeps from frame to frame, as many as [Pool(n)] on step says.
            // start(i) makes item i after every compile; step moves it once a frame.
            // The DEFORM brick draws each item once: it reads the one it is drawing as `item`.
            import xanadu;

            struct Item { float2 position; float2 velocity; };

            Item start(uint i)
            {
                Item t;
                t.position = (random2(float2(i, 1)) - 0.5) * 800;
                t.velocity = (random2(float2(i, 2)) - 0.5) * 200;
                return t;
            }

            [Pool(1000)]
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
        BrickKind.Trace => """
            // TRACE: trace runs on every pixel of the quad as compute, in tiles of [Tile(w, h)]
            // threads. A tile's threads may share groupshared memory and wait for each other
            // (GroupMemoryBarrierWithGroupSync): load what the tile needs once, then all read it.
            // group says where a thread sits: group.thread in its tile, group.place the tile's.
            // traced.depth (pixels into the screen) lets the stack's own triangles drawn after
            // it hide behind it. It lands first; DEFORM and SHADE draw over it.
            import xanadu;

            groupshared float wave[8];

            [Tile(8, 8)]
            float4 trace(ItemInput i, Canvas canvas)
            {
                // One thread a column loads, everyone reads.
                if (group.thread.y == 0)
                    wave[group.thread.x] = sin(time * 2 + (group.place.x * 8 + group.thread.x) * 0.05) * 60;
                GroupMemoryBarrierWithGroupSync();
                float d = canvas.centred.y - wave[group.thread.x];
                traced.depth = d;
                return lerp(colours.midnightBlue, colours.skyBlue, saturate(0.5 - d / 200));
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
