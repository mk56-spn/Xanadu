// The fire rule, shared by the step and the sniper fire list so a shot is
// decided the same way everywhere. Include after rts_rules.gdshaderinc, with
// the nav_field sampler and the bounds and dt uniforms declared.

bool rock_at(vec2 p) {
	return texelFetch(nav_field, nav_cell(p, bounds), 0).y > 0.5;
}

bool clear_line(vec2 a, vec2 b) {
	int steps = int(distance(a, b) / LOS_STEP);
	for (int i = 1; i < steps; i++) {
		if (rock_at(mix(a, b, float(i) / float(steps))))
			return false;
	}
	return true;
}

// In range, and either in line of sight or lobbed (artillery, past its minimum).
bool can_hit(int type, vec2 from, vec2 to) {
	float d = distance(from, to);
	if (d > RANGE[type])
		return false;
	if (type == ARTILLERY)
		return d >= ARTILLERY_MIN;
	return clear_line(from, to);
}

// Last frame's fire rule, used by the shooter and by its target alike.
bool fires_at(vec4 shooter_unit, int shooter, vec2 shooter_pos, int target, vec2 target_pos) {
	return int(shooter_unit.z) == target
		&& shooter_unit.y - dt <= 0.0
		&& can_hit(type_of(shooter), shooter_pos, target_pos);
}
