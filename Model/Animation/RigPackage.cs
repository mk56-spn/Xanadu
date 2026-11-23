using System.Collections;
using System.Collections.Generic;
using Xanadu.Core.Items;
using Xanadu.Core.Keys;
using Xanadu.Core.Particles;

namespace Xanadu.Model.Animation;

/// <summary>
/// A rig: a <see cref="Xif"/> tree whose root carries the skeleton and clips,
/// with its parts, chains and cloths as child nodes (Docs/xif-modules.md).
/// The tree is the truth; <see cref="Attachments"/>, <see cref="Chains"/> and
/// <see cref="Cloths"/> are flat views of it, depth first, rebuilt whenever a
/// node is added or removed through this class.
/// </summary>
public sealed class RigPackage
{
    private readonly RigView<RigAttachment> _attachments = new();
    private readonly RigView<RigChain> _chains = new();
    private readonly RigView<RigCloth> _cloths = new();
    private readonly List<int> _attachmentParents = [];

    public RigPackage() : this(new Xif { Name = "rig" })
    {
    }

    public RigPackage(Xif root)
    {
        Root = root;
        Refresh();
    }

    public Xif Root { get; }

    public RigSkeleton Skeleton => Root.Need<RigSkeleton>();

    /// <summary>The root's clips module: its clips and the transitions between them.</summary>
    public XifClips ClipSet => Root.Need<XifClips>();

    public List<RigClip> Clips => ClipSet.Clips;

    /// <summary>Which clips may fade into which, and how; see <see cref="RigTransitions"/>.</summary>
    public List<RigTransition> Transitions => ClipSet.Transitions;

    /// <summary>Every node bound to a bone or to the part above it, depth first: the art, streaks and particle streams.</summary>
    public RigView<RigAttachment> Attachments => _attachments;

    /// <summary>Verlet chains hanging off bones, such as hair.</summary>
    public RigView<RigChain> Chains => _chains;

    /// <summary>Simulated sheets pinned to the body, such as a dress; see <see cref="RigCloth"/>.</summary>
    public RigView<RigCloth> Cloths => _cloths;

    /// <summary>For each attachment, the index of the nearest attachment above it in the tree, or -1.</summary>
    public IReadOnlyList<int> AttachmentParents => _attachmentParents;

    /// <summary>Adds <paramref name="module"/>'s node under <paramref name="parent"/>, or the root.</summary>
    public T Add<T>(T module, Xif? parent = null) where T : RigNodeModule
    {
        Xif node = module.Node;
        if (!node.Modules.Contains(module))
            node.Modules.Add(module);
        (parent ?? Root).Children.Add(node);
        Refresh();
        return module;
    }

    /// <summary>Removes a node and everything under it.</summary>
    public bool Remove(Xif node)
    {
        Xif? parent = ParentOf(node);
        if (parent is null || !parent.Children.Remove(node))
            return false;
        Refresh();
        return true;
    }

    /// <summary>The node holding <paramref name="node"/> as a child, or null for the root or a node not in the tree.</summary>
    public Xif? ParentOf(Xif node) => FindParent(Root, node);

    /// <summary>Whether any node in the tree has this id.</summary>
    public bool HasId(string id) => Find(Root, id) is not null;

    /// <summary>The node with this id, or null.</summary>
    public Xif? FindNode(string id) => Find(Root, id);

    /// <summary><paramref name="baseId"/>, or the first of <c>baseId-2</c>, <c>baseId-3</c>... no node uses.</summary>
    public string UniqueId(string baseId)
    {
        string id = baseId;
        for (int n = 2; HasId(id); n++)
            id = $"{baseId}-{n}";
        return id;
    }

    /// <summary>Rebuilds the flat views; call after changing the tree by hand.</summary>
    public void Refresh()
    {
        _attachments.Items.Clear();
        _chains.Items.Clear();
        _cloths.Items.Clear();
        _attachmentParents.Clear();
        foreach (Xif child in Root.Children)
            Walk(child, -1);
    }

    private void Walk(Xif node, int parentAttachment)
    {
        int own = parentAttachment;
        foreach (XifModule module in node.Modules)
        {
            switch (module)
            {
                case RigAttachment attachment:
                    attachment.Node = node;
                    _attachmentParents.Add(parentAttachment);
                    own = _attachments.Items.Count;
                    _attachments.Items.Add(attachment);
                    break;
                case RigChain chain:
                    chain.Node = node;
                    _chains.Items.Add(chain);
                    break;
                case RigCloth cloth:
                    cloth.Node = node;
                    _cloths.Items.Add(cloth);
                    break;
            }
        }
        foreach (Xif child in node.Children)
            Walk(child, own);
    }

    private static Xif? FindParent(Xif from, Xif node)
    {
        foreach (Xif child in from.Children)
        {
            if (ReferenceEquals(child, node))
                return from;
            if (FindParent(child, node) is { } found)
                return found;
        }
        return null;
    }

    private static Xif? Find(Xif from, string id)
    {
        if (from.Id == id)
            return from;
        foreach (Xif child in from.Children)
        {
            if (Find(child, id) is { } found)
                return found;
        }
        return null;
    }
}

/// <summary>A read-only, flat view of the modules of one kind in a rig's tree.</summary>
public sealed class RigView<T> : IReadOnlyList<T>
{
    internal readonly List<T> Items = [];

    public T this[int index] => Items[index];

    public int Count => Items.Count;

    public int FindIndex(Predicate<T> match) => Items.FindIndex(match);

    public T? Find(Predicate<T> match) => Items.Find(match);

    public bool Exists(Predicate<T> match) => Items.Exists(match);

    public bool TrueForAll(Predicate<T> match) => Items.TrueForAll(match);

    public int IndexOf(T item) => Items.IndexOf(item);

    public List<TOut> ConvertAll<TOut>(Converter<T, TOut> convert) => Items.ConvertAll(convert);

    public List<T>.Enumerator GetEnumerator() => Items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => Items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => Items.GetEnumerator();
}

/// <summary>
/// A module that makes its node one of a rig's parts, chains or cloths. It
/// knows its node, so code holding the module reaches the node's other
/// modules and id; a module made on its own starts with a node of its own.
/// </summary>
public abstract class RigNodeModule : XifModule
{
    private Xif? _node;

    public Xif Node
    {
        get
        {
            if (_node is null)
            {
                _node = new Xif();
                _node.Modules.Add(this);
            }
            return _node;
        }
        internal set => _node = value;
    }

    /// <summary>The node's id: what clips key it by.</summary>
    public string Id
    {
        get => Node.Id;
        set => Node.Id = value;
    }
}

/// <summary>Bone hierarchy with one root and stable string IDs.</summary>
public sealed class RigSkeleton : XifModule, IKeyable
{
    /// <summary>Each bone is keyable as a part of the skeleton, by its id.</summary>
    public IReadOnlyList<KeyField> PartFields => RigChannels.BoneFields;

    public IEnumerable<string> Parts => Bones.ConvertAll(bone => bone.Id);

    public List<RigBone> Bones = [];

    public override XifModule Copy() => new RigSkeleton { Bones = Bones.ConvertAll(bone => bone.Copy()) };

    public override bool Same(XifModule other) =>
        other is RigSkeleton skeleton && RigPackageCopy.ListEqual(Bones, skeleton.Bones, RigPackageCopy.BoneEqual);
}

/// <summary>A node's clips and the transitions between them.</summary>
public sealed class XifClips : XifModule
{
    public List<RigClip> Clips = [];

    /// <summary>Which clips may fade into which, and how; see <see cref="RigTransitions"/>.</summary>
    public List<RigTransition> Transitions = [];

    public override XifModule Copy() => new XifClips
    {
        Clips = Clips.ConvertAll(RigPackageCopy.Clone),
        Transitions = Transitions.ConvertAll(rule => new RigTransition { From = rule.From, To = rule.To, Fade = rule.Fade, Curve = rule.Curve }),
    };

    public override bool Same(XifModule other) =>
        other is XifClips clips &&
        RigPackageCopy.ListEqual(Clips, clips.Clips, RigPackageCopy.ClipEqual) &&
        RigPackageCopy.ListEqual(Transitions, clips.Transitions, RigPackageCopy.TransitionEqual);
}

/// <summary>Other geometry a part can show instead of its own, such as a fist for an open hand.</summary>
public sealed class XifVariants : XifModule
{
    public List<XifGeometry> Alternates = [];

    public override XifModule Copy() => new XifVariants { Alternates = Alternates.ConvertAll(geometry => (XifGeometry)geometry.Copy()) };

    public override bool Same(XifModule other) =>
        other is XifVariants variants && RigPackageCopy.ListEqual(Alternates, variants.Alternates, (a, b) => a.Same(b));
}

public sealed class RigBone
{
    public string Id = "";
    public int Parent = -1;
    public Transform2D Rest = Transform2D.Identity;
    public int ZOrder;

    /// <summary>
    /// Lets animation translate this bone away from its rest offset. Bones are
    /// fixed length by default; the root always moves.
    /// </summary>
    public bool AllowTranslation;

    /// <summary>
    /// A bend lock for the joint at this bone's origin, such as a knee or an
    /// elbow: 0 bends freely, 1 or -1 only that way (see <see cref="RigJointLocks"/>).
    /// </summary>
    public int Bend;

    /// <summary>
    /// Whether posing keeps this bone's turn inside <see cref="LimitMin"/> to
    /// <see cref="LimitMax"/> (see <see cref="RigJointLocks"/>), so a long
    /// chain such as the spine cannot be bent into a shape a body can't make.
    /// </summary>
    public bool Limited;

    /// <summary>The least the bone may turn from its rest angle, in radians, while <see cref="Limited"/>.</summary>
    public float LimitMin = -RigJointLocks.DefaultLimit;

    /// <summary>The most the bone may turn from its rest angle, in radians, while <see cref="Limited"/>.</summary>
    public float LimitMax = RigJointLocks.DefaultLimit;

    /// <summary>
    /// Where a bone with no children ends, in its own space; zero for none. A
    /// skin needs it to know which way such a bone runs: a foot points at its
    /// toes, not on down the shin (see <see cref="RigSkinBones.Segment"/>).
    /// </summary>
    public Vector2 Tip;

    public bool CanTranslate => Parent < 0 || AllowTranslation;

    public RigBone Copy() => (RigBone)MemberwiseClone();
}

/// <summary>
/// The bind module: makes its node a part that rides a bone, or the part
/// above it when <see cref="BoneId"/> is empty. Its look is the node's
/// geometry and shader; clips key its transform, opacity, visibility, art
/// and layer by the node's id.
/// </summary>
public sealed class RigAttachment : RigNodeModule, IKeyable
{
    public IReadOnlyList<KeyField> KeyFields => RigChannels.PartFields;

    private static readonly List<XifGeometry> NoAlternates = [];

    public string BoneId = "";
    public Transform2D Transform = Transform2D.Identity;
    public int ZOrder;

    /// <summary>The colour its art is multiplied by; white changes nothing.</summary>
    public Color SelfModulate = Colors.White;

    /// <summary>Hidden until a clip keys it visible, as a streak or a flash is.</summary>
    public bool Hidden;

    /// <summary>The node's own geometry: art index 0.</summary>
    public XifGeometry Geometry => Node.Need<XifGeometry>();

    /// <summary>
    /// Other geometry the part can show instead, such as a fist for an open
    /// hand; a clip keys which one with <see cref="RigChannelProperty.Art"/>,
    /// where 0 is the part's own and 1 the first of these.
    /// </summary>
    public IReadOnlyList<XifGeometry> Alternates => Node.Get<XifVariants>()?.Alternates ?? NoAlternates;

    /// <summary>The alternates, made editable: adds the variants module if the node has none.</summary>
    public List<XifGeometry> NeedAlternates() => Node.Need<XifVariants>().Alternates;

    /// <summary>When set, the art is drawn as a mesh bent by the bones near it instead of rigidly.</summary>
    public RigMesh? Mesh
    {
        get => Node.Get<RigMesh>();
        set
        {
            Node.Modules.RemoveAll(module => module is RigMesh);
            if (value is not null)
                Node.Modules.Add(value);
        }
    }

    /// <summary>The particles the part streams while it shows, its node's own settings; null for none.</summary>
    public XpfParticle? Particles
    {
        get => Node.Get<XifParticles>()?.Settings;
        set
        {
            Node.Modules.RemoveAll(module => module is XifParticles);
            if (value is not null)
                Node.Modules.Add(new XifParticles { Settings = value });
        }
    }

    public int ArtCount => 1 + Alternates.Count;

    /// <summary>The geometry of an art index: the part's own for 0 or one out of range.</summary>
    public XifGeometry Art(int index) =>
        index >= 1 && index <= Alternates.Count ? Alternates[index - 1] : Node.Get<XifGeometry>() ?? new XifGeometry();

    public override XifModule Copy() => new RigAttachment
    {
        BoneId = BoneId, Transform = Transform, ZOrder = ZOrder, SelfModulate = SelfModulate, Hidden = Hidden,
    };

    public override bool Same(XifModule other) =>
        other is RigAttachment b && BoneId == b.BoneId && Transform == b.Transform && ZOrder == b.ZOrder &&
        SelfModulate == b.SelfModulate && Hidden == b.Hidden;
}

/// <summary>
/// A Verlet chain hanging off a bone, for hair, tails and cloth edges: the
/// chain module generates its node's segment bones from a count and a length.
/// The node's geometry is the art, drawn once per segment or as one sheet.
/// The points are simulated where the rig is shown.
/// </summary>
public sealed class RigChain : RigNodeModule
{
    public const int MinSegments = 1;
    public const int MaxSegments = 64;
    public const float MinSegmentLength = 1f;
    public const float MaxSegmentLength = 500f;

    /// <summary>The bone the first point is pinned to.</summary>
    public string BoneId = "";

    /// <summary>Where the first point sits, in the bone's own space.</summary>
    public Vector2 Offset;

    public int Segments = 6;
    public float SegmentLength = 20f;

    /// <summary>Downward pull in pixels per second squared.</summary>
    public float Gravity = 900f;

    /// <summary>Share of velocity kept each step; 1 never slows down, 0 stops dead.</summary>
    public float Damping = 0.96f;

    /// <summary>Width of the art at the first segment; it tapers towards the tip.</summary>
    public float Width = 10f;

    /// <summary>
    /// Draws the chain as one sheet bent along a smooth curve through its
    /// points, its art stretched root to tip, rather than a piece per segment:
    /// a strand of hair rather than a string of beads.
    /// </summary>
    public bool Sheet;

    /// <summary>
    /// The draw layer of the chain's art, or null for just above the whole body
    /// (see <see cref="RigChainArt.Layer"/>).
    /// </summary>
    public int? Layer;

    /// <summary>Sideways pull in pixels per second squared, positive towards the way the character faces.</summary>
    public float Lean;

    /// <summary>
    /// A circle on a bone that the chain cannot enter, so it rests on it instead
    /// of passing through. Radius 0 means no collider.
    /// </summary>
    public string CollideBoneId = "";
    public Vector2 CollideOffset;
    public float CollideRadius;

    /// <summary>Whether the node has any polygons to draw along the chain.</summary>
    public bool HasArt => Node.Polygons.Count > 0;

    public bool HasCollider => CollideRadius > 0f && CollideBoneId.Length > 0;

    public override XifModule Copy() => (RigChain)MemberwiseCloneWithoutNode();

    public override bool Same(XifModule other) =>
        other is RigChain b && BoneId == b.BoneId && Offset == b.Offset && Segments == b.Segments &&
        SegmentLength == b.SegmentLength && Gravity == b.Gravity && Damping == b.Damping && Width == b.Width &&
        Sheet == b.Sheet && Layer == b.Layer && Lean == b.Lean && CollideBoneId == b.CollideBoneId &&
        CollideOffset == b.CollideOffset && CollideRadius == b.CollideRadius;

    private RigChain MemberwiseCloneWithoutNode()
    {
        var copy = (RigChain)MemberwiseClone();
        copy.Node = null!;
        return copy;
    }
}

/// <summary>A rig's clip: keys on its own clock (<see cref="KeyClip"/>) plus what only a rig has.</summary>
public sealed class RigClip : KeyClip
{
    /// <summary>Named moments the game is told about as playback passes them; see <see cref="RigEvent"/>.</summary>
    public List<RigEvent> Events = [];

    /// <summary>Paths limb ends follow, baked into this clip's channels; see <see cref="RigLimbTarget"/>.</summary>
    public List<RigLimbTarget> Targets = [];
}

public enum RigChannelTarget
{
    Bone,

    /// <summary>A part (a node with an <see cref="RigAttachment"/>), by node id.</summary>
    Attachment,
}

/// <summary>Individual scalar transform channel; allows independent curves.</summary>
public enum RigChannelProperty
{
    TranslationX,
    TranslationY,
    Rotation,
    ScaleX,
    ScaleY,

    /// <summary>A part's opacity, 0 to 1.</summary>
    Opacity,

    /// <summary>
    /// Whether a part shows: 1 from a key on, 0 from a key off. Always held,
    /// never blended.
    /// </summary>
    Visible,

    /// <summary>Which of a part's art it shows, 0 for its own; held.</summary>
    Art,

    /// <summary>A part's draw layer from the key on, in place of its own; held.</summary>
    Layer,

    /// <summary>
    /// How far a bone's joint sits out of the screen's plane, in rig units;
    /// the segments either side of it foreshorten to match. Only bones have
    /// it (see <see cref="RigTransform.Depth"/>).
    /// </summary>
    Depth,
}


/// <summary>
/// A rig's view of a <see cref="KeyChannel"/>: a bone is keyed as a part of the
/// rig's own skeleton (node "", part = bone id), a part as a node of its own
/// (node = part id). Field names are the rig's fields, <see cref="RigChannelProperty"/>.
/// </summary>
public static class RigChannels
{
    private static readonly string[] Names =
        ["x", "y", "rotation", "scale_x", "scale_y", "opacity", "visible", "art", "layer", "depth"];

    private static readonly Dictionary<string, RigChannelProperty> ByName = BuildByName();

    private static Dictionary<string, RigChannelProperty> BuildByName()
    {
        var byName = new Dictionary<string, RigChannelProperty>(StringComparer.Ordinal);
        for (int i = 0; i < Names.Length; i++)
            byName[Names[i]] = (RigChannelProperty)i;
        return byName;
    }

    /// <summary>The field name a rig property is stored under.</summary>
    public static string Name(RigChannelProperty property) => Names[(int)property];

    /// <summary>The rig property a field name stands for; false for a field a rig does not have.</summary>
    public static bool TryProperty(string field, out RigChannelProperty property) => ByName.TryGetValue(field, out property);

    public static KeyAddress At(RigChannelTarget target, string id, RigChannelProperty property) =>
        target == RigChannelTarget.Bone ? new KeyAddress("", id, Name(property)) : new KeyAddress(id, "", Name(property));

    public static KeyChannel New(RigChannelTarget target, string id, RigChannelProperty property) =>
        new() { Address = At(target, id, property) };

    /// <summary>The bone's fields, as the skeleton declares them for each bone, in the order a timeline lists them.</summary>
    public static readonly KeyField[] BoneFields =
    [
        new("x", KeyValueKind.Number, KeyCombine.Add, "pos x"), new("y", KeyValueKind.Number, KeyCombine.Add, "pos y"),
        new("rotation", KeyValueKind.Angle, KeyCombine.Add, "rot"),
        new("scale_x", KeyValueKind.Number, KeyCombine.Multiply, "scl x"), new("scale_y", KeyValueKind.Number, KeyCombine.Multiply, "scl y"),
        new("depth", KeyValueKind.Number, KeyCombine.Add, "depth"),
    ];

    /// <summary>A part's fields, as its attachment module declares them, in the order a timeline lists them.</summary>
    public static readonly KeyField[] PartFields =
    [
        new("x", KeyValueKind.Number, KeyCombine.Add, "pos x"), new("y", KeyValueKind.Number, KeyCombine.Add, "pos y"),
        new("rotation", KeyValueKind.Angle, KeyCombine.Add, "rot"),
        new("scale_x", KeyValueKind.Number, KeyCombine.Multiply, "scl x"), new("scale_y", KeyValueKind.Number, KeyCombine.Multiply, "scl y"),
        new("visible", KeyValueKind.Switch, KeyCombine.Replace, "show"),
        new("opacity", KeyValueKind.Number, KeyCombine.Multiply, "alpha"),
        new("art", KeyValueKind.Index, KeyCombine.Replace, "art"), new("layer", KeyValueKind.Index, KeyCombine.Replace, "layer"),
    ];

    /// <summary>The fields a bone or a part declares.</summary>
    public static IReadOnlyList<KeyField> FieldsOf(RigChannelTarget target) =>
        target == RigChannelTarget.Bone ? BoneFields : PartFields;

    /// <summary>Where a rig track's keys live: a bone is a part of the rig itself, a part a node of its own.</summary>
    public static KeyTarget TargetOf(RigChannelTarget target, string id) =>
        target == RigChannelTarget.Bone ? new KeyTarget("", id) : new KeyTarget(id, "");

    extension(KeyChannel channel)
    {
        /// <summary>Whether it keys a bone or a part.</summary>
        public RigChannelTarget Target =>
            channel.Address.Part.Length > 0 ? RigChannelTarget.Bone : RigChannelTarget.Attachment;

        /// <summary>The bone's or part's id; setting it keeps the kind and the field (a rename).</summary>
        public string TargetId
        {
            get => channel.Address.Part.Length > 0 ? channel.Address.Part : channel.Address.Node;
            set => channel.Address = channel.Address.Part.Length > 0
                ? channel.Address with { Part = value }
                : channel.Address with { Node = value };
        }

        /// <summary>The rig field it keys; a field a rig does not have reads as X, so check <see cref="IsRigField"/> first where that matters.</summary>
        public RigChannelProperty Property =>
            ByName.TryGetValue(channel.Address.Field, out RigChannelProperty property) ? property : RigChannelProperty.TranslationX;

        public bool IsRigField => ByName.ContainsKey(channel.Address.Field);
    }
}

/// <summary>Thrown when a rig is structurally invalid.</summary>
public sealed class RigFormatException(string message) : Exception(message);
