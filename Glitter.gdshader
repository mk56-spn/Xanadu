// glitter_sparkle.gdshader
shader_type canvas_item;


// --- Glitter Controls ---
group_uniforms glitter_settings;
// The vec2 representing the light source (e.g., mouse position).
// This is the core uniform for the interaction.
uniform vec2 light_position;
uniform vec4 glitter_color : source_color = vec4(1.0, 1.0, 0.8, 1.0);
uniform float sparkle_density : hint_range(0.9, 0.999) = 0.985;
uniform float shininess : hint_range(1, 200) = 80.0;
uniform float time_speed : hint_range(0.0, 2.0) = 0.4;
uniform bool use_rainbow_colors = true;

// --- Noise Textures ---
group_uniforms noise_maps;
// Noise to decide WHERE sparkles can appear. Must be a 3D NoiseTexture2D.
uniform sampler2D sparkle_map;
// Noise to decide the "angle" of each glitter flake.
uniform sampler2D normal_map;
// Optional noise to give each sparkle a unique color.
uniform sampler2D color_map;


// Function to convert HSV to RGB color space.
vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}


void fragment() {
	// Get the base color of the sprite/texture.
	vec4 base_color = texture(TEXTURE, UV);

	// Animate the third dimension of our noise lookup using time.
	float animated_time = TIME * time_speed;

	// Sample the sparkle location map. The .r channel gives us a noise value.
	// We use vec3 for the lookup because we configured the NoiseTexture2D as 3D.
	float sparkle_value = texture(sparkle_map, vec3(UV, animated_time)).r;

	// The final color starts as the base color.
	vec4 final_color = base_color;

	// If the noise value is above our density threshold, it's a potential sparkle.
	if (sparkle_value > sparkle_density) {

		// --- Calculate Sparkle Intensity based on Light Position ---

		// 1. Get the direction from this pixel (UV) to the light source.
		vec2 light_dir = normalize(light_position - UV);

		// 2. Get the "normal" or angle of this specific glitter flake from another noise map.
		// We sample R and G channels to get a 2D vector.
		vec2 flake_normal = texture(normal_map, UV).rg;
		// The texture gives us values in [0, 1], so we remap them to [-1, 1].
		flake_normal = normalize(flake_normal * 2.0 - 1.0);

		// 3. Use the dot product to see how well the flake's angle aligns with the light direction.
		// This simulates a reflection. max(0.0, ...) ensures we only get positive reflections.
		float light_alignment = max(0.0, dot(flake_normal, light_dir));

		// 4. Use pow() to create a very sharp, bright highlight. High shininess = smaller, sharper sparkle.
		float sparkle_intensity = pow(light_alignment, shininess);

		// --- Determine Sparkle Color ---
		vec4 current_glitter_color = glitter_color;
		if (use_rainbow_colors) {
			// Use a third noise map to pick a random hue for a rainbow effect.
			float hue = texture(color_map, UV).r;
			current_glitter_color = vec4(hsv2rgb(vec3(hue, 1.0, 1.0)), 1.0);
		}

		// Mix the base color with the glitter color based on the final calculated intensity.
		// This makes the sparkle "fade in" smoothly instead of just popping.
		final_color = mix(base_color, current_glitter_color, sparkle_intensity);
	}

	COLOR = final_color;
}
