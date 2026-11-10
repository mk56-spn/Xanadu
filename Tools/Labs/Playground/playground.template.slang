// A Xanadu shader, written like a level's shader slot.
//
// Uniforms, then
//     float4 shade(ItemInput i)
// returning the pixel's colour. `import xanadu;` brings the whole library:
// shapes (Box, Circle, … with .rotated .moved .repeat .fill .glow …), and under
// their own names colours., ease., blend., gradient., pattern.
// ItemInput gives you uv (0..1 across the stage), centred and halfSize (pixels
// from the stage's centre, y down), time, screenUv and more (see
// Assets/Shaders/Library/xanadu/stage.slang).
//
// The lab's working copies (playground.slang, editor.slang) are git-ignored.
import xanadu;

// Provided by the lab, no need to declare them: time (seconds), mousePos (the
// pointer in i.centred's space), mouse (0..1 on the stage), resolution.

[Default(0.6)] uniform float speed;

float4 shade(ItemInput i)
{
    float2 p = i.centred - mousePos;

    // Eight rounded boxes in a ring round the pointer, the ring turning one way
    // and each box the other; a breathing circle's outline around them.
    let petals = Box(float2(56), 10).rotated(-time * speed * 2).repeat(8, 160).rotated(time * speed);
    let ring = Circle(160 + 20 * sin(time * 2)).onion(2);

    colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, ease.inOutSine(i.uv.y));
    colour hue = colours.hsv(frac(time * 0.05 + i.uv.x * 0.3), 0.7, 1);
    colour shaded = lerp(sky, hue, petals.fill(p, 1.5));
    shaded.rgb += colours.gold.rgb * ring.glow(p, 12, 3) * 0.6;
    return shaded;
}
