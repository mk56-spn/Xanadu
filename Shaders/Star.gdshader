shader_type canvas_item;
render_mode blend_add;

#include "Base.gdshaderinc"

const float eps = 0.04;

float flare(float uv_axis,){
	  return 0.02 / (abs(uv_axis) +eps);
}

SIZE_VERTEX_EXCLUSIVE
vec4 calculateLensFlare(vec2 uv) {
    float flareIntensity;
   	float horizontalFlare = flare(uv.x) * smoothstep(1.0, 0.0, abs(uv.y));
    float verticalFlare = flare(uv.y)  * smoothstep(1.0, 0.0, abs(uv.x));

    // Diagonal flares
    float diagonalFlare1 = 0.04 / (abs(uv.x + uv.y) + eps);
    float diagonalFlare2 = 0.04 / (abs(uv.x - uv.y) + eps);

    // Combine flares
    flareIntensity = horizontalFlare + verticalFlare + diagonalFlare1 + diagonalFlare2;
    flareIntensity *= exp(-length(uv) * 2.0); // Fade with distance

    // Add color variation to the flare
    vec4 flareColor = getColour(); // Slightly warm tint
    return flareColor * flareIntensity * 0.3; // Adjust intensity here
}

void fragment() {
    vec2 uv = pixel_uv  / canvas_half_size * 0.35;
     COLOR *= calculateLensFlare(uv);
}
