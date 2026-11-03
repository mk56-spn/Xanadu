// The camera every snow shader shares: a pinhole looking down cam_fwd, set
// once a frame from C#. No Godot 3D: world points become canvas pixels here.
uniform vec3 cam_pos;
uniform vec3 cam_right;
uniform vec3 cam_up;
uniform vec3 cam_fwd;
uniform vec2 screen_center;   // low-res viewport pixels
uniform float focal;          // pixels per unit at depth 1
uniform vec3 sun_dir;

const vec3 FOG = vec3(0.70, 0.74, 0.84);   // also the sky at the horizon

vec3 to_view(vec3 world) {
	vec3 d = world - cam_pos;
	return vec3(dot(d, cam_right), dot(d, cam_up), dot(d, cam_fwd));
}

vec2 to_screen(vec3 view) {
	return screen_center + vec2(view.x, -view.y) * focal / max(view.z, 0.01);
}

float fog_at(float depth) {
	return 1.0 - exp(-depth * 0.014);
}

// 4x4 ordered dither into a few levels per channel: the low-res banding look.
const float BAYER[16] = { 0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0 };

vec3 posterize(vec3 c, vec2 pixel) {
	ivec2 p = ivec2(mod(floor(pixel), 4.0));
	float t = (BAYER[p.x + p.y * 4] + 0.5) / 16.0;
	const float LEVELS = 18.0;
	return floor(c * LEVELS + t) / LEVELS;
}
