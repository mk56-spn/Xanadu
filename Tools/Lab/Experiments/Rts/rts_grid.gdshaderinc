// The spatial grid, rebuilt every frame by blits: cells of CELL px over the
// field. Include after rts_rules.gdshaderinc.
//
// Keys and sorted list: slot i (texel i % WIDTH, i / WIDTH) is x cell key,
// y unit id. Dead units and padding carry NO_CELL, so they sort to the end.
// The grid texture is the sorted list's rows, then a table: per cell,
// x its first slot, y one past its last.

const float CELL = 160.0;
const int REACH = 2;            // cells scanned each way: REACH * CELL covers SIGHT, every short RANGE and artillery splash
const int SNIPER_REACH = 4;     // a sniper's own scan, to pick targets at its RANGE; its shots go through the fire list
const float NO_CELL = 1e9;

ivec2 grid_size(vec4 field) {
	return ivec2(ceil((field.zw - field.xy) / CELL));
}

ivec2 cell_of(vec2 pos, vec4 field) {
	return clamp(ivec2(floor((pos - field.xy) / CELL)), ivec2(0), grid_size(field) - 1);
}

ivec2 slot_texel(int i) {
	return ivec2(i % WIDTH, i / WIDTH);
}
