using Xanadu.Core.Rendering;

namespace Xanadu.Ui.Systems.Canvas;

/// <summary>
/// Triangles in their own space, every three corners one triangle, drawn as many copies at once
/// (<see cref="IRectCanvas.DrawCopies"/>). The host keeps them on the GPU while they are drawn and
/// sends them again only after <see cref="Edited"/>. A curve tag per triangle (<see cref="CanvasCurves"/>)
/// draws it as its chord, as every shaded draw does.
/// </summary>
public sealed class CanvasMesh(Vector2[] points, Color[] colors, Vector2[] uvs, byte[]? curves = null)
{
    public Vector2[] Points { get; private set; } = points;
    public Color[] Colors { get; private set; } = colors;
    public Vector2[] Uvs { get; private set; } = uvs;
    public byte[]? Curves { get; private set; } = curves;

    /// <summary>Goes up with every <see cref="Edited"/>, so the host knows to send the triangles again.</summary>
    public int Version { get; private set; }

    /// <summary>Takes new triangles in place of the old.</summary>
    public void Edited(Vector2[] points, Color[] colors, Vector2[] uvs, byte[]? curves = null)
    {
        (Points, Colors, Uvs, Curves) = (points, colors, uvs, curves);
        Version++;
    }
}

/// <summary>
/// One copy of a <see cref="CanvasMesh"/>: where it goes (<paramref name="Place"/> maps the mesh's space to the
/// canvas item's), the colour laid over it (times <paramref name="Modulate"/>, then towards <paramref name="Flash"/>
/// by <paramref name="FlashAmount"/>), its <c>unit</c> (target pixels per mesh unit, as the shader sees it), how far
/// its uv's x moves on (an instancer's copy number), and where its values start in the draw's values.
/// </summary>
/// <param name="Values">
/// The first of this copy's values in the span handed with it: one per value the material's shader declares, in the
/// order <see cref="HostPlatform.DescribeMaterial"/> lists them; a matrix value takes the identity.
/// </param>
public readonly record struct CanvasCopy(Transform2D Place, Color Modulate, Color Flash, float FlashAmount, float Unit, float UvShift,
    int Values);
