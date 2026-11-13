

[fragment]
float4 shade(ItemVaryings v) : SV_Target
{
    colour c = colours.blue;


    c.paint(Circle(5), colours.red);

    return c;
}
