using System.Diagnostics;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Xanadu.Logic.Animator.Commands;
using Xanadu.Model.Animation;
using Xanadu.Model.Animator;

namespace Xanadu.Logic.Animator;

/// <summary>
/// Keeps unsaved work safe from a crash. Every <see cref="Interval"/> seconds
/// with unsaved changes it writes them to the rig's recovery file (never the
/// rig itself); SAVE, or choosing to discard, forgets that file. On the first
/// frame, work left in a recovery file comes back as one undo step, so Ctrl+Z
/// drops it again.
/// </summary>
public sealed class AnimatorAutosaveSystem : QuerySystem<AnimatorDocument, AnimatorFile>
{
    public const double Interval = 30;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private double _lastWrite;
    private RigPackage? _written;
    private bool _restored;

    protected override void OnUpdate()
    {
        foreach (Entity session in Query.ToEntityList())
        {
            if (!_restored)
            {
                _restored = true;
                Restore(session);
            }

            AnimatorFile file = session.GetComponent<AnimatorFile>();
            AnimatorDocument document = session.GetComponent<AnimatorDocument>();
            double now = _clock.Elapsed.TotalSeconds;
            if (!file.Dirty || file.Path.Length == 0 || now - _lastWrite < Interval)
                continue;

            _lastWrite = now;
            if (_written is not null && RigPackageCopy.AreEqual(_written, document.Package))
                continue;

            _written = RigPackageCopy.Clone(document.Package);
            document.Files.SaveRecovery(file.Path, document.Package);
        }
    }

    private static void Restore(Entity session)
    {
        AnimatorFile file = session.GetComponent<AnimatorFile>();
        AnimatorDocument document = session.GetComponent<AnimatorDocument>();
        if (file.Path.Length == 0 || document.Files.LoadRecovery(file.Path) is not { } kept)
            return;

        if (RigPackageCopy.AreEqual(kept.Package, document.Package))
        {
            document.Files.DropRecovery(file.Path);
            return;
        }

        AnimatorHistoryCommands.Edit(session, () =>
        {
            session.GetComponent<AnimatorDocument>().Package = kept.Package;
            AnimatorSession.RigChanged(session);
        });
        AnimatorSession.SetStatus(session,
            $"RESTORED UNSAVED WORK FROM {kept.Written:HH:mm} · CTRL+Z DROPS IT, CTRL+S KEEPS IT");
    }
}
