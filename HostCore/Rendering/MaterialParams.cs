using System;
using Xanadu.Slang;
using Xanadu.Ui.Systems.Canvas;

namespace Xanadu.Host.Rendering;

/// <summary>A compiled effect's values as the UI's value fields know them.</summary>
internal static class MaterialParams
{
    /// <summary>What a value field needs of <paramref name="param"/>: its name, shape, default, options.</summary>
    public static MaterialParam Describe(ShaderParam param) => new(
        param.Name, param.IsMatrix ? param.Columns : Math.Clamp(param.Size / 4, 1, 4), param.IsInt, param.Default,
        param.IsBool, param.IsUnsigned, param.Rows, param.Fit, param.Options);
}
