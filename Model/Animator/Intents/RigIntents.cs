using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Model.Animator.Intents;

// Building the rig: selection, bones, parts, art, meshes, weights, chains, cloth and effects. The ones marked <see cref="IChangesRig"/> only run while EDIT RIG is on.

/// <summary>Selects a bone, by its index in the skeleton.</summary>
public readonly record struct SelectBone(int Bone) : IIntent<SelectBone>;

/// <summary>Selects a part, by its index in the package.</summary>
public readonly record struct SelectPart(int Part) : IIntent<SelectPart>;

/// <summary>Selects a chain, by its index in the package.</summary>
public readonly record struct SelectChain(int Chain) : IIntent<SelectChain>;

/// <summary>Selects a cloth, by its index in the package.</summary>
public readonly record struct SelectCloth(int Cloth) : IIntent<SelectCloth>;

/// <summary>Lets the selected bones move as well as turn, or stops them.</summary>
public readonly record struct ToggleMovable : IIntent<ToggleMovable>, IChangesRig;

/// <summary>Locks the selected knee or elbow to bend only the way it bends now, or frees it.</summary>
public readonly record struct LockBend : IIntent<LockBend>, IChangesRig;

/// <summary>Limits the selected bones' turn from rest, or frees them.</summary>
public readonly record struct ToggleLimit : IIntent<ToggleLimit>, IChangesRig;

/// <summary>Steps the selected bones' lowest (or, with <paramref name="Max"/>, highest) turn by <paramref name="By"/> steps.</summary>
public readonly record struct StepLimit(bool Max, int By) : IIntent<StepLimit>, IChangesRig;

/// <summary>Moves the selected joints toward the viewer (positive) or away, out of the screen's plane.</summary>
public readonly record struct StepDepth(int By) : IIntent<StepDepth>;

/// <summary>Traces the selected part into a skin mesh and weights it to its bones by heat diffusion.</summary>
public readonly record struct AutoWeights : IIntent<AutoWeights>, IChangesRig;

/// <summary>Starts or stops painting the selected skin's weights in the preview.</summary>
public readonly record struct PaintWeights : IIntent<PaintWeights>, IChangesRig;

/// <summary>Makes the weight brush larger (positive) or smaller.</summary>
public readonly record struct StepBrushRadius(int By) : IIntent<StepBrushRadius>, IChangesRig;

/// <summary>Makes the weight brush stronger (positive) or weaker.</summary>
public readonly record struct StepBrushStrength(int By) : IIntent<StepBrushStrength>, IChangesRig;

/// <summary>Gives the selected part more art it can switch to.</summary>
public readonly record struct AddArt : IIntent<AddArt>, IChangesRig;

/// <summary>Removes the alternate art the selected part shows at the playhead.</summary>
public readonly record struct DropArt : IIntent<DropArt>, IChangesRig;

/// <summary>Opens the XIF editor over everything on the selected part's art (or the selected chain's).</summary>
public readonly record struct EditXif : IIntent<EditXif>, IChangesRig;

/// <summary>Keys the selected part's next (positive) or previous art at the playhead.</summary>
public readonly record struct StepArt(int By) : IIntent<StepArt>, IChangesRig;

/// <summary>Keys the selected part one layer further forward (positive) or back from the playhead on.</summary>
public readonly record struct StepLayerKey(int By) : IIntent<StepLayerKey>;

/// <summary>Moves a layer row forward (positive) or back; <paramref name="Row"/> is its <c>AnimatorLayerRow.Key</c>, 0 for the selection.</summary>
public readonly record struct MoveLayer(int Row, int By) : IIntent<MoveLayer>;

/// <summary>Keys the selected part hidden from the playhead on, or shown again.</summary>
public readonly record struct ToggleArtVisible : IIntent<ToggleArtVisible>;

/// <summary>Draws the selected part as a bending mesh, or rigid again.</summary>
public readonly record struct ToggleMesh : IIntent<ToggleMesh>, IChangesRig;

/// <summary>Starts or stops clicking bones to let them bend the selected mesh.</summary>
public readonly record struct PickMeshBones : IIntent<PickMeshBones>, IChangesRig;

/// <summary>More (positive) or fewer mesh cells.</summary>
public readonly record struct StepMeshDetail(int By) : IIntent<StepMeshDetail>, IChangesRig;

/// <summary>A wider (positive) or narrower zone where two bones share the mesh.</summary>
public readonly record struct StepMeshBlend(int By) : IIntent<StepMeshBlend>, IChangesRig;

/// <summary>Adds a bone under the selected one.</summary>
public readonly record struct AddBone : IIntent<AddBone>, IChangesRig;

/// <summary>Deletes the selected bone.</summary>
public readonly record struct DeleteBone : IIntent<DeleteBone>, IChangesRig;

/// <summary>Starts renaming the selected bone.</summary>
public readonly record struct RenameBone : IIntent<RenameBone>, IChangesRig;

/// <summary>Starts choosing the selected bone's new parent.</summary>
public readonly record struct ReparentBone : IIntent<ReparentBone>, IChangesRig;

/// <summary>Adds a part with a starting square to the selected bone, to draw with the polygon editor.</summary>
public readonly record struct AddPart : IIntent<AddPart>, IChangesRig;

/// <summary>Hangs a new Verlet chain off the selected bone.</summary>
public readonly record struct AddChain : IIntent<AddChain>, IChangesRig;

/// <summary>Deletes the selected chain.</summary>
public readonly record struct DeleteChain : IIntent<DeleteChain>, IChangesRig;

/// <summary>Draws the selected chain as one sheet along it, or as a piece per segment again.</summary>
public readonly record struct ToggleChainSheet : IIntent<ToggleChainSheet>, IChangesRig;

/// <summary>The next bone clicked becomes the one the selected chain hangs from, or its collider follows.</summary>
public readonly record struct PickChainBone(AnimatorChainPick Pick) : IIntent<PickChainBone>, IChangesRig;

/// <summary>Adds a dress hanging from the selected bone.</summary>
public readonly record struct AddCloth : IIntent<AddCloth>, IChangesRig;

/// <summary>Deletes the selected cloth.</summary>
public readonly record struct DeleteCloth : IIntent<DeleteCloth>, IChangesRig;

/// <summary>Starts or stops clicking for the selected cloth: its bone, its pins or its colliders.</summary>
public readonly record struct PickCloth(AnimatorClothPick Pick) : IIntent<PickCloth>, IChangesRig;

/// <summary>Hangs a speed streak off the selected bone, shown from the playhead.</summary>
public readonly record struct AddEffect : IIntent<AddEffect>, IChangesRig;

/// <summary>Hangs a particle stream off the selected bone, flowing from the playhead.</summary>
public readonly record struct AddParticles : IIntent<AddParticles>, IChangesRig;

/// <summary>Deletes the selected effect and its keys.</summary>
public readonly record struct DeleteEffect : IIntent<DeleteEffect>, IChangesRig;

/// <summary>Keys the selected effect shown, or hidden if it is showing, at the playhead.</summary>
public readonly record struct ToggleEffectVisible : IIntent<ToggleEffectVisible>;

/// <summary>Keys the selected effect's opacity one step higher (positive) or lower at the playhead.</summary>
public readonly record struct StepEffectOpacity(int By) : IIntent<StepEffectOpacity>;
