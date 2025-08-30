shader_type canvas_item;
instance uniform float rotation_speed = 1.0;

void vertex() {
    float angle = TIME * rotation_speed;
    mat2 rotation_matrix = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
    VERTEX = (rotation_matrix * (VERTEX - vec2(0.5, 0.5))) + vec2(0.5, 0.5);
}

