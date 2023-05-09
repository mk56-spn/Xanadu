extends Perception;

const JUMP_VELOCITY = -1900.0

#Air movement does not require a tween for now
var rotation_tween : Tween;

func _physics_process(delta: float) -> void:
	
	_nucleus_collision()
	
	if self.is_on_floor(): 
		_ground_movement();
	else: 
		_air_movement(delta);
		
	move_and_slide()
	
func _air_movement(d : float) -> void:	
	
	if rotation_tween:
		rotation_tween.kill()
		rotation_tween = null;
		
	$Body.rotate(deg_to_rad(360 * d))
	
	velocity.y = minf(1500, velocity.y + gravity * d)

func _ground_movement() -> void:
	
	_grounded_rotation();
		
	if Input.is_action_pressed("main"):
		velocity.y = JUMP_VELOCITY;
		
func _grounded_rotation() -> void: 
	
	#The step of the snap is set to 90 degrees since we only want to target rotations at which one face is
	#parallel to the ground.
	var target_rotation: float = snappedf($Body.rotation_degrees, 90);
	
	if (rotation_tween == null && $Body.rotation_degrees != target_rotation):
		rotation_tween = create_tween();
		rotation_tween.tween_property($Body,"rotation_degrees", target_rotation, 0.1)
		
func _nucleus_collision() -> void:
	$Nucleus.modulate = Color.RED if $Nucleus.has_overlapping_bodies() else Color.GREEN_YELLOW;

func _on_shell_area_shape_entered():
	set_physics_process(false)
