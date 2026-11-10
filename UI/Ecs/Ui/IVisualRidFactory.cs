
namespace Xanadu.Ecs;

/// <summary>Creates canvas items for UI entities.</summary>
public interface IVisualRidFactory
{
    Handle CreateCanvasItem();

    /// <summary>
    /// Whether scenes should load shader materials and textures for what this
    /// makes. Test factories don't, so scenes build without assets.
    /// </summary>
    bool LoadsAssets => false;
}
