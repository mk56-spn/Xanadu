// Where things live in the snow scene's data textures, all WIDTH texels wide
// and read row by row.
//
// Geometry (uploaded once): four texels per triangle, its three corners as xyz
// and its baked colour. Corners are wound so cross(b - a, c - a) points out of
// the front face. A chunk's triangles are contiguous and keep a fixed order.
//
// Chunks (uploaded once): two texels per chunk, (centre xyz, sort bias) and
// (first triangle, triangle count). Chunks are what gets sorted: a tree, the
// cabin, a terrain tile.
//
// Ranks (blit 1): per chunk, (rank, triangles drawn before it).
// Order (blit 2): per rank, (chunk, triangles before it, first triangle).

const int WIDTH = 256;
const float NEAR = 0.25;

ivec2 slot_texel(int i) {
	return ivec2(i % WIDTH, i / WIDTH);
}

vec4 triangle_texel(sampler2D data, int id, int part) {
	return texelFetch(data, slot_texel(id * 4 + part), 0);
}

vec4 chunk_texel(sampler2D data, int id, int part) {
	return texelFetch(data, slot_texel(id * 2 + part), 0);
}
