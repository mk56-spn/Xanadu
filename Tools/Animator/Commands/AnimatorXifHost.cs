using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Xanadu.Model.Animation;
using Xanadu.Core.Items;
using Xanadu.Core.Maths;
using Xanadu.Core.Storage;
using Xanadu.Model.Levels;
using Xanadu.Tools.Animator.Ecs;
using Xanadu.Tools.Animator.Ui;
using Xanadu.Ui.Ecs.Ui;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Hosts the shared XIF editor in the animator: it edits the node of the
/// selected part, chain or cloth (its polygons and shader), records edits on the animator's
/// undo history, and offers the common shaders, since a rig belongs to no map
/// with shader slots. This is how parts are drawn. A part is edited where it
/// is, on its bone in the preview: the editor goes see-through and its field
/// lines sit on the part, and the preview still zooms and pans under it. Chain and
/// cloth art and skinned parts have no one place, so they edit in the shape box.
/// Either way the preview draws the item through the real drawer and its
/// shader; the editor only draws the lines and points over it.
/// </summary>
public sealed class AnimatorXifHost(EntityStore store) : IXifEditorHost
{
    private static readonly IReadOnlyList<XifShaderChoice> Choices = BuildChoices();
    private static readonly IReadOnlyList<XifShaderChoice> LayerChoices = BuildLayerChoices();

    private Entity Session => AnimatorSession.Find(store);

    /// <summary>
    /// What is being drawn: the selected chain's or cloth's node, or the selected part's
    /// node, or, while the part shows an alternate at the playhead, that
    /// alternate (a stand-in node sharing the alternate's geometry, so edits
    /// land in it). A part that only streams particles has no shape; the
    /// particle editor shows for it instead.
    /// </summary>
    private (Xif Item, string Title)? Target
    {
        get
        {
            Entity session = Session;
            if (session.IsNull || AnimatorPreviewModeCommands.IsOn(session))
                return null;

            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            RigPackage package = document.Package;
            int chain = session.GetComponent<AnimatorSelection>().Chain;
            if (chain >= 0 && chain < package.Chains.Count)
                return (package.Chains[chain].Node, $"{package.Chains[chain].Id}  chain art");
            int cloth = AnimatorClothCommands.Selected(session);
            if (cloth >= 0 && cloth < package.Cloths.Count)
                return (package.Cloths[cloth].Node, $"{package.Cloths[cloth].Id}  cloth art");

            int index = AnimatorEffectCommands.Selected(session);
            if (index < 0)
                return null;
            RigAttachment part = package.Attachments[index];
            if (part.Particles is not null && part.Node.Polygons.Count == 0)
                return null;

            RigAttachmentState[] states = document.Runtime.Pose.AttachmentStates;
            int art = index < states.Length ? states[index].Art : 0;
            if (art < 1 || art >= part.ArtCount)
                return (part.Node, $"{part.Id}  art");

            XifGeometry geometry = part.Art(art);
            // An alternate draws with its part's shader, so the stand-in shares that module too.
            XifShader? shader = part.Node.Get<XifShader>();
            if (_alternate is null || !ReferenceEquals(_alternate.Get<XifGeometry>(), geometry) ||
                (shader is not null && !ReferenceEquals(_alternate.Get<XifShader>(), shader)))
            {
                _alternate = new Xif { Name = $"{part.Id} art {art}", Modules = { geometry } };
                if (shader is not null)
                    _alternate.Modules.Add(shader);
            }
            return (_alternate, $"{part.Id}  art {art}");
        }
    }

    private Xif? _alternate;

    public Xif? Item => Target?.Item;

    public string Title => Target?.Title ?? "";

    public IReadOnlyList<XifShaderChoice> Shaders => Choices;

    public IReadOnlyList<XifShaderChoice> Layers => LayerChoices;

    /// <summary>
    /// The part the XIF editor holds on its bone, if it is open on one: the
    /// preview draws it above the editor's see-through backing instead of
    /// under it, so the order is the character, the backing, then the part. -1 otherwise.
    /// </summary>
    public static int HeldOnBone(Entity session)
    {
        Entity editor = session.GetComponent<AnimatorUi>().XifEditor;
        if (!Xanadu.Ui.Systems.Ui.XifEditorSystem.IsOpen(editor) ||
            editor.GetComponent<XifEditor>().Host.Placement is null)
            return -1;
        return AnimatorEffectCommands.Selected(session);
    }

    /// <summary>
    /// The item the XIF editor holds in its shape box (chain or cloth art, a
    /// skinned part), with where the box shows it: the preview draws it there,
    /// through its shader, above the editor's backing. Null otherwise.
    /// </summary>
    public static (Xif Item, Transform2D Shown)? Boxed(Entity session)
    {
        Entity editor = session.GetComponent<AnimatorUi>().XifEditor;
        if (!Xanadu.Ui.Systems.Ui.XifEditorSystem.IsOpen(editor))
            return null;
        XifEditor state = editor.GetComponent<XifEditor>();
        if (state.Host.Placement is not null || state.Host.Item is not { } item)
            return null;
        return (item, state.Shown);
    }

    /// <summary>The preview draws every item itself, on its bone or in the shape box.</summary>
    public bool DrawsItem => true;

    /// <summary>
    /// How solid the editor's extra dimming and shape box draw while a part is
    /// edited on its bone: not at all. The editor's own see-through backing
    /// already sits between the character and the part.
    /// </summary>
    public const float OnBoneOpacity = 0f;

    public float Opacity => OnBone is null ? 1f : OnBoneOpacity;

    public Transform2D? Placement => OnBone;

    /// <summary>
    /// The selected part's place on screen, as the preview draws it: its bone's
    /// pose at the playhead under the preview's zoom and pan. Null for chain
    /// art, a skinned part (its mesh bends) or anything else with no one place.
    /// </summary>
    private Transform2D? OnBone
    {
        get
        {
            if (Target is null)
                return null;
            Entity session = Session;
            if (session.GetComponent<AnimatorSelection>().Chain >= 0 || AnimatorClothCommands.Selected(session) >= 0)
                return null;
            int index = AnimatorEffectCommands.Selected(session);
            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            RigPackage package = document.Package;
            if (index < 0 || index >= package.Attachments.Count || package.Attachments[index].Mesh is not null)
                return null;

            RigPose pose = AnimatorGeometry.PreviewPose(document, session.GetComponent<AnimatorModes>());
            Transform2D[] bones = RigRig.WorldBones(package, pose);
            Transform2D[] parts = RigRig.WorldAttachments(package, pose, bones);
            Rect2 area = AnimatorGeometry.PanelArea(session.GetComponent<AnimatorUi>().Preview);
            return AnimatorGeometry.PreviewPlacement(area, session.GetComponent<AnimatorPreviewView>()) * parts[index];
        }
    }


    public void Edit(Action change)
    {
        Entity session = Session;
        AnimatorHistoryCommands.Edit(session, change);
        Changed();
    }

    public void BeginGesture() => AnimatorHistoryCommands.BeginGesture(Session);

    public void EndGesture()
    {
        AnimatorHistoryCommands.EndGesture(Session);
        Changed();
    }

    /// <summary>Redraws the rig's art in the preview.</summary>
    public void Changed()
    {
        Entity session = Session;
        if (!session.IsNull)
            session.GetComponent<AnimatorDocument>().EffectsVersion++;
    }

    private static List<XifShaderChoice> BuildLayerChoices()
    {
        var choices = new List<XifShaderChoice>();
        foreach (string path in CommonShaders.Layers)
            choices.Add(new XifShaderChoice(Path.GetFileNameWithoutExtension(path).ToUpperInvariant(), XifShaderRef.OfCommon(path)));
        return choices;
    }

    private static List<XifShaderChoice> BuildChoices()
    {
        var choices = new List<XifShaderChoice> { new("NONE", new XifShaderRef()) };
        foreach (string path in CommonShaders.Paths)
            choices.Add(new XifShaderChoice(Path.GetFileNameWithoutExtension(path).ToUpperInvariant(), XifShaderRef.OfCommon(path)));
        return choices;
    }
}
