shader_type canvas_item;
render_mode blend_add;

#include "ActiveUtils/Perspective.gdshaderinc"
#include "NoteBase.gdshaderinc"

void vertex() {
	SIZE
	PERSPECTIVE
	VERTEX *= easeOutExpo(hitProgress());
}

void fragment() {
	PERSPECTIVE_FRAGMENT
	float dist = sdBox(uv * canvas_half_size ,vec2(canvas_half_size.x / 2.));
	dist = abs(dist) - 30.;


    
    // We mix the full vec4 this time, including the alpha channel.
    vec4 final_color = mix(vec4(1.), vec4(vec3(0.),1), pattern);	
	COLOR *= renderSDFWithOutline(dist,vec4(vec3(1.),1.),vec4(0.3),2,0.4);
	COLOR.a *= 1. - hitProgress();
	COLOR *= final_color;
}