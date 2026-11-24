// SHADE: the bars build() left, from the half its last pass wrote.
import xanadu;

float4 shade(ItemInput i)
{
    uint bar = min(uint(i.uv.x * BARS), BARS - 1);
    float height = heights[(passes & 1) * BARS + bar];
    colour sky = lerp(colours.midnightBlue, colours.darkSlateBlue, i.uv.y);
    bool inside = 1 - i.uv.y < height && frac(i.uv.x * BARS) > 0.15;
    return inside ? lerp(colours.gold, colours.orangeRed, height) : sky;
}
