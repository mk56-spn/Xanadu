// Triangles drawn as geometry: three vertices each, uploaded once. The vertex
// shader turns every triangle toward the mouse; the fragment shader only fills.
// Cost scales with vertices plus the pixels the triangles actually cover.
shader_type canvas_item;

global uniform vec2 lab_mouse;   // canvas pixels, set once a frame by the host

const vec3 NEAR = vec3(0.98, 0.80, 0.08);
const vec3 FAR = vec3(0.22, 0.74, 0.97);

void vertex() {
	// UV carries the triangle's centre; VERTEX is that centre plus a corner
	// offset laid out pointing along +x.
	vec2 center = UV;
	vec2 to_mouse = lab_mouse - center;
	float angle = atan(to_mouse.y, to_mouse.x);
	float c = cos(angle), s = sin(angle);
	VERTEX = center + mat2(vec2(c, s), vec2(-s, c)) * (VERTEX - center);
	COLOR = vec4(mix(NEAR, FAR, clamp(length(to_mouse) / 600.0, 0.0, 1.0)), 1.0);
}
