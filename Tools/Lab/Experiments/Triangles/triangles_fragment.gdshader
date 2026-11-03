// Triangles drawn by the fragment shader: one quad over the whole field, and
// every pixel works out which cell it is in, turns toward the mouse and tests
// the triangle. Cost scales with pixels, not with how many triangles there are.
shader_type canvas_item;

global uniform vec2 lab_mouse;   // canvas pixels, set once a frame by the host
uniform float cell = 32.0;       // grid pitch, canvas pixels

const float SIZE = 0.4;          // triangle radius as a share of the cell
const vec3 NEAR = vec3(0.98, 0.80, 0.08);
const vec3 FAR = vec3(0.22, 0.74, 0.97);

varying vec2 canvas_pos;

void vertex() {
	// The stage sits at the canvas origin, so local is canvas space.
	canvas_pos = VERTEX;
}

void fragment() {
	vec2 center = (floor(canvas_pos / cell) + 0.5) * cell;
	vec2 to_mouse = lab_mouse - center;
	float angle = atan(to_mouse.y, to_mouse.x);
	float c = cos(angle), s = sin(angle);
	// Undo the turn, so the triangle can be tested pointing along +x.
	vec2 q = mat2(vec2(c, -s), vec2(s, c)) * (canvas_pos - center);

	// Equilateral triangle, tip at +x: the furthest of its three edge planes.
	float r = SIZE * cell;
	float sd = max(-q.x, max(dot(q, vec2(0.5, 0.8660254)), dot(q, vec2(0.5, -0.8660254)))) - r * 0.5;
	float alpha = clamp(0.5 - sd / fwidth(sd), 0.0, 1.0);

	vec3 tint = mix(NEAR, FAR, clamp(length(to_mouse) / 600.0, 0.0, 1.0));
	COLOR = vec4(tint, alpha);
}
