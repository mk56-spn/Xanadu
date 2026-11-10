// The song on the editor's timeline, drawn as one quad over the ruler
// (Systems/Editor/EditorWaveformDrawer.cs). The item's texture holds the three
// bands' running sums (WaveformData.RunningSums), one step a texel, R/G/B =
// low/mid/high, rows of wave_data.z texels. Each pixel finds its column, a
// fixed multiple of time, averages each band over it from two sums apiece (as
// WaveformData.Average does), and shows the shortest band that reaches it, so
// the bands read as overlaid bars, tallest behind.
import xanadu.stage;

struct Params
{
    // Window start and end (ms), ms per column, audio lag (ms).
    float4 wave_window;
    // Steps per ms, steps, texels per row of the sums texture.
    float4 wave_data;
    // Ruler height (px), tallest half-bar (px), mid scale, high scale.
    float4 wave_box;
    float4 wave_low;
    float4 wave_mid;
    float4 wave_high;
};
[[vk::binding(1, 3)]] ConstantBuffer<Params> params;

float3 sumsAt(int index, int row)
{
    return itemTexture.Load(int3(index % row, index / row, 0)).rgb;
}

// The running sums at a time, between steps by how far into the step it is.
float3 runningAt(float timeMs)
{
    float steps = params.wave_data.y;
    int row = int(params.wave_data.z);
    float position = clamp(timeMs * params.wave_data.x, 0.0, steps);
    int step = min(int(position), int(steps) - 1);
    float3 a = sumsAt(step, row);
    float3 b = sumsAt(step + 1, row);
    return a + (b - a) * (position - float(step));
}

[shader("fragment")]
float4 shade(ItemVaryings v) : SV_Target
{
    // The texture holds numbers, not colour: input() would sample it, so read the varyings directly.
    float4 window = params.wave_window;
    float time = lerp(window.x, window.y, v.uv.x);
    float column = floor(time / window.z);
    float start = column * window.z + window.w;
    float end = start + window.z;

    float3 average = (runningAt(end) - runningAt(start)) / (window.z * params.wave_data.x);
    float3 half = average * float3(1.0, params.wave_box.z, params.wave_box.w) * params.wave_box.y;
    float y = abs((v.uv.y - 0.5) * params.wave_box.x);

    // The shortest band covering the pixel is the one on top; on a tie the later band, as drawn last.
    float4 colour = float4(0.0);
    float shortest = 1e30;
    if (half.x >= 0.5 && y <= half.x && half.x <= shortest) { shortest = half.x; colour = params.wave_low; }
    if (half.y >= 0.5 && y <= half.y && half.y <= shortest) { shortest = half.y; colour = params.wave_mid; }
    if (half.z >= 0.5 && y <= half.z && half.z <= shortest) { shortest = half.z; colour = params.wave_high; }
    return colour * v.colour;
}
