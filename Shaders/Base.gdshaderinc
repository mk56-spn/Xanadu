global uniform sampler2D colours_texture : filter_nearest;

#include "PureUtils/2DSDF.gdshaderinc"
#include "PureUtils/Easings.gdshaderinc"
#include "ActiveUtils/CanvasSizing.gdshaderinc"

instance uniform int index = -1;

vec4 getColour(){
	if (index == -1){
		return vec4(1.);
	}
	 float u = (float(index) + 0.5) / 100.;
	return texture(colours_texture, vec2(u,0.5));
}