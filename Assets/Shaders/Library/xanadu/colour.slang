// xanadu.colour: colour-space helpers for effects.
module colour;

/// Hue, saturation and value (each 0..1) to RGB.
public float3 hsvToRgb(float3 c)
{
    float4 k = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    float3 p = abs(frac(c.xxx + k.xyz) * 6.0 - k.www);
    return c.z * lerp(k.xxx, saturate(p - k.xxx), c.y);
}

/// The same hue at full saturation and value, cycling red, green, blue.
public float3 hueCycle(float hue)
{
    return saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
}
