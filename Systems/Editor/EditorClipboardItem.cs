using Xanadu.Ecs;

namespace Xanadu.Systems.Editor;

/// <summary>One item on the editor clipboard, copied as a value so the source can change.</summary>
internal readonly record struct EditorClipboardItem(
    long TimeMs,
    Note? Note,
    Mine? Mine,
    StartPoint? StartPoint,
    uint Groups,
    int Shader = 0,
    uint Modulate = SelfModulate.White,
    Block? Block = null,
    Spike? Spike = null,
    LevelText? Text = null,
    GrassPatch? Grass = null,
    LevelPolygon? Polygon = null)
{
    /// <summary>Untimed items: pasted a grid cell along rather than at the playhead.</summary>
    public bool IsGeometry => Block is not null || Spike is not null || Text is not null || Grass is not null || Polygon is not null;
}
