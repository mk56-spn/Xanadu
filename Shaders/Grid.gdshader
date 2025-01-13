shader_type canvas_item;

const float grid_count = 4.;

void fragment() {
	vec2 tiledUV = fract ( UV * grid_count);
	vec2 square = abs(tiledUV * 2. - 1.);
	vec2 sharpened = smoothstep(0.9, 0.95 ,square);
	float main_opacity = step(0.1 , sharpened.x + sharpened.y);
	float main_opacity_stepped = step(0.9,main_opacity);
	float radial_opacity = 1. - distance(UV, vec2(0.5)) * 2. ;

	COLOR = vec4(vec3(1.0),radial_opacity * main_opacity_stepped);
}