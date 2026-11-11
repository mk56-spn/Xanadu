// A Xanadu shader, written like a level's shader slot.
//
// Uniforms, then
//     float4 shade(Canvas canvas)
// returning the pixel's colour (add ItemInput i for uv, the item's colour, screenUv …). `import xanadu;` brings the whole library:
// shapes (Box, Circle, … with .rotated .moved .repeated .filled .haloed …), and under
// their own names colours., ease., blend., gradient., pattern.
// canvas is the stage's quad: canvas.centred (pixels from its centre, y down),
// canvas.size and more; ItemInput has uv (0..1 across the stage), screenUv and
// the rest (see Assets/Shaders/Library/xanadu/stage.slang).
//
// The lab's working copies (playground.slang, editor.slang) are git-ignored.
import xanadu;

// Provided by the lab, no need to declare them: time (seconds), mouse (the
// pointer in pixels from the centre, like canvas.centred), mouseUv (0..1 on
// the stage), resolution.

[Default(0.6)] uniform float speed;

float4 shade(ItemInput i, Canvas canvas)
{
    float2 p = canvas.centred - mouse;

    // Eight rounded boxes in a ring round the pointer, the ring turning one way
    // and each box the other; a breathing circle's outline around them.
    let petals = Box(float2(56), 10).rotated(-time * speed * 2).repeated(8, 160).rotated(time * speed);
    let ring = Circle(160 + 20 * sin(time * 2)).hollowed(2);

    colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, ease.inOutSine(i.uv.y));
    colour hue = colours.hsv(frac(time * 0.05 + i.uv.x * 0.3), 0.7, 1);
    colour shaded = lerp(sky, hue, petals.filled(p, 1.5));
    shaded.rgb += colours.gold.rgb * ring.haloed(p, 12, 3) * 0.6;
    return shaded;
}
