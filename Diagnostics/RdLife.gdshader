// One Game of Life generation, blitted from one DrawableTexture2D to the other.
// R is alive, G is how long the cell has lived, B is a trail left behind by the
// dead. The grid wraps at the edges. RdLife.slang paints the result.
shader_type texture_blit;
render_mode blend_disabled;

uniform sampler2D source_texture0 : hint_blit_source0, filter_nearest;

const float AGE_STEP = 1.0 / 32.0;
const float TRAIL_FADE = 1.0 / 16.0;

void blit() {
	ivec2 size = textureSize(source_texture0, 0);
	ivec2 cell = ivec2(UV * vec2(size));

	int neighbours = 0;
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			if (x == 0 && y == 0) {
				continue;
			}
			ivec2 neighbour = (cell + ivec2(x, y) + size) % size;
			if (texelFetch(source_texture0, neighbour, 0).r > 0.5) {
				neighbours++;
			}
		}
	}

	vec4 current = texelFetch(source_texture0, cell, 0);
	bool alive = current.r > 0.5;
	bool next = neighbours == 3 || (alive && neighbours == 2);

	float age = next && alive ? min(current.g + AGE_STEP, 1.0) : 0.0;
	float trail = next ? 1.0 : max(current.b - TRAIL_FADE, 0.0);
	COLOR0 = vec4(next ? 1.0 : 0.0, age, trail, 1.0);
}
