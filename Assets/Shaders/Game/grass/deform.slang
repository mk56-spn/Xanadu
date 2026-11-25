import xanadu;

ItemVertex deform(ItemVertex v)
{
    float seed = random(float2(floor(v.uv.x), xif_seed));
    float up = 1 - saturate(v.uv.y);
    float bend = looks.wind(v.position, seed, grass_wind.y, grass_wind.z) * grass_wind.x * unit;
    // The tip swings out and dips a little as it goes over: a stalk bending, not sliding.
    v.position.x += bend * ease.inQuad(up);
    v.position.y += abs(bend) * ease.inCubic(up) * 0.3;
    return v;
}
