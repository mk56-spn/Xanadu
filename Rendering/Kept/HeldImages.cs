using Friflo.Engine.ECS;
using Texture = MoonWorks.Graphics.Texture;

namespace Xanadu.Rendering.Kept;

/// <summary>
/// The images an item's graph holds from frame to frame (<c>[Kept]</c>, and each <c>[History]</c> with its source), on
/// the entity keeping the item's range (<see cref="KeptMesh"/>), one relation per piece of it, keyed by where the piece
/// starts. Made for one compile of an effect at one size; made again when either changes, and let go with the range.
/// </summary>
/// <param name="Held">By the images' places: a held texture, or null for an image the frame's pool gives.</param>
internal struct HeldImages(uint start, CanvasEffect effect, uint width, uint height, Texture?[] held) : IRelation<uint>
{
    public uint Start = start;
    public CanvasEffect Effect = effect;
    public uint Width = width, Height = height;
    public Texture?[] Held = held;

    public readonly uint GetRelationKey() => Start;

    /// <summary>Lets go of every held texture on the entity; call it before the entity goes.</summary>
    public static void Release(Entity entity)
    {
        foreach (HeldImages images in entity.GetRelations<HeldImages>())
            images.Dispose();
    }

    public readonly void Dispose()
    {
        foreach (Texture? texture in Held)
            texture?.Dispose();
    }
}
