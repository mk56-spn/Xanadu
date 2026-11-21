// xanadu.steps: showing how a shader is built, one step per panel. split cuts
// the quad into panels and points everything that draws "at the pixel being
// shaded" (shapes, gradients, noise, pattern.tile) at this pixel's place in its
// own panel, so the shader runs unchanged in each. Return early at each step
// and every panel shows the picture as it was at that point:
//
//     float4 shade(ItemInput i)
//     {
//         Step panel = steps.split(4);
//         if (panel.gap) return panel.backdrop;
//         let blob = Circle(60).melted(Box(float2(80)).moved(float2(50, 0)), 20);
//         if (panel.stopsAt(0)) return blob.shown();          // 1: the distance
//         colour c = colours.midnightBlue;
//         c.paint(blob, colours.gold);
//         if (panel.stopsAt(1)) return c;                     // 2: filled
//         c.paint(blob.stroked(3), colours.white);
//         if (panel.stopsAt(2)) return c;                     // 3: outlined
//         c.paint(blob.haloed(40), colours.gold, blend.add);
//         return panel.marked(c);                             // 4: glow, numbered
//     }
//
// steps.slideshow(count, seconds) shows the same steps one after another, full size:
// a slideshow, for a preview.
module steps;
import xanadu.math;
import xanadu.stage;
import xanadu.colour;

/// Which step a pixel shows, and the panel it sits in.
public struct Step
{
    /// Which step this pixel's panel shows: 0 for the first.
    public int index;
    /// How many steps there are.
    public int count;
    /// True between panels: return `backdrop` there.
    public bool gap;
    /// The colour between panels.
    public colour backdrop;
    /// This panel, as the quad was: its size and this pixel's place from its middle (`centred`).
    public Canvas canvas;
    /// 0..1 across this panel.
    public float2 uv;

    /// True in the panel that stops at step `k`: return what you have there.
    public bool stopsAt(int k) { return index == k; }

    /// True in every panel from step `k` on: for a part that stays once it has appeared.
    public bool from(int k) { return index >= k; }

    /// `picture` with the step's number marked in the panel's top left, as dots (1 dot for the first
    /// step); every fifth a bar, so it counts at a glance.
    public colour marked(colour picture)
    {
        float2 corner = uv * canvas.size;
        int n = index + 1;
        float cover = 0.0;
        for (int k = 0; k < n && k < 20; k++)
        {
            bool fifth = (k + 1) % 5 == 0;
            float2 at = float2(10.0 + 9.0 * float(k), 10.0);
            float2 d = abs(corner - at) - (fifth ? float2(1.5, 5.0) : float2(0.0));
            float dist = fifth ? length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) : length(corner - at) - 3.0;
            cover = max(cover, 1.0 - smoothstep(-0.5, 0.5, dist));
        }
        float3 rgb = lerp(picture.rgb, float3(0.0), saturate(cover * 1.6) * 0.6);
        rgb = lerp(rgb, float3(1.0), cover);
        return colour(rgb, max(picture.a, cover));
    }
}

public namespace steps
{
    /// The quad cut into `count` panels, as square as fits (a row of up to 4, then a grid), `gap`
    /// pixels apart, and everything that draws at the pixel being shaded moved into this pixel's
    /// panel. Call it first in `shade`.
    public Step split(int count, float gap = 6.0)
    {
        Canvas whole = shading.canvas;
        int n = max(count, 1);
        // Columns: as many as keep each panel closest to the quad's own shape.
        float aspect = whole.size.x / max(whole.size.y, 1.0);
        int columns = clamp(int(round(sqrt(float(n) * aspect))), 1, n);
        int rows = (n + columns - 1) / columns;
        float2 grid = float2(columns, rows);
        float2 panel = (whole.size - gap * (grid - 1.0)) / grid;
        float2 fromCorner = whole.centred + whole.size * 0.5;
        float2 cell = floor(fromCorner / (panel + gap));
        float2 inside = fromCorner - cell * (panel + gap);

        Step s;
        s.count = n;
        s.index = int(cell.y) * columns + int(cell.x);
        s.gap = any(inside > panel) || s.index >= n || any(cell >= grid);
        s.backdrop = colour(0.06, 0.06, 0.09);
        s.uv = inside / max(panel, float2(1.0));
        s.canvas = whole;
        s.canvas.size = panel;
        s.canvas.centred = inside - panel * 0.5;
        s.canvas.position = whole.position;
        shading.canvas = s.canvas;
        return s;
    }

    /// The same steps shown one after another, full size, each for `seconds`: a slideshow.
    /// Nothing is moved; panel.uv runs across the whole quad.
    public Step slideshow(int count, float seconds = 1.5)
    {
        Step s;
        s.count = max(count, 1);
        s.index = int(mod(floor(time / max(seconds, 1e-3)), float(s.count)));
        s.gap = false;
        s.backdrop = colour(0.06, 0.06, 0.09);
        s.canvas = shading.canvas;
        s.uv = s.canvas.centred / max(s.canvas.size, float2(1.0)) + 0.5;
        return s;
    }
}
