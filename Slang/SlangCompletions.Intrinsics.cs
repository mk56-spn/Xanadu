using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Xanadu.Slang;

public sealed partial class SlangCompletions
{
    /// <summary>Keywords worth offering in an effect body.</summary>
    private static readonly string[] Keywords =
    [
        "if", "else", "for", "while", "return", "break", "continue", "discard", "let", "var", "const",
        "static", "uniform", "in", "out", "inout", "struct", "import", "true", "false",
    ];

    /// <summary>Slang's common intrinsics, with their usual parameters, what they return and what they do. Vectors work per component unless said.</summary>
    private static readonly (string Name, string Detail, string Doc)[] Intrinsics =
    [
        ("abs", "(x) → x's type", "x without its sign."),
        ("acos", "(x) → x's type", "The angle in radians whose cosine is x (x in -1..1); 0 to π."),
        ("asin", "(x) → x's type", "The angle in radians whose sine is x (x in -1..1); -π/2 to π/2."),
        ("atan", "(x) → x's type", "The angle in radians whose tangent is x; -π/2 to π/2. For a direction's angle use atan2."),
        ("atan2", "(y, x) → x's type", "The angle of the direction (x, y) in radians, -π to π, measured from +x. Note y comes first."),
        ("ceil", "(x) → x's type", "The smallest whole number not below x."),
        ("clamp", "(x, min, max) → x's type", "x held between min and max."),
        ("cos", "(x) → x's type", "Cosine of x radians."),
        ("cross", "(a, b) → float3", "The cross product of two float3s: a vector at right angles to both."),
        ("ddx", "(x) → x's type", "How much x changes to the next pixel across. Neighbouring pixels share it (2×2 blocks)."),
        ("ddy", "(x) → x's type", "How much x changes to the next pixel down. Neighbouring pixels share it (2×2 blocks)."),
        ("degrees", "(radians) → radians's type", "Radians turned into degrees."),
        ("distance", "(a, b) → float", "How far apart two points are: length(a - b)."),
        ("dot", "(a, b) → float", "The dot product: the sum of a × b component by component. For unit vectors, the cosine of the angle between them."),
        ("exp", "(x) → x's type", "e to the power x."),
        ("exp2", "(x) → x's type", "2 to the power x."),
        ("floor", "(x) → x's type", "The largest whole number not above x."),
        ("fmod", "(x, y) → x's type", "The remainder of x / y, with x's sign (so negative x gives a negative result). The library's mod() wraps instead."),
        ("frac", "(x) → x's type", "The part of x after the point: x - floor(x), always 0 to 1."),
        ("fwidth", "(x) → x's type", "abs(ddx(x)) + abs(ddy(x)): how much x changes over one pixel. What edges are smoothed over."),
        ("length", "(v) → float", "A vector's length."),
        ("lerp", "(a, b, t) → a's type", "From a to b by t: a at 0, b at 1, a + (b - a) × t. Not held to 0..1."),
        ("log", "(x) → x's type", "The natural logarithm of x."),
        ("log2", "(x) → x's type", "The base 2 logarithm of x."),
        ("max", "(a, b) → a's type", "The larger of a and b."),
        ("min", "(a, b) → a's type", "The smaller of a and b."),
        ("mul", "(a, b) → a vector or matrix", "Matrix product: a matrix times a vector, a vector times a matrix, or two matrices."),
        ("normalize", "(v) → v's type", "v scaled to length 1, pointing the same way. Undefined for a zero vector."),
        ("pow", "(x, y) → x's type", "x to the power y. Undefined for negative x."),
        ("radians", "(degrees) → degrees's type", "Degrees turned into radians."),
        ("reflect", "(i, n) → i's type", "Direction i bounced off a surface with unit normal n."),
        ("round", "(x) → x's type", "x to the nearest whole number."),
        ("rsqrt", "(x) → x's type", "1 / sqrt(x)."),
        ("saturate", "(x) → x's type", "x held to 0..1."),
        ("sign", "(x) → x's type", "-1, 0 or 1 by x's sign."),
        ("sin", "(x) → x's type", "Sine of x radians."),
        ("smoothstep", "(edge0, edge1, x) → x's type", "0 at edge0 to 1 at edge1, eased at both ends; held outside. Swap the edges to go 1 to 0."),
        ("sqrt", "(x) → x's type", "The square root of x."),
        ("step", "(edge, x) → x's type", "0 where x is below edge, 1 from edge on. A hard edge; smoothstep gives a soft one."),
        ("tan", "(x) → x's type", "Tangent of x radians."),
        ("tanh", "(x) → x's type", "Hyperbolic tangent: an S-curve from -1 to 1, through 0 at 0."),
        ("trunc", "(x) → x's type", "x with the part after the point dropped (towards 0)."),
    ];
}
