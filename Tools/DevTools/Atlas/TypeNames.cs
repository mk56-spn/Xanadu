using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Xanadu.Tools.Atlas;

/// <summary>Type names as people write them, for the chart and the text dumps alike.</summary>
internal static class TypeNames
{
    /// <summary>A full type name without namespaces or arity marks, with C# keywords: <c>List&lt;float&gt;</c>.</summary>
    public static string Short(string type)
    {
        string plain = Regex.Replace(type, @"[\w.]*\.|`\d+", "").Replace('/', '.');
        return Regex.Replace(plain, @"\w+", word => Keywords.GetValueOrDefault(word.Value, word.Value));
    }

    private static readonly Dictionary<string, string> Keywords = new()
    {
        ["Single"] = "float", ["Double"] = "double", ["Int32"] = "int", ["UInt32"] = "uint", ["Int64"] = "long",
        ["UInt64"] = "ulong", ["Int16"] = "short", ["UInt16"] = "ushort", ["Byte"] = "byte", ["SByte"] = "sbyte",
        ["Boolean"] = "bool", ["String"] = "string", ["Char"] = "char", ["Object"] = "object",
    };
}
