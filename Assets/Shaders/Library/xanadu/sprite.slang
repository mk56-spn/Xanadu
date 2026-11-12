// xanadu.sprite: pictures as values, drawn like shapes. A Sprite is one cell of
// a strip (equal pictures side by side in one texture; a lone picture is a
// strip of one), `size` across, centred, then placed like any shape:
//
//     let note = Sprite(notes, 4, glyph, float2(40)).rotated(tilt).moved(offset);
//     c.paint(note.haloed(4), colours.gold, blend.add);   // a glow round its ink
//     c.paint(note.filled(), colours.white);              // its ink
//     colour art = note.sampled();                        // its own colours
//
// Outside its box a sprite is see-through, so neighbouring cells never bleed in.
// Like shapes, it draws at the pixel being shaded unless given a point (a
// tile's local, a warped position), in the same units as that point.
module sprite;
import xanadu.math;
import xanadu.stage;
import xanadu.shape;

public struct Sprite : IPlaced
{
    public Placement placement = Placement();
    /// The strip it is a cell of.
    public Sampler2D strip;
    /// How many cells the strip holds, side by side.
    public int cells;
    /// Which cell, from 0 on the left.
    public int cell;
    /// Its full width and height, before scaling.
    public float2 size;

    public __init(Sampler2D strip, int cells, int cell, float2 size)
    {
        this.strip = strip;
        this.cells = cells;
        this.cell = cell;
        this.size = size;
    }

    /// A lone picture: a strip of one.
    public __init(Sampler2D picture, float2 size)
    {
        this.strip = picture;
        this.cells = 1;
        this.cell = 0;
        this.size = size;
    }

    /// Where `position` falls in its box: 0..1 across it, outside that beyond its edges.
    public float2 boxAt(float2 position)
    {
        return placement.toLocal(position) / size + 0.5;
    }

    /// Its colour at a point of its box (0..1 across), straight; see-through
    /// outside it. `lod` below zero lets the GPU pick the mip; higher is blurrier.
    public float4 sampledInBox(float2 q, float lod = -1.0)
    {
        if (any(q < 0.0) || any(q > 1.0))
            return float4(0.0);

        float2 uv = float2((q.x + float(cell)) / float(cells), q.y);
        return lod < 0.0 ? strip.Sample(uv) : strip.SampleLevel(uv, lod);
    }

    /// Its own colour at `position`, straight: see-through outside its box.
    public float4 sampled(float2 position) { return sampledInBox(boxAt(position)); }

    /// Its own colour at the pixel being shaded, from the quad's centre.
    public float4 sampled() { return sampled(shading.canvas.centred); }

    /// How much of its ink covers `position`, 0..1: its alpha, as a shape's filled.
    public float filled(float2 position) { return sampled(position).a; }

    /// How much of its ink covers the pixel being shaded, from the quad's centre.
    public float filled() { return filled(shading.canvas.centred); }

    /// A soft glow hugging its ink's outline, reaching `reach` past it (in the
    /// units of the position): a centre and two rings of eight samples from the
    /// blurrier mip `lod`, so few samples still look smooth. Keep the strip's
    /// cells clear round their ink by as much, or the glow is cut off.
    public float haloed(float2 position, float reach, float lod = 3.0)
    {
        float2 q = boxAt(position);
        float2 spread = reach / (size * placement.scale);
        if (any(q < -spread) || any(q > 1.0 + spread))
            return 0.0;

        float sum = sampledInBox(q, lod).a * 2.0;
        float weight = 2.0;
        for (int i = 0; i < 8; i++)
        {
            float2 direction = rotate(float2(1.0, 0.0), float(i) * TAU / 8.0);
            sum += sampledInBox(q + direction * spread * 0.5, lod).a * 1.5;
            sum += sampledInBox(q + direction * spread, lod).a;
            weight += 2.5;
        }

        return sum / weight;
    }

    /// A soft glow round its ink at the pixel being shaded, from the quad's centre.
    public float haloed(float reach, float lod = 3.0) { return haloed(shading.canvas.centred, reach, lod); }
}
