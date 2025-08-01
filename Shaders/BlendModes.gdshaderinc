

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

// blend_modes.gdshaderinc
// One include, every blend mode + alpha-composite.
// Usage:  #include "res://blend_modes.gdshaderinc"
//         COLOR = BLEND_OVER(base, paint, BLEND_MULTIPLY);

/* ---------- 1. Alpha compositing ---------- */
vec4 _alpha_over(vec4 dst, vec4 src) {
    float a = src.a + dst.a * (1.0 - src.a);
    vec3  c = (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / max(a, 1e-5);
    return vec4(c, a);
}

/* ---------- 2. RGB blend cores ---------- */
vec3 _blend_normal   (vec3 b, vec3 s) { return s; }
vec3 _blend_multiply (vec3 b, vec3 s) { return b * s; }
vec3 _blend_screen   (vec3 b, vec3 s) { return 1.0 - (1.0 - b) * (1.0 - s); }
vec3 _blend_darken   (vec3 b, vec3 s) { return min(b, s); }
vec3 _blend_lighten  (vec3 b, vec3 s) { return max(b, s); }
vec3 _blend_overlay  (vec3 b, vec3 s) { return mix(2.0 * b * s, 1.0 - 2.0 * (1.0 - b) * (1.0 - s), step(0.5, b)); }
vec3 _blend_hardlight(vec3 b, vec3 s) { return mix(2.0 * b * s, 1.0 - 2.0 * (1.0 - b) * (1.0 - s), step(0.5, s)); }
vec3 _blend_softlight(vec3 b, vec3 s) { return mix(2.0 * b * s + b * b * (1.0 - 2.0 * s), sqrt(b) * (2.0 * s - 1.0) + 2.0 * b * (1.0 - s), step(0.5, s)); }
vec3 _blend_difference(vec3 b, vec3 s) { return abs(b - s); }
vec3 _blend_exclusion (vec3 b, vec3 s) { return b + s - 2.0 * b * s; }


/* ---------- 3. Macro glue ---------- */
#define BLEND_OVER(base, over, MODE) _alpha_over(base, vec4(MODE((base).rgb, (over).rgb), (over).a))

/* ---------- 4. Named constants for readability ---------- */
#define BLEND_NORMAL    _blend_normal
#define BLEND_MULTIPLY  _blend_multiply
#define BLEND_SCREEN    _blend_screen
#define BLEND_DARKEN    _blend_darken
#define BLEND_LIGHTEN   _blend_lighten
#define BLEND_OVERLAY   _blend_overlay
#define BLEND_HARDLIGHT _blend_hardlight
#define BLEND_SOFTLIGHT _blend_softlight
#define BLEND_DIFFERENCE _blend_difference
#define BLEND_EXCLUSION  _blend_exclusion
#define BLEND_DODGE      _blend_color_dodge
#define BLEND_BURN       _blend_color_burn