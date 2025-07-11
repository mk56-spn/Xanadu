
shader_type canvas_item;
#include "Base.gdshaderinc"

void vertex() {
	COLOR = getColour();
}
// Tells Godot this is a 2D shader for CanvasItems (like Sprites).


// A uniform to control the rotation speed from the Godot Inspector.
uniform float rotation_speed : hint_range(-10.0, 10.0) = 1.0;
