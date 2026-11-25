using System.Collections.Generic;
using System.Linq;

namespace Xanadu.Slang;

/// <summary>
/// A kind of file a stack can be given: a name for the list, a paragraph saying what it is for, the role its entry takes
/// (a stack has each role at most once), and its starting code. A preset that draws or computes through the graph
/// (<see cref="InGraph"/>) brings its own <c>graph()</c> when the stack has none; when it has one, the file opens with
/// the calls to add to it instead.
/// </summary>
/// <param name="Stem">The file's name before a number: <c>shade.slang</c>, <c>meshes2.slang</c>.</param>
/// <param name="Role">The entry it declares, which the stack can't have twice; null for none.</param>
/// <param name="InGraph">What it runs from <c>graph()</c>, in order; empty for a file the graph doesn't run.</param>
public sealed record StackPreset(string Name, string Stem, string About, StackRole? Role, string[] InGraph, string Body)
{
    /// <summary>The file's code, for a stack that already has a graph or not.</summary>
    public string Text(string file, bool hasGraph)
    {
        string calls = string.Join(" ", InGraph.Select(call => call + ";"));
        string head = InGraph.Length > 0 && hasGraph ? $"// Run these from the stack's graph(): {calls}\n" : "";
        string graph = InGraph.Length > 0 && !hasGraph ? $"\nvoid graph() {{ {calls} }}\n" : "";
        return $"// {file}: {Name.ToLowerInvariant()}. {About}\n{head}import xanadu;\n{Body}{graph}";
    }

    /// <summary>Why this preset can't be added to a stack whose files take <paramref name="roles"/>; null when it can.</summary>
    public string? Refused(IReadOnlyCollection<StackRole> roles) =>
        Role is { } role && roles.Contains(role) ? $"The stack has its {ShaderStack.Entries(role)[0]}() already: a stack has one." : null;
}

/// <summary>The presets "+ FILE" offers, in the order it lists them.</summary>
public static class StackPresets
{
    public static readonly IReadOnlyList<StackPreset> All =
    [
        new("Empty", "part",
            "Nothing but the library. Every file is read with the others as one, so this is where shared structs, constants "
            + "and functions go: anything you write here, the other files can use.",
            null, [], ""),

        new("Shade", "shade",
            "Colours the item. shade runs once for every pixel the item covers and returns that pixel's colour; i holds where "
            + "the pixel is (i.canvas, i.local, i.uv), the time and the item's own colour. The stack has one shade.",
            StackRole.Shade, [],
            """

            [Default(0.2, 0.6, 1, 1)] uniform colour tint;

            float4 shade(ItemInput i)
            {
                colour c = colours.transparent;
                c.paint(Circle(min(i.canvas.size.x, i.canvas.size.y) * 0.4), tint);
                return c * i.colour;
            }

            """),

        new("Deform", "deform",
            "Moves the item's corners before it is drawn. The quad is cut into a grid ([Grid(columns, rows)]) and deform "
            + "runs on every corner, so the item can wave, bend or bulge; shade then paints the bent shape. The stack has one deform.",
            StackRole.Deform, [],
            """

            [Default(8)] uniform float height;

            [Grid(16, 1)]
            ItemVertex deform(ItemVertex v)
            {
                v.position.y += sin(time * 4 + v.uv.x * 6) * height * unit;
                return v;
            }

            """),

        new("Graph", "graph",
            "Work on the GPU every frame, in an order you choose. A List keeps numbers from frame to frame; a [Pass] runs a "
            + "thread for each of them; graph() says what runs, and in which order. shade and other passes read the list. "
            + "The stack has one graph.",
            StackRole.Graph, [],
            """

            static const uint COUNT = 64;

            [Size(COUNT)] List<float> heights;

            /// One thread a height: each eases towards a wave.
            [Pass(64, over: COUNT)]
            void grow(uint i)
            {
                float wanted = 0.5 + 0.5 * sin(time * 2 + i * 0.3);
                heights[i] = lerp(heights[i], wanted, saturate(dt * 4));
            }

            void graph() { grow(); }

            """),

        new("Image pass", "image",
            "Paints an image the size of the item, one thread a pixel, and shows it. Images can be kept from frame to frame "
            + "([Kept]), remember last frame ([History(image)]), be made smaller ([Scale(0.5)]) or be filled only when the "
            + "values change ([Cached]): the start of blurs, trails and post effects.",
            null, ["paint()", "present(shown)"],
            """

            Image<float4> shown;

            [Pixels(8, 8)]
            void paint(ItemInput i, Canvas canvas)
            {
                float wave = 0.5 + 0.5 * sin(canvas.centred.x * 0.05 + time * 3);
                shown[group.pixel] = float4(colours.cyan.rgb * wave, 1);
            }

            """),

        new("Mesh", "meshes",
            "A 3D shape built once on the GPU and drawn every frame. A Meshes pool holds the triangles; a [Pass] builds them "
            + "from a surface (Sphere, Torus, Cylinder, a lathed curve…); a [Draw] places them in front of a camera and "
            + "lights them. [Cached] builds it again only when the values change.",
            null, ["make()", "drawn(frame)"],
            """

            static const uint PAGES = 16;      // one 32 x 32 grid (mesh.pagesFor)

            [Cached] [Pages(PAGES)] Meshes pool;

            Camera eye(float2 size) { return Camera(1500 * size.y / 810).orbited(time * 0.3, 0.3); }

            [Pass(128, over: PAGES * 128)]
            void make(uint i) { pool.grid(Sphere(150), i, uint2(32), 0, 0); }

            [Draw(64, 1, shade: look, copies: PAGES)]
            ItemVertex drawn(ItemVertex v) { return pool.pulled(v, Placement3(), eye(v.rest.size)); }

            float4 look(ItemInput i) { return mesh.lit(i, eye(i.rest.size), colours.gold); }

            """),

        new("Particles", "particles",
            "Many small things born, moved and let go on the GPU. Slots hands out free places in the pool and takes them "
            + "back; a Gathered list collects the live ones each frame, and the [Draw] runs once for each of them, a count "
            + "that never leaves the GPU.",
            null, ["begin()", "spawn()", "move()", "spark(frame)"],
            """

            static const uint CAP = 4096;
            static const float LIFE = 1.5;     // seconds

            struct Spark
            {
                float2 at;          // pixels from the item's centre
                float2 velocity;    // pixels a second
                float age;
                uint alive;
            };

            [Size(CAP)] List<Spark> sparks;
            [Size(CAP)] Slots free;
            [Size(CAP)] Gathered<uint> live;

            [Pass(1, over: 1)]
            void begin(uint i) { live.clear(); }

            /// 64 born a frame, flung out from the middle.
            [Pass(64, over: 64)]
            void spawn(uint i)
            {
                uint slot;
                if (!free.take(slot))
                    return;
                float a = random(float2(i, time)) * TAU;
                Spark s;
                s.at = float2(0, 0);
                s.velocity = float2(cos(a), sin(a)) * (80 + 160 * random(float2(time, i)));
                s.age = 0;
                s.alive = 1;
                sparks[slot] = s;
            }

            [Pass(64, over: CAP)]
            void move(uint i)
            {
                Spark s = sparks[i];
                if (s.alive == 0)
                    return;
                s.age += dt;
                s.velocity.y += 200 * dt;
                s.at += s.velocity * dt;
                if (s.age > LIFE)
                {
                    sparks[i].alive = 0;
                    free.give(i);
                    return;
                }
                sparks[i] = s;
                live.add(i);
            }

            [Draw(1, 1, shade: sparkLook, copies: live)]
            ItemVertex spark(ItemVertex v)
            {
                Spark s = sparks[live[copy]];
                v.position = v.rest.position + s.at + (v.uv - 0.5) * 8;
                v.local = (v.uv - 0.5) * 8;
                return v;
            }

            float4 sparkLook(ItemInput i)
            {
                Spark s = sparks[live[copy]];
                return float4(colours.orange.rgb, Circle(4).filled(i.local, 1) * (1 - s.age / LIFE));
            }

            """),
    ];
}
