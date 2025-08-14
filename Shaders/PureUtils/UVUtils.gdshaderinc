struct RepeatUV{ vec2 repeated_uv; vec2 id;};

 RepeatUV tile_uv_with_ID( vec2 p, float s)
{
	RepeatUV data;
 	data.id = round(p/s);
    data.repeated_uv = p - s*data.id;
	return data;
}