shader_type canvas_item;

#define DistanceMultiplier 2.

uniform vec4 color : source_color;

void fragment()
{
	int iterations = 50;
    vec2 uv = SCREEN_UV;
	uv.x *= SCREEN_PIXEL_SIZE.y /SCREEN_PIXEL_SIZE.x ;
    float scaleDiv = 1.;
    float level = 0.;

    vec2 quadCenter = (floor(uv * scaleDiv) + 0.5) / scaleDiv;
    vec4 quadColor;

    vec2 subdivisionPoint = vec2(.5, .25) + .25 * vec2(cos(TIME), sin(TIME));

    for(int i = 0; i < iterations; i++)
    {
        if(distance(subdivisionPoint, quadCenter) > 1.0 / (scaleDiv) * DistanceMultiplier) break;

        scaleDiv *= 2.0;
        level += 0.05 + 1. / float(iterations);
        quadCenter = (floor(uv * scaleDiv) + 0.5) / scaleDiv;
    }


    COLOR = color * vec4(level);
}