// The scene: one triangle array, three unshared vertices per triangle, built
// once on the CPU with lighting baked into COLOR. VERTEX holds world x,z and
// UV.x holds world y; this shader projects them. There is no depth buffer, so
// C# hands over the indices sorted far to near every frame (painter's order).
shader_type canvas_item;

#include "res://Experiments/Snow/snow_view.gdshaderinc"

void vertex() {
	vec3 view = to_view(vec3(VERTEX.x, UV.x, VERTEX.y));
	VERTEX = to_screen(view);
	COLOR.rgb = mix(COLOR.rgb, FOG, fog_at(view.z));
}

void fragment() {
	COLOR.rgb = posterize(COLOR.rgb, FRAGCOORD.xy);
}
