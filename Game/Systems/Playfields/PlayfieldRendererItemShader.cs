using Friflo.Engine.ECS;
using Xanadu.Ecs;
using Xanadu.Systems.Canvas;

namespace Xanadu.Systems.Playfields;

/// <summary>
/// Picks the shader an item draws with (Docs/render-architecture.md D11, step
/// 8): the group its shader source names, when that group is in its mask and
/// has a track, with the track's keyed values at the playhead; otherwise its
/// own static shader.
/// </summary>
public static partial class PlayfieldRenderer
{
    private static GroupDefinition[] _groupTracks = [];

    private static float _groupSeconds;

    /// <summary>The level's groups and the playhead, set for the frame being drawn.</summary>
    private static void SetGroupTracks(GroupDefinition[]? groups, float seconds)
    {
        _groupTracks = groups ?? [];
        _groupSeconds = seconds;
    }

    /// <summary>
    /// Selects the item's shader and blend on the canvas, writing the followed
    /// group's keyed instance values. True when a group supplied the shader.
    /// </summary>
    public static bool SelectItemShader(IRectCanvas canvas, Entity entity)
    {
        uint mask = !entity.IsNull && entity.TryGetComponent(out ItemGroups groups) ? groups.Mask : 0;
        int source = entity.IsNull ? ItemShaderSource.Own : ItemShaderSource.Of(entity);
        if (GroupModulation.TryItemShader(
                _groupTracks,
                mask,
                source,
                _groupSeconds,
                out int slot,
                out var values))
        {
            canvas.SelectShader(slot, ItemBlend.Of(entity), source);
            foreach ((string param, Vector4 value, int size) in values)
                canvas.SetShaderValue(param, value, size);
            return true;
        }

        string common = entity.IsNull ? "" : ItemCommonShader.Of(entity);
        if (common.Length > 0)
            canvas.SelectCommonShader(common, ItemBlend.Of(entity));
        else
            canvas.SelectShader(ItemShader.Of(entity), ItemBlend.Of(entity));
        return false;
    }
}
