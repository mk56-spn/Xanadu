shader_type canvas_item;
render_mode blend_add;

#include "res://Shaders/Base.gdshaderinc"
// The main color of the flare.
uniform vec4 flare_color : source_color = vec4(1,1,1,1.);
// The overall brightness of the effect.
uniform float intensity : hint_range(0.0, 5.0) = 1;
// How far the horizontal streak extends.
uniform float streak_length : hint_range(0.1, 2.0) = 1.0;
// How thin and sharp the horizontal streak is. Higher values are thinner.
uniform float streak_thinness : hint_range(50.0, 400.0) = 100.0;
// The size of the bright central glow.
uniform float core_size : hint_range(0.0, 0.5) = 0.2;
// The brightness multiplier for the central glow.
uniform float core_intensity : hint_range(0.0, 10.0) = 0.5;


void fragment() {
    // Center the UV coordinates so that (0,0) is the middle of the texture.
    // This makes distance and position calculations much simpler.
    vec2 centered_uv = UV - vec2(0.5);
	

    // --- 1. Create the Horizontal Streak ---

    // Calculate the absolute vertical distance from the center line (y=0).
    float dist_y = abs(centered_uv.y);
    // Use a high power function on the inverse distance to create a very bright, thin line.
    // The 'streak_thinness' uniform controls the power, and thus the line's thickness.
    float streak = pow(1.0 - dist_y, streak_thinness);

    // Create a horizontal falloff so the streak fades out towards the left and right edges.
    // 'streak_length' controls how far it goes before fading.
    float falloff_x = 1.0 - abs(centered_uv.x * 2.0 / streak_length);
    // Use smoothstep to make the fade-out smooth instead of a sharp linear cutoff.
    falloff_x = smoothstep(0.0, 1.0, falloff_x);
    streak *= falloff_x;


    // --- 2. Create the Bright Core ---

    // Calculate the radial distance from the exact center point.
    float dist_from_center = length(centered_uv);
    // Create a small, soft circle at the center using smoothstep.
    float core = 1.0 - smoothstep(0.0, core_size, dist_from_center);
    // Use pow to make the core's falloff sharper and more concentrated.
    core = pow(core, 2.0);


    // --- 3. Combine and Finalize Color ---

    // Add the streak and the core together. The core is multiplied by its own
    // intensity to make it significantly brighter than the streak.
    float total_intensity = streak + core * core_intensity;
    // Apply the master intensity control to the entire effect.
    total_intensity *= intensity;

    // Set the final pixel color.
    // MODIFIED: To prevent the "premultiplied alpha" issue, we output the raw color
    // in the RGB channels and put the full intensity ONLY in the alpha channel.
    // The 'blend_add' mode will then correctly multiply RGB by Alpha before adding.
    COLOR *= vec4(flare_color.rgb, total_intensity);
}