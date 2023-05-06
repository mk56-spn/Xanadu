extends CharacterBody2D


const SPEED = 300.0
const JUMP_VELOCITY = -1900.0

# This should not be altered to obtain different directions or speeds, instead a multiplier should be applied to it
const BASE_VELOCITY : int = 700;

# Get the gravity from the project settings to be synced with RigidBody nodes.
var gravity: int = ProjectSettings.get_setting("physics/2d/default_gravity")

func _ready() -> void:
	velocity.x = BASE_VELOCITY;

func _physics_process(delta: float) -> void:
	
	if self.is_on_floor(): 
		_ground_movement();
	else: 
		_air_movement(delta);
		
	move_and_slide()
	
func _air_movement(d : float) -> void:
	velocity.y = minf(1500, velocity.y + gravity * d)


func _ground_movement() -> void:
	if Input.is_action_pressed("main"):
		velocity.y = JUMP_VELOCITY;
