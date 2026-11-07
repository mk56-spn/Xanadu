shader_type canvas_item;
render_mode unshaded;

// A lens flare worked out per pixel over its box. UV runs from -1 to 1
// across the box each way; the flare's settings come in per canvas item.
// It holds still: anything that moves it over time does so from outside.

// Half width, half height (any unit, only their ratio matters), axes (1 or 2), streak thickness.
instance uniform vec4 flare_box = vec4(1.0, 1.0, 1.0, 0.05);
// Core radius, halo radius, halo brightness, intensity.
instance uniform vec4 flare_light = vec4(0.15, 0.9, 0.35, 1.0);
// The flare's colour.
instance uniform vec4 flare_tint = vec4(1.0, 0.6, 0.25, 1.0);
// Streak falloff, heat.
instance uniform vec2 flare_shape = vec2(1.5, 0.7);

// One streak: bright along its axis, fading to nothing at the box's edge,
// a sharp line inside a softer spread that thins out towards the ends.
float streak(float along, float across, float thickness, float falloff) {
	float reach = pow(clamp(1.0 - abs(along), 0.0, 1.0), falloff);
	float width = max(thickness * (0.3 + 0.7 * reach), 1e-4);
	float line = exp(-pow(across / width, 2.0));
	float spread = exp(-pow(across / (width * 5.0), 2.0)) * 0.3 * reach;
	return reach * (line + spread);
}

void fragment() {
	vec2 uv = UV;
	vec2 half_size = max(flare_box.xy, vec2(1e-4));
	float radius = min(half_size.x, half_size.y);
	// The pixel in units of the box's shorter half, so round things stay round.
	vec2 q = uv * half_size / radius;
	float d = length(q);

	float thickness = flare_box.w;
	float falloff = flare_shape.x;
	float lines = streak(uv.x, q.y, thickness, falloff);
	if (flare_box.z > 1.5)
		lines += streak(uv.y, q.x, thickness, falloff);

	float core = exp(-pow(d / max(flare_light.x, 1e-3), 2.0) * 2.0);
	float halo = pow(clamp(1.0 - d / max(flare_light.y, 1e-3), 0.0, 1.0), 2.0) * flare_light.z;

	// Nothing reaches the box's edge, so the quad never shows.
	float edge = (1.0 - smoothstep(0.9, 1.0, abs(uv.x))) * (1.0 - smoothstep(0.9, 1.0, abs(uv.y)));
	float energy = (core + lines + halo) * flare_light.w * edge;

	// The core and the streaks' brightest parts run hot, towards white.
	float hot = clamp(core + lines * 0.35, 0.0, 1.0) * flare_shape.y;
	vec3 tint = mix(flare_tint.rgb, vec3(1.0), hot);

	// The vertex colour carries self modulate and group fades.
	COLOR = vec4(tint * COLOR.rgb, clamp(energy, 0.0, 1.0) * flare_tint.a * COLOR.a);
}
