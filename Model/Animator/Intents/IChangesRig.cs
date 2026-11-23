namespace Xanadu.Model.Animator.Intents;

/// <summary>
/// Marks an intent that builds or rebuilds the rig itself (bones, joints,
/// parts, art, mesh, chains, cloth, effects). Such intents only run while
/// EDIT RIG is on, so animating can never change the rig by accident.
/// </summary>
public interface IChangesRig;
