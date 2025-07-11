float checkerboard(vec2 uv){
	 float Size = 10.0;
    vec2 Pos = floor(uv / Size);
    float PatternMask = mod(Pos.x + mod(Pos.y, 2.0), 2.0);
}