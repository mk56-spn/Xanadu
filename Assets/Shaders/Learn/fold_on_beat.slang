// A card that folds into a ball on every beat and springs back flat (xanadu.mesh morphed,
// beats.bounce). The whole grid is one surface. Play the shader tool's timeline to see it
// move: it runs on the beat, as it would in a level.
import xanadu;

[Default(colours.hotPink)] uniform colour tint;
[Default(2)] uniform float every;

Camera camera(float s) { return Camera(1100 * s).orbited(sin(time * 0.3) * 0.3, 0.2); }

/// How folded it is: snaps into the ball right on the beat (overshooting a little), holds,
/// then eases back flat by half way to the next.
float folded()
{
    float t = beats.phase(every);
    float closing = ease.outBack(saturate(t * 8));
    float opening = 1 - ease.inOutCubic(saturate((t - 0.2) / 0.3));
    return max(closing * opening, 0);
}

float3 shaped(float2 uv, float s)
{
    let card = Sheet(float2(560, 320)).waving(10, 1, time);
    let ball = Sphere(150);
    return card.morphed(ball, folded()).turned(float3(0.2, sin(time * 0.5) * 0.5, 0)).at(uv) * s;
}

ItemVertex deform(ItemVertex v)
{
    float s = v.rest.size.y / 810;
    v.place(shaped(v.uv, s), camera(s));
    return v;
}

float4 shade(ItemInput i)
{
    float s = i.rest.size.y / 810;
    // The card's face: stripes and a ring, painted on in uv, so they wrap round the ball.
    colour c = tint.mixed(colours.indigo, i.uv.y);
    c.paint(Stripes(0.06).filled(i.uv * float2(1.75, 1), 0.004) * 0.35, colours.white);
    c.paint(Circle(0.18).hollowed(0.02).filled((i.uv - 0.5) * float2(1.75, 1), 0.004), colours.white);
    // place() gives each corner its depth, so the ball's near side covers its far side by
    // itself; the card's back just shows paler.
    if (!mesh.facing())
        c = c.greyed().brightened(0.5);

    const float e = 1e-3;
    float3 p = shaped(i.uv, s);
    float3 n = normalize(cross(shaped(i.uv + float2(0, e), s) - shaped(i.uv - float2(0, e), s), shaped(i.uv + float2(e, 0), s) - shaped(i.uv - float2(e, 0), s)));
    float3 toEye = normalize(camera(s).eye - p);
    if (dot(n, toEye) < 0)
        n = -n;
    float3 key = normalize(float3(-0.5, -0.8, -0.6));
    float3 rgb = c.rgb * (0.25 + 0.75 * lights.diffuse(n, key)) + lights.specular(n, key, toEye, 40) * 0.4;
    return colour(rgb, 1);
}
