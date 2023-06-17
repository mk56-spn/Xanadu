class_name EnvironmentPolygon
extends Polygon2D

var collision_body := StaticBody2D.new();
var collision_hitbox := CollisionPolygon2D.new();

func _init():
	add_child(collision_body);
	
	collision_body.add_child(collision_hitbox);
	collision_body.set_collision_layer(4);
	
	collision_hitbox.one_way_collision = true;
	
func _draw():
	collision_hitbox.set_polygon(self.polygon);
