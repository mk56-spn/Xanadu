shader_type canvas_item;

#include "uid://u421l6gqbhvy"

float hash(float n) { return fract(sin(n * 12.9898) * 43758.5453); }

void vertex() {
	SIZE
	PERSPECTIVE
}

void fragment() {
	PERSPECTIVE_FRAGMENT
	vec2 pixel_perspective_uv = uv * canvas_half_size;
	float main_ring = sdCircle(pixel_perspective_uv, 25.);
	main_ring = abs(main_ring) - 10.;

    // --- START: ADDED LOGIC FOR RANDOM SIZE ---
    float num_repetitions = 3.0;

    // 1. Get an "instance ID" (0, 1, or 2) from the original, unfolded UV.
    float original_angle = atan(pixel_perspective_uv.y, pixel_perspective_uv.x);
    float slice_angle = (2.0 * PI) / num_repetitions;
    float instance_id = floor(mod(original_angle + (2.0 * PI), (2.0 * PI)) / slice_angle);

    // 2. Use the ID to get a deterministic random value (0.0 to 1.0).
    float random_value = hash(instance_id);

    // 3. Map this random value to a desired range for your cutout size.
    float min_size = PI * 0.5 * 0.4; // Example minimum size
    float max_size = PI * 0.5 * 0.7; // Example maximum size
    float cut_out_size = mix(min_size, max_size, random_value);
    // --- END: ADDED LOGIC FOR RANDOM SIZE ---


	vec2 polar_uv = opRepPolar(pixel_perspective_uv, num_repetitions); // Use num_repetitions variable
	polar_uv = rotate(polar_uv, PI * -0.5);

	// The line below now uses the randomized `cut_out_size`
	float partial_ring = sdRing(polar_uv, vec2(sin(cut_out_size), cos(cut_out_size)), 35.,10);
	float final = max(-partial_ring, main_ring);

	COLOR *= renderSDFWithOutline(final+1.,vec4(1.), vec4(.8),1.,0.5);
}
