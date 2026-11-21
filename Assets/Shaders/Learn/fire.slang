// A flame, built in six steps: noise that rises, shaped by a tapering mask and
// coloured by temperature (xanadu.noise, colours.inferno, tonemap).
import xanadu;

[Default(1)] uniform float heat;

float4 shade(ItemInput i)
{
    Step panel = steps.split(6);
    if (panel.gap)
        return panel.backdrop;
    float2 p = panel.canvas.centred;
    float up = panel.uv.y;   // 0 at the top of the panel, 1 at the bottom

    // 1. Billowed noise (|n| per layer): puffy shapes with creases.
    let flames = Simplex(50).billowed(5).evolved(time * 0.8);
    if (panel.stopsAt(0))
        return panel.marked(colour(float3(flames.amount())));

    // 2. Rising: the noise slides up over time, faster than it changes.
    float n = flames.moved(float2(0, -time * 90)).amount();
    if (panel.stopsAt(1))
        return panel.marked(colour(float3(n)));

    // 3. A teardrop mask, swayed by the noise so the flame flickers side to side.
    float sway = (n - 0.5) * 50 * (1 - up);
    float width = lerp(6, 75, sqrt(up));
    float mask = saturate(1 - abs(p.x + sway) / width) * smoothstep(0.02, 0.5, up) * smoothstep(1, 0.85, up);
    if (panel.stopsAt(2))
        return panel.marked(colour(float3(mask)));

    // 4. The temperature: the mask eaten away by the noise, more towards the tip, so the top
    //    breaks into tongues while the base stays solid.
    float t = saturate((mask - (1 - n) * lerp(1.1, 0.35, up)) * 1.6 * heat);
    if (panel.stopsAt(3))
        return panel.marked(colour(float3(t)));

    // 5. Coloured by temperature: inferno runs black, red, orange, pale yellow.
    colour c = colours.inferno(t);
    if (panel.stopsAt(4))
        return panel.marked(c);

    // 6. As light: hot enough is far past white, then tonemapped back like film; a glow at the base.
    colour hdr = colour(colours.inferno(t).linearised().rgb * (1 + 6 * t * t));
    hdr.rgb += colours.orangeRed.linearised().rgb * Circle(40).moved(float2(0, panel.canvas.size.y * 0.4)).haloed(120, 2) * 0.6;
    return panel.marked(tonemap.aces(hdr));
}
