// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

// ReSharper disable once ArrangeNamespaceBody
namespace XanaduProject.Factories.ShaderFactoryHelpers;

public static class VertexShaders
{
    public static readonly string ROTATION_SPEED = "rotation_speed";

    public static readonly VertexPartial ROTATION_VERTEX =
        new("""
            float angle = TIME * rotation_speed;
            mat2 rotation_matrix = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
            VERTEX = (rotation_matrix * (VERTEX - vec2(0.5, 0.5))) + vec2(0.5, 0.5);
            """,
            [new Uniform<float>(ROTATION_SPEED, float.Pi * 2)]);
}
