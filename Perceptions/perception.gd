class_name Perception;
extends CharacterBody2D;

# Get the gravity from the project settings to be synced with RigidBody nodes.
var gravity: int = ProjectSettings.get_setting("physics/2d/default_gravity")


# This should not be altered to obtain different directions or speeds, instead a multiplier should be applied to it
const BASE_VELOCITY : int = 700;


func _ready() -> void:
	velocity.x = BASE_VELOCITY;
