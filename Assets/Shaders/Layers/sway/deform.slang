import xanadu;

ItemVertex deform(ItemVertex v)
{
    float seed = random(float2(floor(v.uv.x), xif_seed));
    float up = 1.0 - saturate(v.uv.y);
    float bend = looks.wind(v.position, seed, sway_wind.y, sway_wind.z) * sway_wind.x * unit;
    // The top swings out and dips a little as it goes over: bending, not sliding.
    v.position.x += bend * up * up;
    v.position.y += abs(bend) * up * up * up * 0.3;
    return v;
}
