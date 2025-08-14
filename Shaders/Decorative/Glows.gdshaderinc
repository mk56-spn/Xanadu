const float eps = 0.02;

float flare(float uv_axis,){
	  return 0.02 / (abs(uv_axis) +eps);
}

vec4 calculateLensFlare(vec2 uv, vec4 colour) {
    float flareIntensity;
   	float horizontalFlare = flare(uv.x) * smoothstep(1.0, 0.0, abs(uv.y));
    float verticalFlare = flare(uv.y)  * smoothstep(1.0, 0.0, abs(uv.x));

    // Diagonal flares
    float diagonalFlare1 = 0.04 / (abs(uv.x + uv.y) + eps);
    float diagonalFlare2 = 0.04 / (abs(uv.x - uv.y) + eps);

    // Combine flares
    flareIntensity = horizontalFlare + verticalFlare + diagonalFlare1 + diagonalFlare2;
    flareIntensity *= exp(-length(uv) * 2.0); // Fade with distance



    return colour * flareIntensity * (0.3); // Adjust intensity here
}