using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using Xanadu.Core.Maths;
using Xanadu.Host.Text;
using Xanadu.Slang;
using Buffer = MoonWorks.Graphics.Buffer;
using Mw = MoonWorks.Graphics;

namespace Xanadu.Labs.Experiments;

/// <summary>
/// The CJK grid kept on the GPU (Shaders/cjk_grid_lab.slang). Two buffers stay
/// there between frames: the glyph table, one entry per character (its atlas
/// rect and its box in a cell), written once when a character is first used;
/// and the cells, one uint each, the character it shows. A frame with nothing
/// changed uploads nothing and draws the grid in one instanced call per atlas page. A changed
/// cell is an 8-byte patch a compute pass writes into the cell buffer.
/// </summary>
internal sealed class CjkGridGpu : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Glyph
    {
        public Vector4 Uv;
        public Vector4 Box;
        public Vector4 Page;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GridUniforms
    {
        public float Columns, Cell, Count, Page;
        public float Width, Height, OriginX, OriginY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PatchUniforms
    {
        public float Count, Pad0, Pad1, Pad2;
    }

    public const int MaxCells = 20_000;

    /// <summary>More patches than this in a frame and the whole cell buffer goes up instead.</summary>
    private const int MaxPatches = 8192;

    private const uint Group = 64;

    private readonly Mw.GraphicsDevice _device;
    private readonly HostText _text;
    private readonly int _characters;
    private readonly int _first;
    private readonly Buffer _cells, _glyphs, _patches;
    private readonly TransferBuffer _cellUpload, _glyphUpload, _patchUpload;
    private readonly ComputePipeline _apply;
    private readonly Shader _vertex, _fragment;
    private readonly GraphicsPipeline _draw;
    private readonly Sampler _sampler;
    private readonly List<HostText.Placed> _placed = [];
    private readonly List<(uint Cell, uint Character)> _pending = [];

    private readonly uint[] _mirror = new uint[MaxCells];
    private readonly Glyph[] _table;
    private GridUniforms _grid;
    private int _size;
    private float _placedCell;
    private int _placedUpTo, _uploadedUpTo;
    private bool _uploadAllCells;

    /// <summary>Bytes the last frame sent to the GPU, and how many patches that was.</summary>
    public int LastBytes { get; private set; }

    public int LastPatches { get; private set; }

    public CjkGridGpu(Mw.GraphicsDevice device, HostText text, string module, Mw.TextureFormat format, int first, int characters)
    {
        _device = device;
        _text = text;
        _first = first;
        _characters = characters;
        _table = new Glyph[characters];

        _cells = Buffer.Create<uint>(device, "cjk cells",
            Mw.BufferUsageFlags.ComputeStorageWrite | Mw.BufferUsageFlags.GraphicsStorageRead, MaxCells);
        _glyphs = Buffer.Create<Glyph>(device, "cjk glyphs", Mw.BufferUsageFlags.GraphicsStorageRead, (uint)characters);
        _patches = Buffer.Create<ulong>(device, "cjk patches", Mw.BufferUsageFlags.ComputeStorageRead, MaxPatches);
        _cellUpload = TransferBuffer.Create<uint>(device, "cjk cells upload", Mw.TransferBufferUsage.Upload, MaxCells);
        _glyphUpload = TransferBuffer.Create<Glyph>(device, "cjk glyphs upload", Mw.TransferBufferUsage.Upload, (uint)characters);
        _patchUpload = TransferBuffer.Create<ulong>(device, "cjk patches upload", Mw.TransferBufferUsage.Upload, MaxPatches);

        _apply = ComputePipeline.Create(device, SlangShaders.Compile(module, "applyPatches", "compute"), "main",
            new Mw.ComputePipelineCreateInfo
            {
                Format = Mw.ShaderFormat.SPIRV,
                NumReadonlyStorageBuffers = 1,
                NumReadWriteStorageBuffers = 1,
                NumUniformBuffers = 1,
                ThreadCountX = Group,
                ThreadCountY = 1,
                ThreadCountZ = 1,
            });
        _vertex = Shader.Create(device, SlangShaders.Compile(module, "cellVertex", "vertex"), "main", new Mw.ShaderCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV,
            Stage = Mw.ShaderStage.Vertex,
            NumStorageBuffers = 2,
            NumUniformBuffers = 1,
        });
        _fragment = Shader.Create(device, SlangShaders.Compile(module, "cellShade", "fragment"), "main", new Mw.ShaderCreateInfo
        {
            Format = Mw.ShaderFormat.SPIRV,
            Stage = Mw.ShaderStage.Fragment,
            NumSamplers = 1,
        });
        _draw = GraphicsPipeline.Create(device, new Mw.GraphicsPipelineCreateInfo
        {
            VertexShader = _vertex,
            FragmentShader = _fragment,
            VertexInputState = Mw.VertexInputState.Empty,
            PrimitiveType = Mw.PrimitiveType.TriangleList,
            RasterizerState = Mw.RasterizerState.CCW_CullNone,
            MultisampleState = Mw.MultisampleState.None,
            DepthStencilState = Mw.DepthStencilState.Disable,
            TargetInfo = new Mw.GraphicsPipelineTargetInfo
            {
                ColorTargetDescriptions = [new Mw.ColorTargetDescription { Format = format, BlendState = Premultiplied }],
            },
        });
        _sampler = Sampler.Create(device, Mw.SamplerCreateInfo.PointClamp);
    }

    private static readonly Mw.ColorTargetBlendState Premultiplied = new()
    {
        EnableBlend = true,
        SrcColorBlendFactor = Mw.BlendFactor.One,
        DstColorBlendFactor = Mw.BlendFactor.OneMinusSrcAlpha,
        ColorBlendOp = Mw.BlendOp.Add,
        SrcAlphaBlendFactor = Mw.BlendFactor.One,
        DstAlphaBlendFactor = Mw.BlendFactor.OneMinusSrcAlpha,
        AlphaBlendOp = Mw.BlendOp.Add,
        EnableColorWriteMask = true,
        ColorWriteMask = Mw.ColorComponentFlags.RGBA,
    };

    /// <summary>A fresh grid: <paramref name="count"/> cells, cell i showing character i. A new font size places every glyph again.</summary>
    public void Layout(int count, int columns, float cell, int size, Vector2 origin, Vector2 stage)
    {
        _grid = new GridUniforms
        {
            Columns = columns, Cell = cell, Count = Math.Min(count, MaxCells),
            Width = stage.X, Height = stage.Y, OriginX = origin.X, OriginY = origin.Y,
        };
        if (size != _size || cell != _placedCell)
        {
            _size = size;
            _placedCell = cell;
            _placedUpTo = _uploadedUpTo = 0;
        }

        for (int i = 0; i < (int)_grid.Count; i++)
            _mirror[i] = (uint)(i % _characters);
        EnsureGlyphs((int)_grid.Count);
        _pending.Clear();
        _uploadAllCells = true;
    }

    /// <summary>Shows character number <paramref name="character"/> in a cell, from the next frame on.</summary>
    public void Set(int cell, int character)
    {
        uint index = (uint)(character % _characters);
        EnsureGlyphs(Math.Min(character + 1, _characters));
        _mirror[cell] = index;
        if (_pending.Count < MaxPatches)
            _pending.Add(((uint)cell, index));
        else
            _uploadAllCells = true;
    }

    /// <summary>Places characters up to <paramref name="upTo"/> in the atlas; characters are used in order, so the placed ones are always a prefix.</summary>
    private void EnsureGlyphs(int upTo)
    {
        if (upTo <= _placedUpTo)
            return;

        FontFace face = _text.Face(FontKind.Regular);
        float ascent = face.Ascent(_size);
        float top = (_grid.Cell - face.Height(_size)) / 2f;
        const float texel = 1f / TextAtlas.Size;
        for (int i = _placedUpTo; i < upTo; i++)
        {
            _text.Place(char.ConvertFromUtf32(_first + i), FontKind.Regular, _size, new Vector2(0, ascent), _placed);
            if (_placed.Count == 0)
            {
                _table[i] = default;
                continue;
            }

            HostText.Placed p = _placed[0];
            AtlasGlyph g = p.Glyph;
            _table[i] = new Glyph
            {
                Uv = new Vector4(g.X, g.Y, g.X + g.Width, g.Y + g.Height) * texel,
                Box = new Vector4((_grid.Cell - g.Width) / 2f, top + p.Y, g.Width, g.Height),
                Page = new Vector4(g.Page, 0, 0, 0),
            };
        }

        _placedUpTo = upTo;
    }

    /// <summary>Sends what changed (new glyphs, patches, or every cell after a new layout), applies the patches, and draws the grid into <paramref name="target"/> over <paramref name="background"/>.</summary>
    public void Render(Mw.CommandBuffer commands, Texture target, TextAtlas atlas, Xanadu.Core.Maths.Color background)
    {
        int bytes = 0;
        int patches = _uploadAllCells ? 0 : _pending.Count;
        int count = (int)_grid.Count;
        bool glyphs = _placedUpTo > _uploadedUpTo;

        if (glyphs || _uploadAllCells || patches > 0)
        {
            Mw.CopyPass copy = commands.BeginCopyPass();
            if (glyphs)
            {
                int from = _uploadedUpTo, n = _placedUpTo - _uploadedUpTo;
                _table.AsSpan(from, n).CopyTo(_glyphUpload.Map<Glyph>(cycle: true));
                _glyphUpload.Unmap();
                // Not cycled: the rest of the table is still in use.
                copy.UploadToBuffer<Glyph>(_glyphUpload, _glyphs, 0, (uint)from, (uint)n, cycle: false);
                _uploadedUpTo = _placedUpTo;
                bytes += n * Marshal.SizeOf<Glyph>();
            }

            if (_uploadAllCells)
            {
                _mirror.AsSpan(0, count).CopyTo(_cellUpload.Map<uint>(cycle: true));
                _cellUpload.Unmap();
                copy.UploadToBuffer<uint>(_cellUpload, _cells, 0, 0, (uint)count, cycle: false);
                bytes += count * sizeof(uint);
            }
            else if (patches > 0)
            {
                Span<ulong> span = _patchUpload.Map<ulong>(cycle: true);
                for (int i = 0; i < patches; i++)
                {
                    // The cell's latest character, so two patches to one cell in a frame agree whichever lands last.
                    uint cell = _pending[i].Cell;
                    span[i] = cell | ((ulong)_mirror[cell] << 32);
                }
                _patchUpload.Unmap();
                copy.UploadToBuffer<ulong>(_patchUpload, _patches, 0, 0, (uint)patches, cycle: true);
                bytes += patches * sizeof(ulong);
            }

            commands.EndCopyPass(copy);
        }

        if (patches > 0)
        {
            // Not cycled: the patches land on the cells already there.
            Mw.ComputePass apply = commands.BeginComputePass(new Mw.StorageBufferReadWriteBinding(_cells, cycle: false));
            apply.BindComputePipeline(_apply);
            apply.BindStorageBuffers(_patches);
            commands.PushComputeUniformData(new PatchUniforms { Count = patches });
            apply.Dispatch(((uint)patches + Group - 1) / Group, 1, 1);
            commands.EndComputePass(apply);
        }

        _pending.Clear();
        _uploadAllCells = false;
        LastBytes = bytes;
        LastPatches = patches;

        // Opaque: the canvas shows a texture as straight alpha, and the glyphs blend premultiplied over this.
        Mw.RenderPass pass = commands.BeginRenderPass(new Mw.ColorTargetInfo(target, new Mw.Color(background.R, background.G, background.B, 1f)));
        if (count > 0 && atlas.PageCount > 0)
        {
            pass.BindGraphicsPipeline(_draw);
            pass.BindVertexStorageBuffers(_cells, _glyphs);
            // One draw per atlas page: each binds its page, and cells on other pages draw nothing.
            for (int page = 0; page < atlas.PageCount; page++)
            {
                pass.BindFragmentSamplers(new Mw.TextureSamplerBinding(atlas.PageTexture(page), _sampler));
                commands.PushVertexUniformData(_grid with { Page = page });
                pass.DrawPrimitives(6, (uint)count, 0, 0);
            }
        }

        commands.EndRenderPass(pass);
    }

    public void Dispose()
    {
        _draw.Dispose();
        _vertex.Dispose();
        _fragment.Dispose();
        _apply.Dispose();
        _sampler.Dispose();
        _cells.Dispose();
        _glyphs.Dispose();
        _patches.Dispose();
        _cellUpload.Dispose();
        _glyphUpload.Dispose();
        _patchUpload.Dispose();
    }
}
