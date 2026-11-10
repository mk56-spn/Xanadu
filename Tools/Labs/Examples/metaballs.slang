// Metaballs: a handful of circles drifting about, melted into one blob,
// filled, outlined and glowing.
import xanadu;

[Default(60)] uniform float melt;

static const int Balls = 7;

// The shapes' melt joins two at a time and its type grows with each join, so a
// loop can't build one: melt the distances by hand (Quilez's polynomial smooth min).
float smoothMin(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1 - h);
}

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
        d = smoothMin(d, ball.distance(p), melt);
    }

    // fill, stroke and glow belong to shapes; a plain distance needs them written out.
    float aa = fwidth(d);
    float inside = 1 - smoothstep(-aa, aa, d);
    float outline = 1 - smoothstep(2 - aa, 2 + aa, abs(d + 6));
    float glow = pow(saturate(1 - abs(d) / 80), 3);

    colour c = colours.midnightBlue;
    c.rgb += colours.deepPink.rgb * glow * 0.6;
    c = lerp(c, colours.hotPink, inside);
    c = lerp(c, colours.white, outline);
    return c;
}
