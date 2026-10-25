using Writer.Animation;

namespace Writer.Tools.Animator.Files;

/// <summary>Reads and writes XAF packages at a path.</summary>
public interface IAnimatorFiles
{
    /// <summary>Loads the package at the path, or returns null when none can be read.</summary>
    XafPackage? Load(string path);

    void Save(string path, XafPackage package);
}
