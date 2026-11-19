import xanadu;

bool isCheckered(float x)
{
    return (x > 20.0) && ((int(ceil(x / 20.0)) & 1) == 0);
}   

[Default(colours.gold)] uniform colour tint;

float4 shade(ItemInput i, Canvas canvas)
{
    int moveDirection =  isCheckered(abs(canvas.centred.y)) == 1 ? 1 : -1 ;
    colour col  = colours.red;

    

    col.paint(Checker(20).moved(float2(time * 100 * moveDirection, 0)));
    return col;
}


