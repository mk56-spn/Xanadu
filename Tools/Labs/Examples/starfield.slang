// Starfield: layers of stars on a grid, each cell one star at a random spot,
// the layers sliding towards you and fading in from the distance.
import xanadu;

[Default(0.08)] uniform float speed;

static const int Layers = 5;

float4 shade(Canvas canvas)
{
    float2 p = canvas.centred;
    float3 sky = float3(0);
    for (int l = 0; l < Layers; l++)
    {
        // 0 far away, 1 right in front; each layer at its own point in the cycle.
        float depth = frac(float(l) / Layers + time * speed);
        float zoom = lerp(0.2, 2.0, depth);
        float fade = smoothstep(0, 0.3, depth) * smoothstep(1, 0.85, depth);

        Tile cell = pattern.tile(p / zoom + l * 731.0, 90);
        float2 spot = (random2(cell.id) - 0.5) * 70;
        float d = length(cell.local - spot) * zoom;
        float twinkle = 0.6 + 0.4 * sin(time * 3 + random(cell.id) * TAU);
        float star = 1.5 / max(d, 0.5) * smoothstep(12, 0, d);

        float3 tint = lerp(colours.lightSkyBlue.rgb, colours.navajoWhite.rgb, random(cell.id + 4.2));
        sky += tint * star * twinkle * fade;
    }

    return colour(sky, 1);
}
