shader_type canvas_item;
render_mode blend_add;

#include "Base.gdshaderinc"

const float size  = 30.;
void fragment() {
	vec2 final_uv = PIXEL_UV;
	RepeatUV repeat = tile_uv_with_ID(final_uv, size);
	vec2 uv = rotate(repeat.repeated_uv, TIME + repeat.id.x);
	vec4 sdf1 = renderSDF( sdCross(uv,vec2(size/4.,1.),0.),GREEN - distance(vec2(0.),sin(repeat.id/ 3.) / 2.),1.);
	vec4 sdf2 = renderSDF(sdCircle(repeat.repeated_uv,sin(repeat.id.x + TIME + 10.) * size / 4.), DARK_ORANGE - distance(vec2(0.),cos(repeat.id/ 3.) / 2.),1.);
	COLOR *= mix(sdf1, sdf2,checkerboard(final_uv  + size  / 2.,size));
}
