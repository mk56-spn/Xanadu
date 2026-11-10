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

    float inside = sdf.fill(d, 1.5);
    float outline = sdf.stroke(d + 6, 4, 1.5);
    float glow = sdf.glow(d, 80, 3);

    colour c = colours.midnightBlue;
    c.rgb += colours.deepPink.rgb * glow * 0.6;
    c = lerp(c, colours.hotPink, inside);
    c = lerp(c, colours.white, outline);
    return c;
}
