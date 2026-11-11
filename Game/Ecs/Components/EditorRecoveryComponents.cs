using System;
using Friflo.Engine.ECS;
using Xanadu.Levels;

namespace Xanadu.Ecs;

/// <summary>The editor loaded a level, so what is on screen is the clean state to compare against.</summary>
public struct EditorBaselineStale : ITag;

/// <summary>The editor just saved, so the saved state is now the clean one.</summary>
public struct EditorSaved : ITag;

/// <summary>An autosave newer than the level file was found and is waiting for the user's answer.</summary>
public struct EditorRecoveryOffer(string path, LevelDocument level, DateTimeOffset savedAt) : IComponent
{
    public string Path = path;
    public LevelDocument Level = level;
    public DateTimeOffset SavedAt = savedAt;
    public bool Asked;
}
