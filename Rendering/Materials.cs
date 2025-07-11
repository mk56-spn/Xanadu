// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;
using static Godot.RenderingServer;

namespace XanaduProject.Rendering
{
    public enum BlockMaterialId
    {
        Rounded,
        Chamfer,
        Sharp,
    }

    public enum HitMaterialId
    {
        Default,
    }

    /// <summary>
    /// Defines the type of note, used to fetch a complete material set.
    /// </summary>
    public enum NoteTypeId
    {
        Default,
    }

    /// <summary>
    /// A container for the pair of materials that make up a single note.
    /// </summary>
    public readonly struct NoteMaterialSet
    {
        public Rid Receiver { get; }
        public Rid Falling { get; }

        internal NoteMaterialSet(Rid receiver, Rid falling)
        {
            Receiver = receiver;
            Falling = falling;
        }
    }

    public static class Materials
    {
        /// <summary>
        /// Generic helper to load and cache a shader material.
        /// </summary>
        private static Rid getMaterial<TEnum>(TEnum id, IReadOnlyDictionary<TEnum, string> paths, Dictionary<TEnum, ShaderMaterial> cache)
            where TEnum : Enum
        {
            if (cache.TryGetValue(id, out var mat))
                return mat.GetRid();

            if (!paths.TryGetValue(id, out string? path))
                throw new ArgumentOutOfRangeException(nameof(id),
                    $"No path registered for {typeof(TEnum).Name} '{id}'.");

            mat = new ShaderMaterial
            {
                Shader = GD.Load<Shader>(path)
                         ?? throw new InvalidOperationException(
                             $"Failed to load ShaderMaterial at '{path}' ({typeof(TEnum).Name}={id}).")
            };

            cache[id] = mat;
            return mat.GetRid();
        }


        public static class Blocks
        {
            private static readonly Dictionary<BlockMaterialId, string> paths = new()
            {
                { BlockMaterialId.Rounded, "uid://c2pe6a110d65t"},
                { BlockMaterialId.Sharp, "uid://6ek1r3nyde7v"},
                { BlockMaterialId.Chamfer, "uid://d0ymfb87gyt3w"},
            };

            private static readonly Dictionary<BlockMaterialId, ShaderMaterial> cache = new();

            /// <summary>Fetch the material for a block (loads & caches lazily).</summary>
            public static Rid Get(BlockMaterialId id) => getMaterial(id, paths, cache);
        }
        public static class Hits
        {
            private static readonly Dictionary<HitMaterialId, string> paths = new()
            {
                { HitMaterialId.Default , "uid://b08picb1q7kn"}
            };

            private static readonly Dictionary<HitMaterialId, ShaderMaterial> cache = new();

            /// <summary>Fetch the material for a block (loads & caches lazily).</summary>
            public static Rid Get(HitMaterialId id) => getMaterial(id, paths, cache);
        }

        public static class Notes
        {
            private static readonly Dictionary<NoteTypeId, string> receiver_paths = new()
            {
                // The material for the stationary "receiver" part of the note.
                { NoteTypeId.Default, "uid://bvhqkn6lx2xh2" },
            };

            private static readonly Dictionary<NoteTypeId, string> falling_paths = new()
            {
                // The material for the "falling" part of the note.
                { NoteTypeId.Default, "uid://bqtb5n365g2nl" },
            };

            private static readonly Dictionary<NoteTypeId, ShaderMaterial> receiver_cache = new();
            private static readonly Dictionary<NoteTypeId, ShaderMaterial> falling_cache = new();

            /// <summary>Fetch the material set for a note (loads & caches lazily).</summary>
            public static NoteMaterialSet Get(NoteTypeId type = NoteTypeId.Default)
            {
                var receiverMat = getMaterial(type, receiver_paths, receiver_cache);
                var fallingMat = getMaterial(type, falling_paths, falling_cache);
                return new NoteMaterialSet(receiverMat, fallingMat);
            }

            /// <summary>
            /// Fetches and assigns the appropriate receiver and falling note materials to the given canvas items.
            /// </summary>
            /// <param name="receiverCanvas">The Rid of the canvas item for the note receiver.</param>
            /// <param name="fallingNoteCanvas">The Rid of the canvas item for the falling note.</param>
            /// <param name="type">The type of note material to apply.</param>
            public static void Set(Rid receiverCanvas, Rid fallingNoteCanvas, NoteTypeId type = NoteTypeId.Default)
            {
                NoteMaterialSet materials = Get(type);
                CanvasItemSetMaterial(receiverCanvas, materials.Receiver);
                CanvasItemSetMaterial(fallingNoteCanvas, materials.Falling);
            }
        }
    }
}
