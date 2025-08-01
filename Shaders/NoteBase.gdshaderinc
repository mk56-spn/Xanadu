#include "Base.gdshaderinc"

global uniform float song_pos;
instance uniform float note_pos;
instance uniform float hit_pos;

const float preempt =0.4;
const float hit_duration = 0.5;

float noteProgress(){
	float clamped_progress = -clamp(song_pos - note_pos,-preempt,0);
	 return (1./preempt) * clamped_progress ;
}

float hitProgress(){
	float clamped_progress = clamp(song_pos - hit_pos,0,10);
	 return (1./preempt) * clamped_progress ;
}

float getPulse(){
	float beats_per_second = 200.0 / 60.0;
    float current_beat = song_pos * beats_per_second;
	float beat_progress = fract(current_beat);
	float pulse = pow(1.0 - beat_progress, 4.0);
	return pulse;
}