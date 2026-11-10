// A Xanadu shader, written like a level's shader slot.
//
// Uniforms, then
//     float4 shade(ItemInput i)
// returning the pixel's colour. `import xanadu;` brings the whole library:
// shapes (Box, Circle, … with .rotate .move .repeat .fill .glow …), and under
// their own names color., ease., blend., gradient., pattern.
// ItemInput gives you uv (0..1 across the stage), centred and halfSize (pixels
// from the stage's centre, y down), time, screenUv and more (see
// Assets/Shaders/Library/xanadu/stage.slang).
//
// The lab's working copies (playground.slang, editor.slang) are git-ignored.
import xanadu;

// Provided by the lab: declare any of these and it is set every frame;
// leave one out and it isn't. (centre, time, mouse, resolution)
uniform float2 centre;       // the pointer, in i.centred's space (pixels from the stage centre)
uniform float time;          // seconds

[Default(0.6)] uniform float speed;

float4 shade(ItemInput i)
{
    float2 p = i.centred - centre;

    // Eight rounded boxes in a ring round the pointer, the ring turning one way
    // and each box the other; a breathing circle's outline around them.
    let petals = Box(float2(56), 10).rotate(-time * speed * 2).repeat(8, 160).rotate(time * speed);
    let ring = Circle(160 + 20 * sin(time * 2)).onion(2);

    float3 sky = lerp(color.midnightBlue.rgb, color.darkSlateBlue.rgb, ease.inOutSine(i.uv.y));
    float3 hue = color.hsv(frac(time * 0.05 + i.uv.x * 0.3), 0.7, 1);
    float3 colour = lerp(sky, hue, petals.fill(p, 1.5));
    colour += color.gold.rgb * ring.glow(p, 12, 3) * 0.6;
    return float4(colour, 1);
}
