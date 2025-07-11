
float sdCircle( vec2 p, float r )
{
    return length(p) - r;
}


float sdRing( in vec2 p, in vec2 n, in float r, in float th )
{
    p.x = abs(p.x);

    p = mat2(vec2(n.x,n.y),vec2(-n.y,n.x))*p;

    return max( abs(length(p)-r)-th*0.5,
                length(vec2(p.x,max(0.0,abs(r-p.y)-th*0.5)))*sign(p.x) );
}

float sdBox( in vec2 p, in vec2 b )
{
    vec2 d = abs(p)-b;
    return length(max(d,0.0)) + min(max(d.x,d.y),0.0);
}

float sdRoundedBox( in vec2 p, in vec2 b, in vec4 r )
{
    r.xy = (p.x>0.0)?r.xy : r.zw;
    r.x  = (p.y>0.0)?r.x  : r.y;
    vec2 q = abs(p)-b+r.x;
    return min(max(q.x,q.y),0.0) + length(max(q,0.0)) - r.x;
}
float sdChamferBox(vec2 p, vec2 b, float chamfer)
{
    p = abs(p) - b;

    p = (p.y>p.x) ? p.yx : p.xy;
    p.y += chamfer;

    const float k = 1.0-sqrt(2.0);
    if( p.y<0.0 && p.y+p.x*k<0.0 ) return p.x;
    if( p.x<p.y ) return (p.x+p.y)*sqrt(0.5);
    return length(p);
}

float opOnion(float shape, in vec2 p, in float r )
{
  return abs(shape) - r;
}
float sdEquilateralTriangle( in vec2 p, in float r )
{
    const float k = sqrt(3.0);
    p.x = abs(p.x) - r;
    p.y = p.y + r/k;
    if( p.x+k*p.y>0.0 ) p = vec2(p.x-k*p.y,-k*p.x-p.y)/2.0;
    p.x -= clamp( p.x, -2.0*r, 0.0 );
    return -length(p)*sign(p.y);
}

float sdParallelogram( in vec2 p, float wi, float he, float sk )
{
    vec2 e = vec2(sk,he);
    p = (p.y<0.0)?-p:p;
    vec2  w = p - e; w.x -= clamp(w.x,-wi,wi);
    vec2  d = vec2(dot(w,w), -w.y);
    float s = p.x*e.y - p.y*e.x;
    p = (s<0.0)?-p:p;
    vec2  v = p - vec2(wi,0); v -= e*clamp(dot(v,e)/dot(e,e),-1.0,1.0);
    d = min( d, vec2(dot(v,v), wi*he-abs(s)));
    return sqrt(d.x)*sign(-d.y);
}

float sdHexagram( in vec2 p, in float r )
{
    const vec4 k = vec4(-0.5,0.8660254038,0.5773502692,1.7320508076);
    p = abs(p);
    p -= 2.0*min(dot(k.xy,p),0.0)*k.xy;
    p -= 2.0*min(dot(k.yx,p),0.0)*k.yx;
    p -= vec2(clamp(p.x,r*k.z,r*k.w),r);
    return length(p)*sign(p.y);
}

vec2 rotate(vec2 p, float angle) {
    float cosA = cos(angle);
    float sinA = sin(angle);
    return mat2(vec2(cosA, -sinA), vec2(sinA, cosA)) * p; // Rotate `p` by `angle`
}


float sdMoon(vec2 p, float d, float ra, float rb )
{
    p.y = abs(p.y);

    float a = (ra*ra - rb*rb + d*d)/(2.0*d);
    float b = sqrt(max(ra*ra-a*a,0.0));
    if( d*(p.x*b-p.y*a) > d*d*max(b-p.y,0.0) )
    {
        return length(p-vec2(a,b));
    }

    return max( (length(p          )-ra),
               -(length(p-vec2(d,0))-rb));
}


float selectShape(int index, vec2 uv, float rotation, vec2 size, vec2 extra){
	uv = rotate(uv,rotation);
	float shape_temp;

	switch(index){
		case 0: shape_temp = sdCircle(uv, size.x);
		break;
		case 1: shape_temp = sdBox(uv, size);
		break;
		case 2: shape_temp = sdParallelogram(uv,size.x,size.y, 3.);
		break;
		case 3: shape_temp = sdRoundedBox(uv, size, vec4(20.));
		break;
		case 4: shape_temp = sdChamferBox(uv, size, size.y /4.);
		break;
		case 5: shape_temp = sdEquilateralTriangle(uv, size.x);
		break;
		case 6: shape_temp = sdHexagram(uv, size.x / 2.);
		break;
		case 7: shape_temp = sdRing(uv,vec2(cos(extra.x), sin(extra.x)) , size.x, size.y);
		break;
	}
	return shape_temp;
}

vec4 renderGlowFromSDF(float dist, float outline_width, float width, vec4 color,float intensity ) {

	// Define the zone where the glow exists. It starts at the outline and fades out.
	float glow_start = outline_width;
	float glow_end = glow_start + width;

	// Create a smooth falloff from 1.0 to 0.0 across the glow zone.
	// `1.0 - smoothstep(...)` creates a fade-out effect.
	float glow_alpha = 1.0 - smoothstep(glow_start, glow_end, dist);

	// Apply the color's alpha and the master intensity control.
	float final_alpha = glow_alpha * color.a * intensity;

	return vec4(color.rgb, final_alpha);
}


vec4 renderSDFWithOutline(
    float dist,
    vec4 fillColor,
    vec4 outlineColor,
    float outlineWidth,
    float smoothness
) {
    float shapeAlpha = 1.0 - smoothstep(outlineWidth - smoothness, outlineWidth + smoothness, dist);
    float outlineMix = smoothstep(-smoothness, smoothness, dist);
    vec4 blendedColor = mix(fillColor, outlineColor, outlineMix);
    blendedColor.a *= shapeAlpha;

    return blendedColor;
}
