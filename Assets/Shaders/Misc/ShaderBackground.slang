import xanadu;

float4 shade()
{
    colour c = colours.darkGray;

    c.paint(Checker(20), colours.gray);
    c.brighten(0.3);

    return c;
}
