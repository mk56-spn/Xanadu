import xanadu;

float4 shade()
{
    colour c = colours.blue;


    c.paint(Circle(5), colours.red);

    return c;
}
