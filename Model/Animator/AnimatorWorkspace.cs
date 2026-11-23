namespace Xanadu.Model.Animator;

/// <summary>
/// The animator's three workspaces, one at a time: building the rig (the rest
/// pose, every clip moves with it), animating a clip, or watching it with the
/// editing panels gone. Read from <see cref="AnimatorModes.Rest"/> and
/// <see cref="AnimatorPreviewMode.On"/>; the header's three-way switch sets it.
/// </summary>
public enum AnimatorWorkspace
{
    Rig,
    Animate,
    Preview,
}
