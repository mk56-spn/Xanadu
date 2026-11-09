using System.Globalization;
using Friflo.Engine.ECS;
using Godot;
using Xanadu.Animation;
using Xanadu.Timeline;
using Xanadu.Tools.Animator.Systems;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>Executes <see cref="ToolAction"/> requests against the session entity.</summary>
public static class AnimatorCommands
{
    public static void Run(Entity session, ToolAction action)
    {
        AnimatorPendingPicks.Dismiss(session, action.Kind);

        switch (action.Kind)
        {
            case ToolActionKind.AddArt:
                AnimatorArtCommands.OpenArtPicker(session);
                break;
            case ToolActionKind.DropArt:
                AnimatorArtCommands.DropArt(session);
                break;
            case ToolActionKind.PreviousArt:
                AnimatorArtCommands.StepArt(session, -1);
                break;
            case ToolActionKind.NextArt:
                AnimatorArtCommands.StepArt(session, 1);
                break;
            case ToolActionKind.MoveLayerForward:
                AnimatorLayerCommands.Move(session, action.Argument, 1);
                break;
            case ToolActionKind.MoveLayerBack:
                AnimatorLayerCommands.Move(session, action.Argument, -1);
                break;
            case ToolActionKind.LayerDownKey:
                AnimatorArtCommands.StepLayer(session, -1);
                break;
            case ToolActionKind.LayerUpKey:
                AnimatorArtCommands.StepLayer(session, 1);
                break;
            case ToolActionKind.OpenArtFile:
                AnimatorArtCommands.OpenFile(session);
                break;
            case ToolActionKind.ToggleArtVisible:
                AnimatorArtCommands.ToggleVisible(session);
                break;
            case ToolActionKind.ToggleMesh:
                AnimatorArtCommands.ToggleMesh(session);
                break;
            case ToolActionKind.PickMeshBones:
                AnimatorArtCommands.TogglePickBones(session);
                break;
            case ToolActionKind.AutoWeights:
                AnimatorWeightCommands.AutoWeights(session);
                break;
            case ToolActionKind.PaintWeights:
                AnimatorWeightCommands.TogglePaint(session);
                break;
            case ToolActionKind.BrushSmaller:
                AnimatorWeightCommands.StepRadius(session, -1);
                break;
            case ToolActionKind.BrushLarger:
                AnimatorWeightCommands.StepRadius(session, 1);
                break;
            case ToolActionKind.BrushWeaker:
                AnimatorWeightCommands.StepStrength(session, -1);
                break;
            case ToolActionKind.BrushStronger:
                AnimatorWeightCommands.StepStrength(session, 1);
                break;
            case ToolActionKind.MeshDetailDown:
                AnimatorArtCommands.StepDetail(session, -1);
                break;
            case ToolActionKind.MeshDetailUp:
                AnimatorArtCommands.StepDetail(session, 1);
                break;
            case ToolActionKind.MeshBlendDown:
                AnimatorArtCommands.StepBlend(session, -1);
                break;
            case ToolActionKind.MeshBlendUp:
                AnimatorArtCommands.StepBlend(session, 1);
                break;
            case ToolActionKind.Play:
                session.GetComponent<AnimatorDocument>().Playback.Play();
                AnimatorSession.SetStatus(session, "PLAYING");
                break;
            case ToolActionKind.Stop:
                AnimatorSessionCommands.Stop(session);
                break;
            case ToolActionKind.SequenceGame:
                AnimatorSequenceCommands.StartGame(session);
                break;
            case ToolActionKind.SequenceAll:
                AnimatorSequenceCommands.StartAll(session);
                break;
            case ToolActionKind.CycleAttacks:
                AnimatorAttackCycleCommands.Toggle(session);
                break;
            case ToolActionKind.MetronomeSlower:
                AnimatorAttackCycleCommands.StepBpm(session, -1);
                break;
            case ToolActionKind.MetronomeFaster:
                AnimatorAttackCycleCommands.StepBpm(session, 1);
                break;
            case ToolActionKind.AttackMoreOften:
                AnimatorAttackCycleCommands.StepEvery(session, -1);
                break;
            case ToolActionKind.AttackLessOften:
                AnimatorAttackCycleCommands.StepEvery(session, 1);
                break;
            case ToolActionKind.AllowTransition:
                AnimatorFlowCommands.Toggle(session, action.Argument);
                break;
            case ToolActionKind.SelectTransition:
                AnimatorFlowCommands.Select(session, action.Argument);
                break;
            case ToolActionKind.TransitionFadeDown:
                AnimatorFlowCommands.StepFade(session, -XafTransitions.FadeStep);
                break;
            case ToolActionKind.TransitionFadeUp:
                AnimatorFlowCommands.StepFade(session, XafTransitions.FadeStep);
                break;
            case ToolActionKind.TransitionCurve:
                AnimatorFlowCommands.SetCurve(session, action.Argument);
                break;
            case ToolActionKind.PreviewTransition:
                AnimatorFlowCommands.Preview(session);
                break;
            case ToolActionKind.ToggleFlowLoop:
                AnimatorFlowCommands.ToggleLoop(session);
                break;
            case ToolActionKind.ToggleSlow:
                AnimatorFlowCommands.ToggleSlow(session);
                break;
            case ToolActionKind.AddEffect:
                AnimatorEffectCommands.Add(session);
                break;
            case ToolActionKind.AddParticles:
                AnimatorEffectCommands.AddParticles(session);
                break;
            case ToolActionKind.SelectEffect:
                if (int.TryParse(action.Argument, out int effect))
                    AnimatorEffectCommands.Select(session, effect);
                break;
            case ToolActionKind.DeleteEffect:
                AnimatorEffectCommands.Delete(session);
                break;
            case ToolActionKind.ToggleEffectVisible:
                AnimatorEffectCommands.ToggleVisible(session);
                break;
            case ToolActionKind.EffectOpacityDown:
                AnimatorEffectCommands.StepOpacity(session, -AnimatorEffectCommands.OpacityStep);
                break;
            case ToolActionKind.EffectOpacityUp:
                AnimatorEffectCommands.StepOpacity(session, AnimatorEffectCommands.OpacityStep);
                break;
            case ToolActionKind.Clip:
                AnimatorSessionCommands.PlayClip(session, action.Argument);
                break;
            case ToolActionKind.TogglePreviewMode:
                AnimatorPreviewModeCommands.Set(session, !session.GetComponent<AnimatorPreviewMode>().On);
                break;
            case ToolActionKind.ExitPreviewMode:
                AnimatorPreviewModeCommands.Set(session, false);
                break;
            case ToolActionKind.Undo:
                AnimatorHistoryCommands.Undo(session);
                break;
            case ToolActionKind.Redo:
                AnimatorHistoryCommands.Redo(session);
                break;
            case ToolActionKind.Interpolate:
                if (System.Enum.TryParse(action.Argument, out EasePreset preset))
                    AnimatorKeyCommands.SetInterpolation(session, preset);
                break;
            case ToolActionKind.OpenNewClip:
                AnimatorNewClipDialogCommands.Open(
                    session,
                    action.Argument == "duplicate");
                break;
            case ToolActionKind.CreateClip:
                AnimatorNewClipDialogCommands.Create(session);
                break;
            case ToolActionKind.CloseNewClip:
                AnimatorNewClipDialogCommands.Close(session);
                break;
            case ToolActionKind.ToggleEasingWindow:
                ref AnimatorEasingWindow easing = ref session.GetComponent<AnimatorEasingWindow>();
                easing.Open = !easing.Open;
                break;
            case ToolActionKind.OpenInfo:
                session.GetComponent<AnimatorInfoPanel>().Open = true;
                break;
            case ToolActionKind.CloseInfo:
                session.GetComponent<AnimatorInfoPanel>().Open = false;
                break;
            case ToolActionKind.TogglePathWindow:
                ref AnimatorModes modes = ref session.GetComponent<AnimatorModes>();
                modes.PathWindow = !modes.PathWindow;
                AnimatorSession.SetStatus(session, modes.PathWindow
                    ? "MOTION PATHS: ONE SECOND AROUND THE PLAYHEAD"
                    : "MOTION PATHS: WHOLE CLIP");
                break;
            case ToolActionKind.OpenClipLength:
                session.GetComponent<AnimatorClipLength>().Open = true;
                AnimatorSession.SetStatus(
                    session,
                    "TYPE A LENGTH OR STEP WITH THE ARROWS");
                break;
            case ToolActionKind.DuplicateClip:
                AnimatorClipCommands.Duplicate(session);
                break;
            case ToolActionKind.DeleteClip:
                AnimatorClipCommands.Delete(session);
                break;
            case ToolActionKind.DurationDown:
                AnimatorClipCommands.StepDuration(session, -AnimatorSessionCommands.DurationStep(action.Argument));
                break;
            case ToolActionKind.DurationUp:
                AnimatorClipCommands.StepDuration(session, AnimatorSessionCommands.DurationStep(action.Argument));
                break;
            case ToolActionKind.ToggleLoop:
                AnimatorClipCommands.ToggleLoop(session);
                break;
            case ToolActionKind.SelectBone:
                AnimatorSessionCommands.SelectBone(session, action.Argument);
                break;
            case ToolActionKind.SelectAttachment:
                AnimatorSessionCommands.SelectAttachment(session, action.Argument);
                break;
            case ToolActionKind.LimbTarget:
                AnimatorTargetCommands.Run(session, action.Argument);
                break;
            case ToolActionKind.PlantBone:
                AnimatorPlantCommands.Toggle(session);
                break;
            case ToolActionKind.KeyBone:
                AnimatorEditing.KeySelected(session);
                break;
            case ToolActionKind.RotateLeft:
                AnimatorEditing.Nudge(session, -AnimatorEditing.RotationStep, 0f);
                break;
            case ToolActionKind.RotateRight:
                AnimatorEditing.Nudge(session, AnimatorEditing.RotationStep, 0f);
                break;
            case ToolActionKind.ScaleDown:
                AnimatorEditing.Nudge(session, 0f, -AnimatorEditing.ScaleStep);
                break;
            case ToolActionKind.ScaleUp:
                AnimatorEditing.Nudge(session, 0f, AnimatorEditing.ScaleStep);
                break;
            case ToolActionKind.AddEvent:
                AnimatorEventCommands.Add(session);
                break;
            case ToolActionKind.DeleteEvent:
                AnimatorEventCommands.Delete(session);
                break;
            case ToolActionKind.LockBend:
                AnimatorEditing.LockBend(session);
                break;
            case ToolActionKind.DepthAway:
                AnimatorEditing.NudgeDepth(session, -AnimatorEditing.DepthStep);
                break;
            case ToolActionKind.DepthToward:
                AnimatorEditing.NudgeDepth(session, AnimatorEditing.DepthStep);
                break;
            case ToolActionKind.ToggleLimit:
                AnimatorEditing.ToggleLimit(session);
                break;
            case ToolActionKind.LimitMinDown:
                AnimatorEditing.StepLimit(session, max: false, -1);
                break;
            case ToolActionKind.LimitMinUp:
                AnimatorEditing.StepLimit(session, max: false, 1);
                break;
            case ToolActionKind.LimitMaxDown:
                AnimatorEditing.StepLimit(session, max: true, -1);
                break;
            case ToolActionKind.LimitMaxUp:
                AnimatorEditing.StepLimit(session, max: true, 1);
                break;
            case ToolActionKind.ToggleMovable:
                AnimatorEditing.ToggleMovable(session);
                break;
            case ToolActionKind.AddBone:
                AnimatorEditing.AddBone(session);
                break;
            case ToolActionKind.DeleteBone:
                AnimatorEditing.DeleteBone(session);
                break;
            case ToolActionKind.RenameBone:
                AnimatorSessionCommands.BeginBoneRename(session);
                break;
            case ToolActionKind.ReparentBone:
                AnimatorSessionCommands.BeginBoneReparent(session);
                break;
            case ToolActionKind.AddKey:
                AnimatorSessionCommands.AddKeyFromMenu(session);
                break;
            case ToolActionKind.DeleteKey:
                AnimatorKeyCommands.DeleteSelected(session);
                break;
            case ToolActionKind.AddSvg:
                AnimatorSessionCommands.OpenSvgMenu(session);
                break;
            case ToolActionKind.AttachSvg:
                AnimatorSessionCommands.AttachSvgFromMenu(session, action.Argument);
                break;
            case ToolActionKind.AddChain:
                AnimatorChainCommands.Add(session);
                break;
            case ToolActionKind.SelectChain:
                if (int.TryParse(action.Argument, out int chain))
                    AnimatorChainCommands.Select(session, chain);
                break;
            case ToolActionKind.ToggleChainSheet:
                AnimatorChainCommands.ToggleSheet(session);
                break;
            case ToolActionKind.DeleteChain:
                AnimatorChainCommands.Delete(session);
                break;
            case ToolActionKind.PickChainArt:
                AnimatorChainPlacementCommands.OpenArtPicker(session);
                break;
            case ToolActionKind.PickChainBone:
                AnimatorChainPlacementCommands.BeginPick(session, AnimatorChainPick.Anchor);
                break;
            case ToolActionKind.PickColliderBone:
                AnimatorChainPlacementCommands.BeginPick(session, AnimatorChainPick.Collider);
                break;
            case ToolActionKind.AddCloth:
                AnimatorClothCommands.Add(session);
                break;
            case ToolActionKind.SelectCloth:
                if (int.TryParse(action.Argument, out int cloth))
                    AnimatorClothCommands.Select(session, cloth);
                break;
            case ToolActionKind.DeleteCloth:
                AnimatorClothCommands.Delete(session);
                break;
            case ToolActionKind.PickClothBone:
                AnimatorClothCommands.TogglePick(session, AnimatorClothPick.Bone);
                break;
            case ToolActionKind.PinClothPoints:
                AnimatorClothCommands.TogglePick(session, AnimatorClothPick.Pins);
                break;
            case ToolActionKind.PickClothColliders:
                AnimatorClothCommands.TogglePick(session, AnimatorClothPick.Colliders);
                break;
            case ToolActionKind.TogglePlay:
                AnimatorSessionCommands.TogglePlay(session);
                break;
            case ToolActionKind.ToggleMode:
                AnimatorSessionCommands.ToggleMode(session, action.Argument);
                break;
            case ToolActionKind.KeyPose:
                AnimatorPoseCommands.KeyPose(session);
                break;
            case ToolActionKind.CopyPose:
                AnimatorPoseCommands.CopyPose(session);
                break;
            case ToolActionKind.PastePose:
                AnimatorPoseCommands.PastePose(session);
                break;
            case ToolActionKind.MirrorPose:
                AnimatorPoseCommands.MirrorPose(session);
                break;
            case ToolActionKind.Save:
                AnimatorFileCommands.Save(session);
                break;
            case ToolActionKind.Load:
                AnimatorFileCommands.Load(session);
                break;
            case ToolActionKind.NewSkeleton:
                AnimatorFileCommands.NewSkeleton(session);
                break;
            case ToolActionKind.CloseEditor:
                AnimatorFileCommands.CloseEditor(session);
                break;
            case ToolActionKind.FitPreview:
                AnimatorPreviewCommands.Fit(session);
                break;
            case ToolActionKind.ResetPreview:
                AnimatorPreviewCommands.Reset(session);
                break;
            case ToolActionKind.ResetTransform:
                AnimatorTransformCommands.ResetSelected(session);
                break;
            case ToolActionKind.CopyKeys:
                AnimatorKeyClipboardCommands.Copy(session);
                break;
            case ToolActionKind.PasteKeys:
                AnimatorKeyClipboardCommands.Paste(session);
                break;
            case ToolActionKind.SelectAllKeys:
                AnimatorKeyClipboardCommands.SelectAll(session);
                break;
            case ToolActionKind.RemoveOverride:
                AnimatorOverrideCommands.Remove(session, session.GetComponent<AnimatorKeyMenu>().Override, keepKeys: false);
                break;
            case ToolActionKind.DetachOverride:
                AnimatorOverrideCommands.Remove(session, session.GetComponent<AnimatorKeyMenu>().Override, keepKeys: true);
                break;
        }
    }
}
