using Xanadu.Model.Animation;

namespace Xanadu.Model.Animator;

/// <summary>Reads and writes rigs (XIF files) at a path.</summary>
public interface IAnimatorFiles
{
    /// <summary>Loads the package at the path, or returns null when none can be read.</summary>
    RigPackage? Load(string path);

    void Save(string path, RigPackage package);

    /// <summary>Keeps unsaved work on <paramref name="path"/> where a crash cannot lose it.</summary>
    void SaveRecovery(string path, RigPackage package) { }

    /// <summary>The unsaved work kept for <paramref name="path"/>, or null.</summary>
    (RigPackage Package, System.DateTime Written)? LoadRecovery(string path) => null;

    /// <summary>Forgets the unsaved work kept for <paramref name="path"/>.</summary>
    void DropRecovery(string path) { }
}
