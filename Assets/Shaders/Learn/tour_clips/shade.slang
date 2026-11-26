// Clips: spans of time on a clock, read as progress. Each `Clip name = …` shows as a row on the timeline
// under the preview, found by running this code, not by reading it: a dot pops on a two-second loop, a
// trip runs there and back over four beats along keyed points, and four rings start a quarter second
// apart (one clip, staggered: one row, its later copies fading after it).
import xanadu;

float4 shade(Canvas canvas)
{
    Clip pop = clock.looped(2).clip(0.2, 0.6).eased(eases.outBack);
    Clip trip = clock.beats.looped(4).clip(0, 2).bounced();
    Keys<float2> path = Keys<float2>(float2(-180, 60))
        .to(0.5, float2(0, -90), eases.outQuad)
        .to(1, float2(180, 60), eases.inQuad);

    colour c = colours.transparent;
    c.paint(Circle(30 * pop.progress).moved(float2(0, 110)), colours.gold);
    c.paint(Circle(14).moved(path.at(trip)), colours.cyan);
    for (int k = 0; k < 4; k++)
    {
        Clip ring = clock.looped(2).clip(0, 0.5).staggered(k, 0.25).eased(eases.outQuad);
        c.paint(Circle(20 + 60 * ring.progress).hollowed(3).moved(float2(-210 + 140 * k, -110)), colours.pink.withAlpha(1 - ring.progress));
    }

    return c;
}
