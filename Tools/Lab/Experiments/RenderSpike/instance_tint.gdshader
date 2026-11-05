// One material, a colour per canvas item.
shader_type canvas_item;

instance uniform vec4 tint : source_color = vec4(1.0, 0.0, 1.0, 1.0);

void fragment() {
	COLOR = tint;
}
