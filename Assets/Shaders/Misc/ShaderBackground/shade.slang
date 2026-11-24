import xanadu;

float4 shade(Canvas canvas)
{
    colour c = colours.darkGray;

    c.paint(Checker(20), colours.gray);
    c.brighten(0.3);
    c.paint(Box(canvas.size).outlined(2), colours.gray);

    return c;
}
