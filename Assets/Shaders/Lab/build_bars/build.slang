// BUILD, the smallest that shows the order: `grow` writes a wave of bar heights, then
// `smooth` runs `passes` times, each one a dispatch of its own reading what the last wrote,
// ping-ponging between the two halves of one buffer. SHADE draws the bars.
import xanadu;

static const uint BARS = 256;

// Two halves: the heights `grow` wrote, then each smoothing's result in the other half.
[Buffer(2 * BARS)] RWStructuredBuffer<float> heights;

// How many smoothing passes build() runs: each blurs the bars a little more.
[Options("0|1|2|3|4|5|6|7|8")] [Default(4)] uniform int passes;

[Pass(64, over: BARS)]
void grow(uint i)
{
    float x = i / float(BARS);
    heights[i] = 0.5 + 0.35 * sin(x * 40 + time * 3) * step(0.5, frac(x * 4 + time * 0.2));
}

/// Half `from` into the other half, each bar the mean of it and its neighbours.
[Pass(64, over: BARS)]
void smooth(uint i, uint from)
{
    uint a = from * BARS, b = (1 - from) * BARS;
    float left = heights[a + max(i, 1) - 1], right = heights[a + min(i + 1, BARS - 1)];
    heights[b + i] = (left + heights[a + i] + right) / 3;
}

void build()
{
    grow();
    for (uint k = 0; k < passes; k++)
        smooth(k & 1);
}
