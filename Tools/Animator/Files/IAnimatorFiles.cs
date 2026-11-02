using Xanadu.Animation;

namespace Xanadu.Tools.Animator.Files;

/// <summary>Reads and writes XAF packages at a path.</summary>
public interface IAnimatorFiles
{
    /// <summary>Loads the package at the path, or returns null when none can be read.</summary>
    XafPackage? Load(string path);

    void Save(string path, XafPackage package);

    /// <summary>Asks the system to open the file in its own app; false when it could not.</summary>
    bool OpenInSystem(string path);
}
