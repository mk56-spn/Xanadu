// Copyright (c) mk56_spn <dhsjplt@gmail.com>. Licensed under the GNU General Public Licence (2.0).
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using Godot;
using static Godot.RenderingServer;

namespace XanaduProject.Stage.Masters.Rendering
{
    public enum BackgroundShaderId
    {
        Default
    }

    public enum BlockShaderId
    {
        Rounded,
        Chamfer,
        Sharp,
        Cross
    }

    public enum HitShaderId
    {
        Default
    }

    /// <summary>
    /// Defines the type of note, used to fetch a complete material set.
    /// </summary>
    public enum NoteTypeId
    {
        Default
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

    /// <summary>
    /// Generic material manager that handles loading, caching, and setup of shader materials.
    /// </summary>
    /// <typeparam name="TEnum">The enum type used as material identifiers</typeparam>
    public class MaterialManager<TEnum>(
        Dictionary<TEnum, string> paths,
        Dictionary<TEnum, Action<ShaderMaterial>>? setupActions = null)
        where TEnum : Enum
    {
        private readonly Dictionary<TEnum, Action<ShaderMaterial>> setupActions = setupActions ?? new Dictionary<TEnum, Action<ShaderMaterial>>();
        private readonly Dictionary<TEnum, ShaderMaterial> cache = new();

        /// <summary>
        /// Gets the material RID for the specified identifier (loads and caches lazily).
        /// </summary>
        public Rid Get(TEnum id)
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

            // Execute setup action if registered
            if (setupActions.TryGetValue(id, out var setupAction))
                setupAction(mat);

            cache[id] = mat;
            return mat.GetRid();
        }

        /// <summary>
        /// Sets the material on a canvas item.
        /// </summary>
        public void Set(Rid canvasItem, TEnum id)=>
            CanvasItemSetMaterial(canvasItem, Get(id));
    }

    public static class Materials
    {
        public static readonly MaterialManager<BlockShaderId> BLOCKS = new(
            new Dictionary<BlockShaderId, string>
            {
                { BlockShaderId.Rounded, "uid://c2pe6a110d65t" },
                { BlockShaderId.Sharp, "uid://6ek1r3nyde7v" },
                { BlockShaderId.Chamfer, "uid://d0ymfb87gyt3w" },
                { BlockShaderId.Cross, "uid://chwlqdpv7wivn" }
            });

        public static readonly MaterialManager<HitShaderId> HITS = new(
            new Dictionary<HitShaderId, string>
            {
                { HitShaderId.Default, "uid://b08picb1q7kn" }
            });

        public static readonly MaterialManager<BackgroundShaderId> BACKGROUNDS = new(
            new Dictionary<BackgroundShaderId, string>
            {
                { BackgroundShaderId.Default, "uid://replace_with_background_shader_uid" }
            },
            new Dictionary<BackgroundShaderId, Action<ShaderMaterial>>
            {
                { BackgroundShaderId.Default, mat => mat.SetShaderParameter("tint_color", new Color(1f, 1f, 1f)) }
            });

        public static class Notes
        {
            private static readonly MaterialManager<NoteTypeId> receivers = new(
                new Dictionary<NoteTypeId, string>
                {
                    { NoteTypeId.Default, "uid://bvhqkn6lx2xh2" }
                },
                new Dictionary<NoteTypeId, Action<ShaderMaterial>>
                {

                });

            private static readonly MaterialManager<NoteTypeId> falling = new(
                new Dictionary<NoteTypeId, string>
                {
                    { NoteTypeId.Default, "uid://bqtb5n365g2nl" }
                },
                new Dictionary<NoteTypeId, Action<ShaderMaterial>>
                {
                    { NoteTypeId.Default, mat => mat.SetShaderParameter("base_color", new Color(0.5f, 0.5f, 1.0f)) }
                });

            /// <summary>Fetch the material set for a note (loads & caches lazily).</summary>
            public static NoteMaterialSet Get(NoteTypeId type = NoteTypeId.Default) =>
                new(receivers.Get(type), falling.Get(type));

            /// <summary>
            /// Fetches and assigns the appropriate receiver and falling note materials to the given canvas items.
            /// </summary>
            public static void Set(Rid receiverCanvas, Rid fallingNoteCanvas, NoteTypeId type = NoteTypeId.Default)
            {
                var materials = Get(type);
                CanvasItemSetMaterial(receiverCanvas, materials.Receiver);
                CanvasItemSetMaterial(fallingNoteCanvas, materials.Falling);
            }
        }
    }
}
