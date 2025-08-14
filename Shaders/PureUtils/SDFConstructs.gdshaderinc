#include "2DSDF.gdshaderinc"

float sdfAlpha(float dist, float smoothness) {
  return 1.0 - smoothstep(0.0, smoothness, dist);
}

vec4 renderSDF(float dist, vec4 fillColor, float smoothness) {
  // Get the antialiasing alpha from our common helper function.
  float alpha = sdfAlpha(dist, smoothness);
  fillColor.a *= alpha;

  return fillColor;
}

float outerGlowSDF(float d, float radius, float edge, float power)
{
    float t = 1.0 - clamp(-d / (radius * edge), 0.0, 1.0); // 1 at rim, 0 at core
    return pow(t, power);
}

vec4 renderSDFWithOutline(
float dist,
vec4 fillColor,
vec4 outlineColor,
float outlineWidth,
float smoothness
) {
// Inside the shape: dist <= 0
// Outline is drawn *inside* the shape, so we subtract outlineWidth from the distance
float innerEdge = -outlineWidth;

// Alpha for the shape: full inside, antialiased at edge
float shapeAlpha = 1.0 - smoothstep(0.0, smoothness, dist);

// Mix factor for outline: 1.0 inside the outline region, 0.0 outside
float outlineMix = smoothstep(innerEdge - smoothness, innerEdge + smoothness, dist);

vec4 color = mix(outlineColor, fillColor, outlineMix);
color.a *= shapeAlpha;
return color;
}

// Unique IDs for every shape
#define SHAPE_CIRCLE          0
#define SHAPE_CROSS           1
#define SHAPE_RING            2
#define SHAPE_BOX             3
#define SHAPE_ROUNDED_BOX     4
#define SHAPE_CHAMFER_BOX     5
#define SHAPE_EQUIL_TRIANGLE  6
#define SHAPE_PARALLELOGRAM   7
#define SHAPE_HEXAGRAM        8
#define SHAPE_MOON            9

/*  Generic parameter block
 *  x,y,z,w are re-interpreted by the requested shape.
 *  The comment on each case below gives the mapping.
 */
float sdShape( int id, vec2 p, vec4 prm )
{
    switch (id)
    {
        /* prm.x  : radius                                            */
        case SHAPE_CIRCLE:
            return sdCircle( p, prm.x );

        /* prm.xy : b (half-size), prm.z : rounding radius            */
        case SHAPE_CROSS:
            return sdCross( p, prm.xy, prm.z );

        /* prm.xy : n  (normal), prm.z : r (mean radius), prm.w : thickness */
        case SHAPE_RING:
            return sdRing( p, prm.xy, prm.z, prm.w );

        /* prm.xy : half-size                                         */
        case SHAPE_BOX:
            return sdBox( p, prm.xy );

        /* prm.xy : half-size, prm.zw : radii (zw because vec4)       */
        case SHAPE_ROUNDED_BOX:
            return sdRoundedBox( p, prm.xy, prm );

        /* prm.xy : half-size, prm.z : chamfer                        */
        case SHAPE_CHAMFER_BOX:
            return sdChamferBox( p, prm.xy, prm.z );

        /* prm.x  : radius                                            */
        case SHAPE_EQUIL_TRIANGLE:
            return sdEquilateralTriangle( p, prm.x );

        /* prm.x : width, prm.y : height, prm.z : skew                */
        case SHAPE_PARALLELOGRAM:
            return sdParallelogram( p, prm.x, prm.y, prm.z );

        /* prm.x  : radius                                            */
        case SHAPE_HEXAGRAM:
            return sdHexagram( p, prm.x );

        /* prm.x : d (distance between centres),
           prm.y : ra (outer radius), prm.z : rb (inner radius)       */
        case SHAPE_MOON:
            return sdMoon( p, prm.x, prm.y, prm.z );
    }
    return 1e38;   // unreachable
}
