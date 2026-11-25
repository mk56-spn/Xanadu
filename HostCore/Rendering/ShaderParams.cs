using System.Collections.Generic;
using System.Numerics;
using MoonWorks.Graphics;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Rendering;

/// <summary>Which stage a values block goes to.</summary>
internal enum ParamsStage { Fragment, Vertex, Compute }

/// <summary>An effect's values packed into uniform buffer 1: its defaults, then the material's, then the item's own.</summary>
internal static class ShaderParams
{
    public static unsafe void Push(CommandBuffer commands, CompiledShader compiled, CanvasMaterial? material,
        IReadOnlyDictionary<string, Vector4>? instance, ParamsStage stage = ParamsStage.Fragment)
    {
        Span<byte> bytes = stackalloc byte[compiled.ParamsSize];
        bytes.Clear();
        foreach (ShaderParam param in compiled.Params)
        {
            if (param.IsMatrix)
            {
                PackMatrix(bytes, param, material);
                continue;
            }

            Vector4 value = param.Default;
            if (material?.Values.TryGetValue(param.Name, out Vector4 set) == true)
                value = set;
            if (instance?.TryGetValue(param.Name, out Vector4 own) == true)
                value = own;

            Span<float> floats = [value.X, value.Y, value.Z, value.W];
            for (int i = 0; i < Math.Min(4, param.Size / 4); i++)
                Write(bytes.Slice(param.Offset + i * 4, 4), floats[i], param.IsInt);
        }

        fixed (byte* data = bytes)
        {
            switch (stage)
            {
                case ParamsStage.Compute:
                    commands.PushComputeUniformData(data, (uint)bytes.Length, 1);
                    break;
                case ParamsStage.Vertex:
                    commands.PushVertexUniformData(data, (uint)bytes.Length, 1);
                    break;
                default:
                    commands.PushFragmentUniformData(data, (uint)bytes.Length, 1);
                    break;
            }
        }
    }

    /// <summary>A matrix value, row by row, each row 16 bytes on from the last as Slang lays it out; the identity when the material sets none.</summary>
    private static void PackMatrix(Span<byte> bytes, ShaderParam param, CanvasMaterial? material)
    {
        Matrix4x4 value = material?.Matrices.TryGetValue(param.Name, out Matrix4x4 set) == true ? set : Matrix4x4.Identity;
        int rows = Math.Min(param.Rows, 4), columns = Math.Min(param.Columns, 4);
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                int at = param.Offset + row * 16 + column * 4;
                if (at + 4 <= bytes.Length)
                    Write(bytes.Slice(at, 4), value[row, column], param.IsInt);
            }
        }
    }

    private static void Write(Span<byte> slot, float value, bool isInt)
    {
        if (isInt)
            BitConverter.TryWriteBytes(slot, (int)MathF.Round(value));
        else
            BitConverter.TryWriteBytes(slot, value);
    }
}
