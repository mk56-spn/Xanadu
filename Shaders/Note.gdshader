shader_type canvas_item;

	#include "Base.gdshaderinc"
	#include "BlendModes.gdshaderinc"
	#include "NoteBase.gdshaderinc"

	// --- Global Styling ---
	uniform sampler2D texture2;
	uniform float outline_width : hint_range(0, 5, 1) = 1.0;
	uniform float transition_smoothness : hint_range(.5, 3., 0.1) = 1.0;
	uniform float glow_width : hint_range(0.0, 15.0, 1.0) = 5.0;
	uniform float glow_intensity : hint_range(0.0, 1.0, 0.01) = .5;


	uniform float radius = 3.;

	uniform vec2 size2 = vec2(20.0, 20.0);

	void fragment()
	{
		vec4 tex1 = texture(TEXTURE, UV);
		vec4 tex2 = texture(texture2, UV);

		// inner circle
		float dist1 =  sdCircle(PIXEL_UV, radius);
		vec4 shape_layer1 = renderSDFWithOutline(dist1,YELLOW, RED - 0.4,3.,outline_width);
		vec4 glow_layer1 = renderSDF(dist1,LAWN_GREEN,0.5);

		vec4 final_color;

		final_color = blend_over(shape_layer1, glow_layer1);

		shape_layer1 *= 0.5 + 0.5 * getPulse();


		COLOR = final_color;
	}