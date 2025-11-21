// Copyright (c) mk56_spn <dhsjplt@gmail.com>.Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MemoryPack;
using XanaduProject.Screens.AssetCreation.PoseAnimating.Components;

namespace XanaduProject.IO.Indexes
{
    namespace XanaduProject.IO
    {
        public abstract class AnimationIndex : BinarySerializerBaseIndex<AnimationInfo>
        {
            public static string? RenameAnimation(string oldName, string newName)
            {
                if (!ANIMATIONS.TryGetValue(oldName, out var oldInfo))
                {
                    GD.PrintErr($"AnimationIndex: '{oldName}' not found in index.");
                    return null;
                }

                string? oldFull = ProjectSettings.GlobalizePath(oldInfo.FilePath);
                if (!File.Exists(oldFull))
                {
                    GD.PrintErr($"AnimationIndex: file '{oldFull}' does not exist.");
                    return null;
                }

                string newRelative = SerializationUtils.ANIMATIONS_DIR.PathJoin(newName);
                string? newFull     = ProjectSettings.GlobalizePath(newRelative);

                if (File.Exists(newFull))
                {
                    GD.PrintErr($"AnimationIndex: destination '{newFull}' already exists.");
                    return null;
                }

                try
                {
                    File.Move(oldFull, newFull);
                }
                catch (Exception e)
                {
                    GD.PrintErr($"AnimationIndex: failed to move file – {e.Message}");
                    return null;
                }

                ANIMATIONS.Remove(oldName);
                ANIMATIONS[newName] = new AnimationInfo(newName, newRelative);
                return newRelative;
            }

            public record AnimationInfo(string Name, string FilePath);

            public static readonly Dictionary<string, AnimationInfo> ANIMATIONS = new();

            static AnimationIndex() => BuildIndex();

            public static void BuildIndex()
            {
                ANIMATIONS.Clear();

                string? animationsDir = ProjectSettings.GlobalizePath(SerializationUtils.ANIMATIONS_DIR);
                if (!Directory.Exists(animationsDir))
                {
                    GD.PrintErr($"AnimationIndex: No animations directory at '{animationsDir}'.");
                    return;
                }

                foreach (string filePath in Directory.EnumerateFiles(animationsDir, $"*{SerializationUtils.ANIMATIONS_EXT}"))
                {
                    try
                    {

                        string fileName = Path.GetFileName(filePath);
                        string relativePath = SerializationUtils.ANIMATIONS_DIR.PathJoin(fileName);

                        ANIMATIONS.Add(fileName, new AnimationInfo(fileName, relativePath));
                    }
                    catch (Exception e) // MessagePackException, IOException, etc.
                    {
                        GD.PrintErr($"AnimationIndex: skipped '{filePath}' – {e.Message}");
                    }
                }
            }

            public static List<string> GetAllAnimationNames() => ANIMATIONS.Keys.ToList();
        }
    }
}
