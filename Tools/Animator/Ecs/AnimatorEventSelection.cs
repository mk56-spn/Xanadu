using Friflo.Engine.ECS;

namespace Xanadu.Tools.Animator;

/// <summary>
/// The clip event picked on the ruler, kept apart from the bone and art
/// selection so picking an event does not drop what is being posed. It only
/// counts while its clip is the one open.
/// </summary>
public struct AnimatorEventSelection
{
    public string Clip;
    public int Index;

    public static AnimatorEventSelection None => new() { Clip = "", Index = -1 };
}
