
global uniform sampler2D colours_texture : filter_nearest;

instance uniform int index = -1;
instance uniform int index_2 = -1;


vec4 getColour(){

	if (index == -1){
		return vec4(1.);
	}

	 float u = (float(index) + 0.5) / 100.;
	return texture(colours_texture, vec2(u,0.5));
}

vec4 getSecondaryColour(){
	if (index_2 == -1){
		return vec4(0.,0.,0.,1.);
	}

	 float u = (float(index_2) + 0.5) / 100.;
	return texture(colours_texture, vec2(u,0.5));
}
