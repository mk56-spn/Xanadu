import xanadu;



[Default(20,20)]uniform float2 size;

float4 shade(ItemInput i, Canvas canvas)
{
    colour col = colours.transparent;
    
    Box square = Box(size);

    square.anchor(anchor.bottomLeft);

    square.move(size /2 );
    Line s  = Line(20, 10, 3);

    col.paint(square);
    col.paint(s);

    return col;
}
