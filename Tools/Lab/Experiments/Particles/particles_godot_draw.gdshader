// GPUParticles2D draws a 1px quad per particle; this folds its two right-hand
// corners onto one tip, so each quad becomes the same unit triangle the
// DRAWABLE tab draws (the second half of the quad has no area). The particle
// shader's TRANSFORM then places, turns and scales it.
shader_type canvas_item;
render_mode blend_add;

void vertex() {
	VERTEX = UV.x > 0.5 ? vec2(1.0, 0.0) : vec2(-0.5, UV.y > 0.5 ? 0.8660254 : -0.8660254);
}
