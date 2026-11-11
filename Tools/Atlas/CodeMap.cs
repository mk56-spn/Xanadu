using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Xanadu.Tools.Atlas;

/// <summary>What a type is, for its colour and its family.</summary>
internal enum NodeKind
{
    Component,
    Tag,
    System,
    Group,
    Interface,
    Struct,
    Class,
    Enum,
}

/// <summary>How one type touches another.</summary>
internal enum LinkKind
{
    /// <summary>A system's query asks for the component (its <c>QuerySystem&lt;…&gt;</c> arguments).</summary>
    Queries,
    Implements,
    Inherits,

    /// <summary>Anything else: a field, a signature, or code that names it (a call, a <c>GetComponent&lt;T&gt;</c>, a <c>new</c>).</summary>
    Uses,
}

internal sealed record CodeNode(int Id, string Name, string FullName, string Namespace, string Assembly)
{
    public NodeKind Kind { get; set; } = NodeKind.Class;

    /// <summary>Links from this type, by kind, to node ids.</summary>
    public readonly Dictionary<LinkKind, SortedSet<int>> Out = new();

    /// <summary>Links to this type, by kind, from node ids.</summary>
    public readonly Dictionary<LinkKind, SortedSet<int>> In = new();
}

/// <summary>
/// The project's types and how they touch each other, read from the built
/// assemblies' metadata (no loading, so nothing runs): what each type
/// inherits and implements, the components each system queries, and every
/// project type a type names in its fields, signatures or method bodies.
/// Compiler-made types (lambdas, iterators) count as the type they were
/// written in. Only the project's own types are nodes.
/// </summary>
internal sealed class CodeMap
{
    private static readonly Dictionary<short, OperandType> Operands = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(op => op.Value, op => op.OperandType);

    private readonly Dictionary<string, int> _byFullName = new(StringComparer.Ordinal);

    public List<CodeNode> Nodes { get; } = new();

    /// <summary>Each assembly read and when it was built.</summary>
    public List<(string Name, DateTime Built)> Assemblies { get; } = new();

    /// <summary>The project assemblies each one references directly.</summary>
    private readonly Dictionary<string, List<string>> _references = new(StringComparer.Ordinal);

    /// <summary>Friflo's own component types (UniqueEntity, EntityName, Position, Rotation, Scale3, Transform, TreeNode, Unresolved).</summary>
    public const int FrifloComponents = 8;

    /// <summary>Friflo's own tag (Disabled).</summary>
    public const int FrifloTags = 1;

    /// <summary>
    /// Friflo's real limits, measured on 4.0.0-preview.2: a component type with index 256 makes
    /// CreateEntity throw, and a tag past 255 silently shares a bit with another (queries go wrong).
    /// Index 0 is no type in both, so 255 of each, Friflo's own included.
    /// </summary>
    public const int MaxComponents = 255, MaxTags = 255;

    /// <summary>
    /// The heaviest process for the ECS: the assembly whose own plus referenced project
    /// assemblies hold the most component and tag types (Friflo counts every one an exe loads),
    /// with Friflo's own counted in.
    /// </summary>
    public (string Assembly, int Components, int Tags) Heaviest()
    {
        (string, int, int) heaviest = ("", 0, 0);
        foreach (string assembly in _references.Keys)
        {
            var closure = new HashSet<string>(StringComparer.Ordinal);
            var stack = new Stack<string>([assembly]);
            while (stack.TryPop(out string? next))
            {
                if (closure.Add(next) && _references.TryGetValue(next, out var refs))
                    refs.ForEach(stack.Push);
            }

            int components = FrifloComponents + Nodes.Count(n => n.Kind == NodeKind.Component && closure.Contains(n.Assembly));
            int tags = FrifloTags + Nodes.Count(n => n.Kind == NodeKind.Tag && closure.Contains(n.Assembly));
            if (components + tags > heaviest.Item2 + heaviest.Item3)
                heaviest = (assembly, components, tags);
        }

        return heaviest;
    }

    /// <summary>The newest build of each project assembly under the repository (tests left out), read into one map.</summary>
    public static CodeMap Read(string root)
    {
        var newest = new Dictionary<string, FileInfo>(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "Xanadu*.dll", SearchOption.AllDirectories))
        {
            if (!path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || path.Contains("Tools copy", StringComparison.Ordinal) || Path.GetFileName(path).Contains("Tests", StringComparison.Ordinal))
                continue;
            var file = new FileInfo(path);
            if (!newest.TryGetValue(file.Name, out FileInfo? known) || file.LastWriteTimeUtc > known.LastWriteTimeUtc)
                newest[file.Name] = file;
        }

        var map = new CodeMap();
        var readers = new List<(PEReader Pe, MetadataReader Meta, string Name)>();
        foreach (FileInfo file in newest.Values.OrderBy(f => f.Name))
        {
            var pe = new PEReader(File.OpenRead(file.FullName));
            if (!pe.HasMetadata)
                continue;
            MetadataReader meta = pe.GetMetadataReader();
            string name = meta.GetString(meta.GetAssemblyDefinition().Name);
            readers.Add((pe, meta, name));
            map.Assemblies.Add((name, file.LastWriteTime));
            map._references[name] = meta.AssemblyReferences
                .Select(handle => meta.GetString(meta.GetAssemblyReference(handle).Name))
                .Where(reference => reference.StartsWith("Xanadu", StringComparison.Ordinal))
                .ToList();
        }

        // Nodes first, so links between assemblies find both ends.
        foreach (var (pe, meta, name) in readers)
            map.AddNodes(meta, name);
        foreach (var (pe, meta, _) in readers)
            map.AddLinks(pe, meta);
        map.Classify();
        foreach (var (pe, _, _) in readers)
            pe.Dispose();
        return map;
    }

    public CodeNode? Find(string name) =>
        Nodes.Find(n => n.FullName == name) ?? Nodes.Find(n => n.Name == name)
        ?? Nodes.Find(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    // ---- nodes ----------------------------------------------------------------------

    /// <summary>What a type's base type and interfaces are named, until every assembly is read and kinds can be settled.</summary>
    private readonly Dictionary<int, (string? Base, bool ValueType, bool Interface, bool Enum, List<string> Interfaces)> _shape = new();

    private void AddNodes(MetadataReader meta, string assembly)
    {
        foreach (TypeDefinitionHandle handle in meta.TypeDefinitions)
        {
            TypeDefinition type = meta.GetTypeDefinition(handle);
            string full = FullName(meta, handle);
            if (Generated(full) || full == "<Module>" || !full.StartsWith("Xanadu", StringComparison.Ordinal))
                continue;

            string ns = Namespace(meta, handle);
            string name = Plain(full[(ns.Length > 0 ? ns.Length + 1 : 0)..]);
            var node = new CodeNode(Nodes.Count, name, Plain(full), ns, assembly);
            _byFullName[full] = node.Id;
            Nodes.Add(node);

            string? baseName = type.BaseType.IsNil ? null : TypeName(meta, type.BaseType, null);
            var interfaces = new List<string>();
            foreach (InterfaceImplementationHandle implementation in type.GetInterfaceImplementations())
                interfaces.Add(TypeName(meta, meta.GetInterfaceImplementation(implementation).Interface, null));
            bool isInterface = (type.Attributes & TypeAttributes.Interface) != 0;
            _shape[node.Id] = (baseName, baseName == "System.ValueType", isInterface, baseName == "System.Enum", interfaces);
        }
    }

    /// <summary>Settles each node's kind from its base chain and interfaces, now that every assembly is in.</summary>
    private void Classify()
    {
        foreach (CodeNode node in Nodes)
        {
            var shape = _shape[node.Id];
            NodeKind kind = shape.Interface ? NodeKind.Interface : shape.Enum ? NodeKind.Enum : shape.ValueType ? NodeKind.Struct : NodeKind.Class;
            if (shape.Interfaces.Contains("Friflo.Engine.ECS.ITag"))
                kind = NodeKind.Tag;
            else if (shape.Interfaces.Contains("Friflo.Engine.ECS.IComponent"))
                kind = NodeKind.Component;
            else if (BaseChain(node.Id).Any(b => b.StartsWith("Friflo.Engine.ECS.Systems.SystemGroup", StringComparison.Ordinal)))
                kind = NodeKind.Group;
            else if (BaseChain(node.Id).Any(b => b.StartsWith("Friflo.Engine.ECS.Systems.", StringComparison.Ordinal)))
                kind = NodeKind.System;
            node.Kind = kind;
        }
    }

    private IEnumerable<string> BaseChain(int id)
    {
        for (int guard = 0; guard < 32; guard++)
        {
            string? next = _shape[id].Base;
            if (next is null)
                yield break;
            yield return next;
            if (!_byFullName.TryGetValue(next, out id))
                yield break;
        }
    }

    // ---- links ----------------------------------------------------------------------

    private void AddLinks(PEReader pe, MetadataReader meta)
    {
        foreach (TypeDefinitionHandle handle in meta.TypeDefinitions)
        {
            if (Owner(meta, handle) is not { } from)
                continue;
            TypeDefinition type = meta.GetTypeDefinition(handle);
            bool own = !Generated(FullName(meta, handle));
            var named = new HashSet<string>(StringComparer.Ordinal);

            if (own && !type.BaseType.IsNil)
            {
                var args = new List<string>();
                string baseName = TypeName(meta, type.BaseType, args);
                Link(from, baseName, LinkKind.Inherits);
                // A system's query: the components its QuerySystem<…> base takes.
                bool query = baseName.StartsWith("Friflo.Engine.ECS.Systems.QuerySystem", StringComparison.Ordinal);
                foreach (string arg in args)
                    Link(from, arg, query ? LinkKind.Queries : LinkKind.Uses);
            }

            if (own)
            {
                foreach (InterfaceImplementationHandle implementation in type.GetInterfaceImplementations())
                {
                    var args = new List<string>();
                    Link(from, TypeName(meta, meta.GetInterfaceImplementation(implementation).Interface, args), LinkKind.Implements);
                    named.UnionWith(args);
                }
            }

            // Decoding records every type a signature names into `named`.
            foreach (FieldDefinitionHandle field in type.GetFields())
                meta.GetFieldDefinition(field).DecodeSignature(new Names(meta, named), null);

            foreach (MethodDefinitionHandle methodHandle in type.GetMethods())
            {
                MethodDefinition method = meta.GetMethodDefinition(methodHandle);
                method.DecodeSignature(new Names(meta, named), null);
                if (method.RelativeVirtualAddress != 0)
                    ScanBody(meta, pe.GetMethodBody(method.RelativeVirtualAddress), named);
            }

            foreach (string name in named)
                Link(from, name, LinkKind.Uses);
        }
    }

    /// <summary>Every type a method body names: what it creates, calls, reads, casts to or asks for (GetComponent&lt;T&gt;).</summary>
    private static void ScanBody(MetadataReader meta, MethodBodyBlock body, HashSet<string> named)
    {
        BlobReader il = body.GetILReader();
        var names = new Names(meta, named);
        if (!body.LocalSignature.IsNil)
        {
            StandaloneSignature locals = meta.GetStandaloneSignature(body.LocalSignature);
            if (locals.GetKind() == StandaloneSignatureKind.LocalVariables)
                locals.DecodeLocalSignature(names, null);
        }

        while (il.RemainingBytes > 0)
        {
            short code = il.ReadByte();
            if (code == 0xFE)
                code = (short)(0xFE00 | il.ReadByte());
            if (!Operands.TryGetValue(code, out OperandType operand))
                return;
            switch (operand)
            {
                case OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok:
                    Token(meta, MetadataTokens.EntityHandle(il.ReadInt32()), names, named);
                    break;
                case OperandType.InlineSwitch:
                    // The count first, then a target per case; read apart, since `Offset +=` would take Offset before the read moves it.
                    int cases = il.ReadInt32();
                    il.Offset += cases * 4;
                    break;
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar:
                    il.Offset += 1;
                    break;
                case OperandType.InlineVar:
                    il.Offset += 2;
                    break;
                case OperandType.InlineI8 or OperandType.InlineR:
                    il.Offset += 8;
                    break;
                default:
                    il.Offset += 4;
                    break;
            }
        }
    }

    private static void Token(MetadataReader meta, EntityHandle handle, Names names, HashSet<string> named)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification:
                named.Add(TypeName(meta, handle, named));
                break;
            case HandleKind.MethodDefinition:
                named.Add(FullName(meta, meta.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType()));
                break;
            case HandleKind.FieldDefinition:
                named.Add(FullName(meta, meta.GetFieldDefinition((FieldDefinitionHandle)handle).GetDeclaringType()));
                break;
            case HandleKind.MemberReference:
                MemberReference member = meta.GetMemberReference((MemberReferenceHandle)handle);
                if (member.Parent.Kind is HandleKind.TypeDefinition or HandleKind.TypeReference or HandleKind.TypeSpecification)
                    named.Add(TypeName(meta, member.Parent, named));
                break;
            case HandleKind.MethodSpecification:
                MethodSpecification spec = meta.GetMethodSpecification((MethodSpecificationHandle)handle);
                // The generic arguments: GetComponent<TextEditor> names TextEditor.
                foreach (string arg in spec.DecodeSignature(names, null))
                    named.Add(arg);
                Token(meta, spec.Method, names, named);
                break;
        }
    }

    private void Link(int from, string to, LinkKind kind)
    {
        if (!_byFullName.TryGetValue(to, out int target) || target == from)
            return;
        CodeNode a = Nodes[from], b = Nodes[target];
        // A stronger link already there (a query, an interface, a base) says it; "uses" would repeat it.
        if (kind == LinkKind.Uses && a.Out.Any(pair => pair.Key != LinkKind.Uses && pair.Value.Contains(target)))
            return;
        if (kind != LinkKind.Uses && a.Out.TryGetValue(LinkKind.Uses, out var uses) && uses.Remove(target))
            b.In[LinkKind.Uses].Remove(from);
        Add(a.Out, kind, target);
        Add(b.In, kind, from);
    }

    private static void Add(Dictionary<LinkKind, SortedSet<int>> links, LinkKind kind, int id)
    {
        if (!links.TryGetValue(kind, out var set))
            links[kind] = set = new SortedSet<int>();
        set.Add(id);
    }

    // ---- names ----------------------------------------------------------------------

    /// <summary>The project type a type definition counts as: itself, or for a compiler-made one the type it was written in.</summary>
    private int? Owner(MetadataReader meta, TypeDefinitionHandle handle)
    {
        TypeDefinitionHandle at = handle;
        for (int guard = 0; guard < 16; guard++)
        {
            string full = FullName(meta, at);
            if (!Generated(full) && _byFullName.TryGetValue(full, out int id))
                return id;
            TypeDefinitionHandle outer = meta.GetTypeDefinition(at).GetDeclaringType();
            if (outer.IsNil)
                return null;
            at = outer;
        }

        return null;
    }

    /// <summary>A compiler-made type: a closure, an iterator, an anonymous type.</summary>
    private static bool Generated(string full) => full.Contains('<');

    /// <summary>Drops generic arity (<c>QuerySystem`2</c> → <c>QuerySystem</c>) and nesting marks.</summary>
    private static string Plain(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"`\d+", "").Replace('/', '.');

    private static string Namespace(MetadataReader meta, TypeDefinitionHandle handle)
    {
        TypeDefinition type = meta.GetTypeDefinition(handle);
        TypeDefinitionHandle outer = type.GetDeclaringType();
        return outer.IsNil ? meta.GetString(type.Namespace) : Namespace(meta, outer);
    }

    /// <summary>Namespace.Outer/Inner`n, the key nodes are found by.</summary>
    internal static string FullName(MetadataReader meta, TypeDefinitionHandle handle)
    {
        TypeDefinition type = meta.GetTypeDefinition(handle);
        TypeDefinitionHandle outer = type.GetDeclaringType();
        string name = meta.GetString(type.Name);
        if (!outer.IsNil)
            return FullName(meta, outer) + "/" + name;
        string ns = meta.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    private static string FullName(MetadataReader meta, TypeReferenceHandle handle)
    {
        TypeReference type = meta.GetTypeReference(handle);
        string name = meta.GetString(type.Name);
        if (type.ResolutionScope.Kind == HandleKind.TypeReference)
            return FullName(meta, (TypeReferenceHandle)type.ResolutionScope) + "/" + name;
        string ns = meta.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    /// <summary>A type handle's full name; a generic instance gives its generic type's, its arguments added to <paramref name="args"/>.</summary>
    internal static string TypeName(MetadataReader meta, EntityHandle handle, ICollection<string>? args)
    {
        switch (handle.Kind)
        {
            case HandleKind.TypeDefinition:
                return FullName(meta, (TypeDefinitionHandle)handle);
            case HandleKind.TypeReference:
                return FullName(meta, (TypeReferenceHandle)handle);
            case HandleKind.TypeSpecification:
                var names = new Names(meta, args ?? new List<string>()) { Arguments = args };
                return meta.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(names, null);
            default:
                return "";
        }
    }

    /// <summary>
    /// Decodes signatures into full type names, adding every type it meets
    /// (generic arguments, array elements, pointers) to a set as it goes.
    /// </summary>
    private sealed class Names(MetadataReader meta, ICollection<string> seen) : ISignatureTypeProvider<string, object?>
    {
        /// <summary>Where a generic instance's arguments go, when the caller wants them apart (a query's components).</summary>
        public ICollection<string>? Arguments { get; init; }

        private string Seen(string name)
        {
            seen.Add(name);
            return name;
        }

        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => "System." + typeCode;
        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => Seen(FullName(meta, handle));
        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => Seen(FullName(meta, handle));

        public string GetTypeFromSpecification(MetadataReader reader, object? context, TypeSpecificationHandle handle, byte rawTypeKind) =>
            meta.GetTypeSpecification(handle).DecodeSignature(this, context);

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
        {
            if (Arguments is not null)
            {
                foreach (string arg in typeArguments)
                    Arguments.Add(arg);
            }

            return genericType;
        }

        public string GetSZArrayType(string elementType) => elementType;
        public string GetArrayType(string elementType, ArrayShape shape) => elementType;
        public string GetByReferenceType(string elementType) => elementType;
        public string GetPointerType(string elementType) => elementType;
        public string GetPinnedType(string elementType) => elementType;
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
        public string GetFunctionPointerType(MethodSignature<string> signature) => "";
        public string GetGenericMethodParameter(object? context, int index) => "";
        public string GetGenericTypeParameter(object? context, int index) => "";
    }
}
