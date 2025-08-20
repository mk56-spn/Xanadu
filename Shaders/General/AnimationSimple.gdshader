shader_type canvas_item;

#define OVERRIDE_DEFAULT_VERTEX

#include "res://Shaders/Base.gdshaderinc"
#include "res://Shaders/NoteBase.gdshaderinc"

instance uniform float animation_start_time = 3.;
instance uniform float animation_duration = 10.;
instance uniform int easing_index = 27; // Default to easeInOutBack
instance uniform vec2 translation = vec2(0.0, -150.0); // Animate 150px upwards
instance uniform float rotation_degrees = 0.0;
instance uniform vec2 scaled = vec2(1.5, 1.5); // End at 150% size
instance uniform vec2 center = vec2(0.0, 0.0); // Pivot point for scale/rotation


void vertex() {
	// --- 1. Calculate Progress ---
	// Normalize the current time against the animation's start and duration.
	// clamp() ensures the value stays between 0.0 and 1.0.
	float progress = clamp((song_pos - animation_start_time) / animation_duration, 0.0, 1.0);
	// --- 2. Apply Easing ---
	// A single, clean call to our library function.
	float eased_progress = getEasing(easing_index, progress) ;

	// --- 3. Apply Transformations ---
	// Start with the original vertex position.
	vec2 transformed_vertex = VERTEX;
	// Scale: Interpolate from original scale (1.0) to target scale.
	vec2 scale_factor = mix(vec2(1.0), scaled, eased_progress);
	transformed_vertex = (transformed_vertex - center) * scale_factor + center;

	// Rotate: Interpolate from 0 to target rotation.
	float angle_rad = radians(rotation_degrees) * eased_progress;
	float s = sin(angle_rad);
	float c = cos(angle_rad);
	mat2 rotation_matrix = mat2(vec2(c, -s), vec2( s, c));
	transformed_vertex = rotation_matrix * (transformed_vertex - center) + center;

	// Translate: Interpolate from 0 to target translation.
	transformed_vertex += translation * eased_progress;

	VERTEX = transformed_vertex;
}
