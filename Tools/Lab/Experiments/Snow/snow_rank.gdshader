// Sort blit 1: each chunk counts the chunks that draw before it (farther, or
// as far with a lower id). That count is its rank; the sum of their triangle
// counts is where its triangles start. No sorting network: one pass, each
// fragment loops over every chunk, a few hundred reads that all hit the cache.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D chunks_in : hint_blit_source0;
uniform int chunks;

#include "res://Experiments/Snow/snow_view.gdshaderinc"
#include "res://Experiments/Snow/snow_slots.gdshaderinc"

// Far sorts first: the key is minus the biased view depth of the chunk's centre.
float key_of(int id) {
	vec4 c = chunk_texel(chunks_in, id, 0);
	return -(dot(c.xyz - cam_pos, cam_fwd) + c.w);
}

void blit() {
	ivec2 texel = ivec2(FRAGCOORD.xy);
	int me = texel.y * WIDTH + texel.x;
	if (me >= chunks) {
		COLOR0 = vec4(0.0);
	} else {
		float mine = key_of(me);
		float rank = 0.0;
		float start = 0.0;
		for (int o = 0; o < chunks; o++) {
			float other = key_of(o);
			if (other < mine || (other == mine && o < me)) {
				rank += 1.0;
				start += chunk_texel(chunks_in, o, 1).y;
			}
		}
		COLOR0 = vec4(rank, start, 0.0, 0.0);
	}
}
