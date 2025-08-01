global uniform sampler2D colours_texture : filter_nearest;


#include "BlendModes.gdshaderinc"
#include "ActiveUtils/CanvasSizing.gdshaderinc"

#include "PureUtils/SDFConstructs.gdshaderinc"
#include "PureUtils/Colours.gdshaderinc"
#include "PureUtils/Patterns.gdshaderinc"
#include "PureUtils/UVUtils.gdshaderinc"
#include "PureUtils/2DSDF.gdshaderinc"
#include "PureUtils/Easings.gdshaderinc"

instance uniform int index = -1;

vec4 getColour(){
	if (index == -1){
		return vec4(1.);
	}

	 float u = (float(index) + 0.5) / 100.;
	return texture(colours_texture, vec2(u,0.5));
}

#ifndef OVERRIDE_DEFAULT_VERTEX
void vertex(){
	SIZE
	COLOR *= getColour();
}
#endif
