vec4 blend_over(vec4 top_layer, vec4 bottom_layer) {
	float out_alpha = top_layer.a + bottom_layer.a * (1.0 - top_layer.a);
	if (out_alpha == 0.0) { return vec4(0.0); }
	vec3 out_rgb = (top_layer.rgb * top_layer.a + bottom_layer.rgb * bottom_layer.a * (1.0 - top_layer.a)) / out_alpha;
	return vec4(out_rgb, out_alpha);
}


#define GENERATE_BLEND_COLORS_FUNC(N) \
vec4 blend_colors(const vec4 colors[N]) { \
    vec4 current = vec4(0.0); \
    for (int i = 0; i < N; ++i) { \
        current = blend_over(colors[i], current); \
    } \
    return current; \
}

GENERATE_BLEND_COLORS_FUNC(2)
GENERATE_BLEND_COLORS_FUNC(3)
GENERATE_BLEND_COLORS_FUNC(4)
GENERATE_BLEND_COLORS_FUNC(5)
GENERATE_BLEND_COLORS_FUNC(6)
GENERATE_BLEND_COLORS_FUNC(7)