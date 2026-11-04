// The scene: one triangle array, three vertices per triangle, uploaded once and
// never redrawn by the CPU. UV is (draw position, corner). Canvas triangles
// paint in array order, so draw position is painter's order: each vertex
// binary-searches the sorted chunk list for the chunk covering its position,
// takes that chunk's triangle and projects its corner. Back faces and
// triangles crossing the near plane collapse to a point.
shader_type canvas_item;

uniform sampler2D geometry : filter_nearest;
uniform sampler2D order : filter_nearest;
uniform int chunks;

#include "res://Experiments/Snow/snow_view.gdshaderinc"
#include "res://Experiments/Snow/snow_slots.gdshaderinc"

void vertex() {
	float position = UV.x;
	int corner = int(UV.y + 0.5);

	// The last slot whose start is at or before this position.
	int lo = 0;
	int hi = chunks - 1;
	for (int step = 0; step < 16 && lo < hi; step++) {
		int mid = (lo + hi + 1) / 2;
		if (texelFetch(order, slot_texel(mid), 0).y <= position)
			lo = mid;
		else
			hi = mid - 1;
	}
	vec4 entry = texelFetch(order, slot_texel(lo), 0);
	int id = int(entry.z + position - entry.y + 0.5);

	vec3 a = triangle_texel(geometry, id, 0).xyz;
	vec3 b = triangle_texel(geometry, id, 1).xyz;
	vec3 c = triangle_texel(geometry, id, 2).xyz;
	bool front = dot(cross(b - a, c - a), cam_pos - a) > 0.0;
	float near = min(dot(a - cam_pos, cam_fwd), min(dot(b - cam_pos, cam_fwd), dot(c - cam_pos, cam_fwd)));
	if (!front || near < NEAR) {
		VERTEX = vec2(-1e4);   // all three corners meet: nothing to fill
		COLOR = vec4(0.0);
	} else {
		vec3 view = to_view(corner == 0 ? a : corner == 1 ? b : c);
		VERTEX = to_screen(view);
		COLOR = vec4(mix(triangle_texel(geometry, id, 3).rgb, FOG, fog_at(view.z)), 1.0);
	}
}

void fragment() {
	COLOR.rgb = posterize(COLOR.rgb, FRAGCOORD.xy);
}
