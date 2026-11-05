// A layer pass over a viewport texture: splits red and blue sideways. The
// viewport's pixels come out premultiplied, so it composites as premultiplied.
shader_type canvas_item;
render_mode blend_premul_alpha;

void fragment() {
	vec2 shift = vec2(0.012, 0.0);
	vec4 mid = texture(TEXTURE, UV);
	float r = texture(TEXTURE, UV - shift).r;
	float b = texture(TEXTURE, UV + shift).b;
	COLOR = vec4(r, mid.g, b, mid.a);
}
