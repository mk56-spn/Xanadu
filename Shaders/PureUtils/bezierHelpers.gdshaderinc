// ----------------------------------------------------------
//  BEZIER CURVE HELPERS
// ----------------------------------------------------------
vec2 cubic_bezier(vec2 p0, vec2 p1, vec2 p2, vec2 p3, float t) {
	float t_inv = 1.0 - t;
	return pow(t_inv, 3.0) * p0
	     + 3.0 * pow(t_inv, 2.0) * t * p1
	     + 3.0 * t_inv * pow(t, 2.0) * p2
	     + pow(t, 3.0) * p3;
}

vec2 cubic_bezier_derivative(vec2 p0, vec2 p1, vec2 p2, vec2 p3, float t) {
	float t_inv = 1.0 - t;
	return 3.0 * t_inv * t_inv * (p1 - p0)
	     + 6.0 * t_inv * t * (p2 - p1)
	     + 3.0 * t * t * (p3 - p2);
}

// ----------------------------------------------------------
//  LOW-FREQUENCY PATH + HIGH-FREQUENCY WANDER
// ----------------------------------------------------------
float rand(vec2 co) {
	return fract(sin(dot(co.xy, vec2(12.9898, 78.233))) * 43758.5453);
}

// Computes the anchor & handle for a given node index.
//   node_index : index of the curve node (0 … N)
//   anchor     : OUT – absolute position of the node
//   handle     : OUT – offset the node uses for its Bézier handle
void curve_node(int node_index,
                out vec2 anchor,
                out vec2 handle,
                vec2  bounds,
                float path_frequency,
                float wander_strength,
                float handle_length,
                float animation_speed,
                float animation_radius,
                float time) {
	float i = float(node_index);

	// 1) Smooth low-frequency path (Lissajous)
	float t = i / 1000.0 * path_frequency;

	// 2) High-frequency wander
	vec2 wander_dir = vec2(0.);

	// 3) Per-node animation
	float angle = (time + i * 0.5) * animation_speed;
	anchor = float(node_index)	 * vec2(0.1)	;
	// 4) Handle direction
	float handle_angle =  6.28318; // 2π
	handle = vec2(cos(handle_angle), sin(handle_angle)) * handle_length;
}
