// Metaballs: a handful of circles drifting about, melted into one blob,
// filled, outlined and glowing.
import xanadu;

[Default(60)] uniform float melt;

static const int Balls = 7;

float4 shade(ItemInput i)
{
    float2 p = i.canvas.centred;
    float d = 1e5;
    for (int b = 0; b < Balls; b++)
    {
        float seed = b * 1.7;
        float2 at = float2(sin(time * 0.7 + seed * 2.1), cos(time * 0.5 + seed * 1.3)) * float2(320, 180);
        Circle ball = Circle(50 + 25 * sin(seed));
        ball.move(at);
        d = sdf.melt(d, ball.distance(p), melt);
    }

    colour c = colours.midnightBlue;
    c.paint(sdf.glow(d, 80, 3), colours.deepPink.withAlpha(0.6), blend.Mode.Screen);
    c.paint(sdf.fill(d, 1.5), colours.hotPink);
    c.paint(sdf.stroke(d + 6, 4, 1.5), colours.white);
    return c;
}
