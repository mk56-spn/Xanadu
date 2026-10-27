using System;
using Godot;
using Xanadu.Animation;
using Xanadu.Ecs;

namespace Xanadu.Systems.Characters;

/// <summary>Loads character XAF packages.</summary>
public interface ICharacterRigFiles
{
    /// <summary>The package at <paramref name="path"/>, or null when missing or unreadable.</summary>
    XafPackage? Load(string path);
}

/// <summary>Reads XAF files from <c>res://</c>, <c>user://</c> or absolute paths.</summary>
public sealed class GodotCharacterRigFiles : ICharacterRigFiles
{
    public XafPackage? Load(string path)
    {
        string full = ProjectSettings.GlobalizePath(path);
        if (!System.IO.File.Exists(full))
            return null;

        try
        {
            XafPackage package = XafBinary.Load(full);
            XafAssetPaths.Rebase(package, full);
            return package;
        }
        catch (Exception exception) when (exception is XafFormatException or
                                              System.IO.IOException or
                                              UnauthorizedAccessException)
        {
            return null;
        }
    }
}

/// <summary>Which XAF package each character family is drawn with.</summary>
public static class CharacterRigs
{
    public const string ContactPath = "res://Tools/Animator/Animations/Characters/contact.xaf";

    /// <summary>The rig for a character, or null when it has none yet.</summary>
    public static string? PathFor(CharacterKind kind) => kind switch
    {
        CharacterKind.Contact => ContactPath,
        _ => null,
    };
}
