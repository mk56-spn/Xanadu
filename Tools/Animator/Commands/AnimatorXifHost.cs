using System;
using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Xanadu.Animation;
using Xanadu.Ecs;
using Xanadu.Items;
using Xanadu.Levels;

namespace Xanadu.Tools.Animator.Commands;

/// <summary>
/// Hosts the shared XIF editor in the animator: it edits the item of the
/// selected effect, records edits on the animator's undo history, and offers
/// the common shaders, since a rig belongs to no map with shader slots.
/// </summary>
public sealed class AnimatorXifHost(EntityStore store) : IXifEditorHost
{
    private static readonly IReadOnlyList<XifShaderChoice> Choices = BuildChoices();

    private Entity Session => AnimatorSession.Find(store);

    private XafEffect? Effect
    {
        get
        {
            Entity session = Session;
            if (session.IsNull || AnimatorPreviewModeCommands.IsOn(session))
                return null;

            int index = AnimatorEffectCommands.Selected(session);
            return index < 0 ? null : session.GetComponent<AnimatorDocument>().Package.Effects[index];
        }
    }

    public Xif? Item => Effect?.Item;

    public string Title => Effect is { } effect ? $"{effect.Id}  item" : "";

    public IReadOnlyList<XifShaderChoice> Shaders => Choices;

    public bool CanLoad(string path) => Godot.ResourceLoader.Exists(path);

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

    /// <summary>Redraws the effects in the preview; the rest of the rig is untouched.</summary>
    public void Changed()
    {
        Entity session = Session;
        if (!session.IsNull)
            session.GetComponent<AnimatorDocument>().EffectsVersion++;
    }

    private static List<XifShaderChoice> BuildChoices()
    {
        var choices = new List<XifShaderChoice> { new("NONE", new XifShaderRef()) };
        foreach (string path in CommonShaders.Paths)
            choices.Add(new XifShaderChoice(Path.GetFileNameWithoutExtension(path).ToUpperInvariant(), XifShaderRef.OfCommon(path)));
        return choices;
    }
}
