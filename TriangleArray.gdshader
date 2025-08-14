shader_type canvas_item;

#include "Shaders/ActiveUtils/CanvasSizing.gdshaderinc"
#include "Shaders/PureUtils/Patterns.gdshaderinc"
#include "Shaders/PureUtils/Colours.gdshaderinc"
#include "Shaders/PureUtils/bezierHelpers.gdshaderinc"   // <-- NEW include

// --- Procedural Generation Controls ---
uniform int   segments_in_chain          : hint_range(1, 400000) = 4;
uniform int   subdivisions_per_segment   : hint_range(20, 200)   = 50;
uniform float seed                       : hint_range(0.0, 100.0) = 1.0;
uniform vec2  bounds                     = vec2(800.0, 500.0);
uniform float handle_length              : hint_range(0.0, 300.0) = 200.0;

// --- Animation Controls ---
uniform float animation_speed : hint_range(0.0, 2.0) = 0.2;
uniform float animation_radius : hint_range(0.0, 200.0) = 30.0;

// --- Calligraphy Controls ---
uniform float max_thickness : hint_range(1.0, 80.0) = 18.0;

// --- Glow & Colour ---
uniform vec4 line_color : source_color = vec4(1.0);
uniform vec4 glow_color : source_color = vec4(0.8, 0.7, 1.0, 1.0);
uniform float glow_width : hint_range(0.0, 1.0) = 0.75;

// --- Path Shapes ---
uniform float path_frequency : hint_range(0.1, 10.0) = 1.3;
uniform float wander_strength  : hint_range(0.0, 1000.0) = 400.0;

// --- Varyings to fragment shader ---
varying float v_global_t;
varying float v_cross_t;

// ------------------------------------------------------------------
//  VERTEX
// ------------------------------------------------------------------
void vertex() {
	// Map vertex ID to subdivision quad
	int global_sub_idx = VERTEX_ID / 6;
	int vertex_in_quad = VERTEX_ID % 6;

	int seg_idx = global_sub_idx / subdivisions_per_segment;
	int sub_idx = global_sub_idx % subdivisions_per_segment;

	float t_start = float(sub_idx)     / float(subdivisions_per_segment);
	float t_end   = float(sub_idx + 1) / float(subdivisions_per_segment);

	float local_t;
	float side;   // -1 or 1 for thickness offset

	// 6-vertex quad layout:
	// 0:BL  1:TL  2:BR  |  3:TL  4:TR  5:BR
	if (vertex_in_quad == 0) { local_t = t_start; side = -1.0; }
	else if (vertex_in_quad == 1) { local_t = t_start; side =  1.0; }
	else if (vertex_in_quad == 2) { local_t = t_end;   side = -1.0; }
	else if (vertex_in_quad == 3) { local_t = t_start; side =  1.0; }
	else if (vertex_in_quad == 4) { local_t = t_end;   side =  1.0; }
	else /* 5 */                { local_t = t_end;   side = -1.0; }

	float global_t = (float(seg_idx) + local_t) / float(segments_in_chain);
	v_global_t = global_t;
	v_cross_t  = side;

	// Clamp final segment so we never read past the last node
	int idx_now  = min(seg_idx,     segments_in_chain - 1);
	int idx_next = min(seg_idx + 1, segments_in_chain - 1);

	vec2 p0, h0, p3, h3;

	curve_node(idx_now,  p0, h0, bounds,
	           path_frequency, wander_strength,
	           handle_length, animation_speed, animation_radius, TIME);

	curve_node(idx_next, p3, h3,  bounds,
	           path_frequency, wander_strength,
	           handle_length, animation_speed, animation_radius, TIME);

	vec2 p1 = p0 + h0;
	vec2 p2 = p3 - h3;

	vec2 pos     = cubic_bezier(p0, p1, p2, p3, local_t);
	vec2 tangent = cubic_bezier_derivative(p0, p1, p2, p3, local_t);
	if (length(tangent) < 0.0001) tangent = p3 - p0;
	vec2 normal  = normalize(vec2(-tangent.y, tangent.x));

	VERTEX = pos + normal * side * max_thickness * 0.5;
}
