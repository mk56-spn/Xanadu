// A highly efficient shader to hue-shift a single solid color.
// The logic is in the vertex shader, making it extremely cheap.
shader_type canvas_item;

// The amount to shift the hue.
instance uniform float hue_shift : hint_range(0.0, 1.0) = 0.0;
instance uniform float brightness : hint_range(-1, 1.0, 0.1);

// The RGB <-> HSV conversion functions still need to be defined.
// The compiler will make them available to both vertex and fragment stages.
vec3 rgb_to_hsv(vec3 c) {
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv_to_rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

void vertex() {
    // Convert the single input color to HSV.
    vec3 hsv = rgb_to_hsv(COLOR.rgb);

    // Apply the hue shift.
    hsv.x += hue_shift;
    hsv.x = fract(hsv.x);

    // Convert back to RGB.
    vec3 final_rgb = hsv_to_rgb(hsv);

    // Overwrite the COLOR variable. Godot will automatically interpolate this
    // color across the polygon's surface for the fragment shader.
    COLOR = vec4(final_rgb, COLOR.a);
}

void fragment() {
    // The COLOR variable here is the result interpolated from the vertex shader.
    // If the node has a texture, we must multiply our new color by it.
    // If not, we can just use the color directly.
    vec4 texture_color = texture(TEXTURE, UV);
	
	COLOR.rgb += -brightness;
    COLOR *= texture_color;
}