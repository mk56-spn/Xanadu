using System.Collections.Generic;
using System.IO;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Animation;
using Xanadu.Tools.Animator.Files;

namespace Xanadu.Tools.Animator.Systems;

/// <summary>
/// Redraws the rig's art when one of its SVG files is saved, so an edit in an
/// outside program shows on the moving rig straight away. Only the art reloads:
/// the clip, playhead and any unsaved rig edits stay as they are.
/// </summary>
public sealed class AnimatorSvgWatchSystem(IAnimatorSvgCatalog catalog) : QuerySystem<AnimatorDocument>
{
    public const float PollSeconds = 0.25f;

    private readonly Dictionary<string, long> _stamps = new();

    /// <summary>Files seen changing; one reloads once its time holds for a poll, so a half-written save is never read.</summary>
    private readonly Dictionary<string, long> _settling = new();
    private float _sincePoll;

    protected override void OnUpdate()
    {
        _sincePoll += Tick.deltaTime;
        if (_sincePoll < PollSeconds)
            return;
        _sincePoll = 0f;

        foreach (Entity session in Query.ToEntityList())
            Poll(session);
    }

    private void Poll(Entity session)
    {
        XafPackage package = session.GetComponent<AnimatorDocument>().Package;
        var saved = new List<string>();
        foreach ((string path, _) in Art(package))
        {
            long stamp = catalog.Stamp(path);

            // Editors may replace a file by removing it first; wait for it to come back.
            if (stamp == 0L)
                continue;
            if (!_stamps.TryGetValue(path, out long known))
            {
                _stamps[path] = stamp;
            }
            else if (stamp == known)
            {
                _settling.Remove(path);
            }
            else if (_settling.TryGetValue(path, out long settling) && settling == stamp)
            {
                _stamps[path] = stamp;
                _settling.Remove(path);
                saved.Add(path);
            }
            else
            {
                _settling[path] = stamp;
            }
        }

        if (saved.Count == 0)
            return;

        session.GetComponent<AnimatorDocument>().Version++;
        AnimatorSession.SetStatus(session, Report(package, saved));
    }

    /// <summary>What reloaded, or the one mistake that makes art vanish: the group's id changed in the editor.</summary>
    private string Report(XafPackage package, List<string> saved)
    {
        foreach (string path in saved)
        {
            IReadOnlyList<string> groups = catalog.Groups(path);
            foreach ((string file, string group) in Art(package))
            {
                if (file == path && !Contains(groups, group))
                    return $"NO GROUP ID \"{group}\"";
            }
        }
        return saved.Count == 1 ? $"RELOADED {Path.GetFileNameWithoutExtension(saved[0])}" : $"RELOADED {saved.Count} SVGS";
    }

    private static bool Contains(IReadOnlyList<string> groups, string group)
    {
        foreach (string candidate in groups)
        {
            if (candidate == group)
                return true;
        }
        return false;
    }

    private static IEnumerable<(string Path, string Group)> Art(XafPackage package)
    {
        foreach (XafAttachment attachment in package.Attachments)
        {
            for (int art = 0; art < attachment.ArtCount; art++)
            {
                XafArt source = attachment.Art(art);
                yield return (source.SvgPath, source.Group);
            }
        }
        foreach (XafChain chain in package.Chains)
        {
            if (chain.HasArt)
                yield return (chain.SvgPath, chain.Group);
        }
    }
}
