// Xanadu Labs shader playground. Save this file and the stage redraws.
//
// It's written like a level's shader slot: uniforms, then
//     float4 shade(ItemInput i)
// returning the pixel's colour. `import xanadu;` brings the whole library:
// sdf shapes, easings, colours, blend modes, patterns, gradients, curves.
// ItemInput gives you uv (0..1 across the stage), centred and halfSize (pixels
// from the stage's centre, y down), time, screenUv and more (see
// Assets/Shaders/Library/xanadu/stage.slang).
//
// Your working copy (playground.slang) is git-ignored: scribble freely.
import xanadu;

// Provided by the lab: declare any of these and it is set every frame;
// leave one out and it isn't. (centre, time, mouse, resolution)
uniform float2 centre;       // the pointer, in i.centred's space (pixels from the stage centre)
uniform float time;          // seconds

[Default(0.6)] uniform float speed;

float4 shade(ItemInput i)
{
    // A ring of rounded boxes turning round the pointer, over a slow gradient.
    float2 p = rotate(i.centred - centre, time * speed);
    float2 cell = opRepPolar(p, 8.0) - float2(160.0, 0.0);
    float box = sdRoundedBox(rotate(cell, -time * speed * 2.0), float2(28.0), float4(10.0));
    float ring = opOnion(sdCircle(i.centred - centre, 160.0 + 20.0 * sin(time * 2.0)), 2.0);

    float3 sky = lerp(MIDNIGHT_BLUE.rgb, DARK_SLATE_BLUE.rgb, inOutSine(i.uv.y));
    float3 colour = sky;
    colour = lerp(colour, hsvToRgb(float3(frac(time * 0.05 + i.uv.x * 0.3), 0.7, 1.0)), sdfAlpha(box, 1.5));
    colour += GOLD.rgb * outerGlowSDF(-abs(ring), 12.0, 1.0, 3.0) * 0.6;
    return float4(colour, 1.0);
}
