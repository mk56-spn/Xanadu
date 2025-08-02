shader_type canvas_item;

#include "res://Shaders/Base.gdshaderinc"

const float OUTLINE_OFFSET = 2.;
const float OUTLINE_WIDTH = 1.;

vec4 draw_square()
{
	vec2 size = CANVAS_HALF_SIZE - OUTLINE_WIDTH - 3.;

    float main_square = sdBox(PIXEL_UV, size );
	const float rounding = 20.;
    float glow = outerGlowSDF(sdBox(PIXEL_UV,size -rounding)-rounding,20.,1.,2.);

	const float base_multiplier = 0.8;
	vec3 colour_final = getColour().rgb * base_multiplier + getColour().rgb * glow * (1. -base_multiplier) ;
    return renderSDF(main_square, vec4(colour_final.rgb,getColour().a), fwidth(PIXEL_UV.x));
}
vec4 draw_outline(){
	float main_square = sdBox(PIXEL_UV, CANVAS_HALF_SIZE -OUTLINE_WIDTH - OUTLINE_OFFSET) - OUTLINE_OFFSET;

	float outline_half_width = OUTLINE_WIDTH / 2.;
	main_square = opOnion(main_square - outline_half_width, outline_half_width);
	vec3 colour_final = getColour().rgb - abs(PIXEL_UV.y / CANVAS_HALF_SIZE.y * 0.5);

	return renderSDF(main_square,vec4(colour_final, getColour().a),fwidth(PIXEL_UV.x));
}

void fragment(){
	COLOR = blend_over(draw_square(),draw_outline());
}