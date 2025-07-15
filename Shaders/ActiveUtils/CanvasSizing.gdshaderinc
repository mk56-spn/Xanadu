varying flat vec2 canvas_half_size;
varying vec2 pixel_uv;
varying vec2 pixel_uv_raw;
varying vec2 scale;

#define SIZE\
	scale = vec2(length(MODEL_MATRIX[0]), length(MODEL_MATRIX[1]));\
	vec2 full_size = VERTEX.xy / (UV - 0.5);\
	canvas_half_size = full_size * scale / 2.0;\
	pixel_uv_raw = VERTEX.xy;\
	pixel_uv = VERTEX.xy * scale;

#define SIZE_VERTEX_EXCLUSIVE \
void vertex(){\
	SIZE\
}
