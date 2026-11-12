// Clips: a card slides in, turns and fades; a dot rides a keyed path; a ring hits on the beat;
// F flashes the middle and any direction key nudges a pip (click the preview first).
import xanadu;

float4 shade(Canvas canvas)
{
    colour c = colours.midnightBlue;
    Clock loop = clock.looped(2.4);

    Clip slide = loop.clip(0, 0.5).eased(eases.outCubic);
    Clip spin  = slide.then(0.4).eased(eases.inOutSine);
    Clip leave = spin.then(0.3).delayed(0.6).eased(eases.inQuad);

    Box card = Box(float2(160, 90), 18);
    card.move(float2(slide.mixed(-300, 0), -60));
    card.rotate(spin.mixed(0, PI / 2));
    c.paint(card, colours.hotPink.withAlpha(1 - leave.progress));

    Clip trip = loop.clip(0.2, 2).eased(eases.inOutSine);
    Keys<float2> path = Keys<float2>(float2(-280, 120))
        .to(0.5, float2(0, 40), eases.outQuad)
        .to(1, float2(280, 120), eases.inQuad);
    c.paint(Circle(18).moved(path.at(trip)), colours.gold);

    Clip hit = clock.beats.looped(1).clip(0, 0.75).eased(eases.outExpo);
    c.paint(Circle(hit.mixed(20, 90)).moved(float2(260, -120)).stroked(5), colours.deepSkyBlue.withAlpha(1 - hit.progress));

    Tile t = pattern.tile(40);
    Clip rise = loop.clip(0, 0.4).staggered(length(t.id), 0.06).bounced();
    c.paint(Circle(rise.mixed(1, 5)).filled(t.local), colours.white.withAlpha(0.25));

    Clip flash = clock.since(keys.action1).clip(0, 0.35).eased(eases.outQuad);
    c.paint(Circle(flash.mixed(40, 160)), colours.white.withAlpha(0.6 * (1 - flash.progress)), blend.screen);

    Clip nudge = clock.since(keys.anyDirection).clip(0, 0.2).eased(eases.outBack);
    c.paint(Box(float2(nudge.mixed(10, 26)), 4).moved(float2(-300, -150)), colours.lime);
    return c;
}
