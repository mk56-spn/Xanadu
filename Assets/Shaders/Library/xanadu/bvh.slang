// xanadu.bvh: a bounding volume hierarchy rebuilt on the GPU every frame (Karras' LBVH), and
// a walk through it. A graph gives each thing a Morton code (bvh.morton), sorts the codes
// (sort.radix), writes a leaf per thing (bvh.leaf), and calls bvh.tree to link the inner nodes
// over the sorted codes and box them up from the leaves:
//
//     [Buffer(2 * MAX)] globallycoherent RWStructuredBuffer<bvh.Node> nodes;   // inner 0..n-2, leaves n-1..2n-2
//     [Buffer(2 * MAX)] RWStructuredBuffer<uint> parents;                       // each node's parent
//     [Buffer(MAX)] globallycoherent RWStructuredBuffer<uint> arrivals;         // children arrived at each inner node
//
//     void build()
//     {
//         code();                                             // keys[i] = bvh.morton(p, box); ids[i] = i
//         sort.radix(n, keys, ids, keysB, idsB, counts);
//         leaves();                                           // nodes[n - 1 + i] = bvh.leaf(box of ids[i], i, ids[i])
//         bvh.tree(n, keys, nodes, parents, arrivals);
//     }
//
// nodes and arrivals must be globallycoherent: refit reads boxes other groups wrote. To use the
// tree, walk it with a visitor (bvh.IVisit): it says how near each box is (or bvh.SKIP) and what
// to do at each leaf, and bvh.walk goes nearer child first.
module bvh;
import xanadu.stage;
import xanadu.march;

public namespace bvh
{
    /// What a node's nearness says to leave it out of a walk.
    public static const float SKIP = 1e30;
    /// The most nodes a walk keeps to come back to; deeper ones are dropped.
    public static const uint STACK = 32;
    /// A parent nothing has: the root's.
    public static const uint NONE = 0xFFFFFFFFu;

    /// A node: inner ones join two children, leaves hold one thing (its sorted place and its id).
    public struct Node
    {
        public float3 low;
        public uint left;           // inner: a child; leaf: its place in sorted order
        public float3 high;
        public uint right;          // inner: the other child; leaf: the thing's id

        /// Its box.
        public property Bounds box { get { return Bounds(low, high); } }
    };

    /// A leaf round `box`, for the thing `id` at sorted place `place`.
    public Node leaf(Bounds box, uint place, uint id)
    {
        Node node;
        node.low = box.low;
        node.high = box.high;
        node.left = place;
        node.right = id;
        return node;
    }

    /// Whether node `node` of a tree over n things is a leaf.
    public bool isLeaf(uint node, uint n) { return node >= n - 1; }

    /// Ten bits spread three apart, for a Morton code.
    public uint spread(uint x)
    {
        x &= 0x3FF;
        x = (x | (x << 16)) & 0x030000FF;
        x = (x | (x << 8)) & 0x0300F00F;
        x = (x | (x << 4)) & 0x030C30C3;
        x = (x | (x << 2)) & 0x09249249;
        return x;
    }

    /// p's 30-bit Morton code inside `box` (ten bits an axis): near points, near codes.
    public uint morton(float3 p, Bounds box)
    {
        uint3 q = uint3(saturate((p - box.low) / (box.high - box.low)) * 1023.0);
        return (spread(q.x) << 2) | (spread(q.y) << 1) | spread(q.z);
    }

    /// The common prefix of the sorted codes at places a and b; equal codes go on into the places.
    int prefix(RWStructuredBuffer<uint> keys, uint n, int a, int b)
    {
        if (b < 0 || b >= int(n))
            return -1;
        uint x = keys[a], y = keys[b];
        if (x == y)
            return 32 + (31 - int(firstbithigh(uint(a ^ b))));
        return 31 - int(firstbithigh(x ^ y));
    }

    /// A thread an inner node (Karras 2012): the range of leaves it covers and where it splits,
    /// so its two children and their parent; arrivals cleared for refit.
    [Pass(256, over: n - 1)]
    public void link(uint at, uint n, RWStructuredBuffer<uint> keys, RWStructuredBuffer<Node> nodes,
        RWStructuredBuffer<uint> parents, RWStructuredBuffer<uint> arrivals)
    {
        int i = int(at);
        int d = prefix(keys, n, i, i + 1) - prefix(keys, n, i, i - 1) >= 0 ? 1 : -1;
        int least = prefix(keys, n, i, i - d);
        int most = 2;
        while (prefix(keys, n, i, i + most * d) > least)
            most <<= 1;
        int l = 0;
        for (int t = most >> 1; t >= 1; t >>= 1)
            if (prefix(keys, n, i, i + (l + t) * d) > least)
                l += t;
        int j = i + l * d;
        int common = prefix(keys, n, i, j);

        int s = 0;
        int divider = 2;
        for (int t = (l + divider - 1) / divider; ; t = (l + divider - 1) / divider)
        {
            if (prefix(keys, n, i, i + (s + t) * d) > common)
                s += t;
            if (t == 1)
                break;
            divider <<= 1;
        }
        int split = i + s * d + min(d, 0);

        uint leaves = n - 1;
        uint left = min(i, j) == split ? leaves + uint(split) : uint(split);
        uint right = max(i, j) == split + 1 ? leaves + uint(split + 1) : uint(split + 1);
        nodes[i].left = left;
        nodes[i].right = right;
        parents[left] = uint(i);
        parents[right] = uint(i);
        arrivals[i] = 0;
        if (i == 0)
            parents[0] = NONE;
    }

    /// A thread a leaf, up the tree from it: the second child to arrive at a node joins both boxes.
    [Pass(256, over: n)]
    public void refit(uint i, uint n, RWStructuredBuffer<Node> nodes, RWStructuredBuffer<uint> parents,
        RWStructuredBuffer<uint> arrivals)
    {
        uint up = parents[n - 1 + i];
        while (up != NONE)
        {
            AllMemoryBarrier();
            uint before;
            InterlockedAdd(arrivals[up], 1, before);
            if (before == 0)
                return;
            AllMemoryBarrier();
            Node a = nodes[nodes[up].left];
            Node c = nodes[nodes[up].right];
            nodes[up].low = min(a.low, c.low);
            nodes[up].high = max(a.high, c.high);
            up = parents[up];
        }
    }

    /// The tree over n sorted codes whose leaves are written: inner nodes linked, then boxed up.
    [Schedule]
    public void tree(uint n, RWStructuredBuffer<uint> keys, RWStructuredBuffer<Node> nodes,
        RWStructuredBuffer<uint> parents, RWStructuredBuffer<uint> arrivals)
    {
        link(n, keys, nodes, parents, arrivals);
        refit(n, nodes, parents, arrivals);
    }

    // ---- walking it -------------------------------------------------------------------

    /// What a walk asks at each node: how near its box is (bvh.SKIP leaves it out), and, at a
    /// leaf, what to do there (true stops the walk).
    public interface IVisit
    {
        [mutating] public float near(Node node);
        [mutating] public bool leaf(Node node);
    };

    /// Walks the tree over n things from `root`, depth first and nearer child first: each child
    /// asked how near it is, the nearer one opened now and the other kept for later.
    public void walk<V : IVisit>(RWStructuredBuffer<Node> nodes, uint n, uint root, inout V visit)
    {
        if (visit.near(nodes[root]) >= SKIP)
            return;
        uint stack[STACK];
        uint top = 0;
        uint node = root;
        while (true)
        {
            Node here = nodes[node];
            if (isLeaf(node, n))
            {
                if (visit.leaf(here))
                    return;
            }
            else
            {
                float a = visit.near(nodes[here.left]);
                float b = visit.near(nodes[here.right]);
                bool useA = a < SKIP, useB = b < SKIP;
                if (useA && useB)
                {
                    bool aFirst = a <= b;
                    node = aFirst ? here.left : here.right;
                    if (top < STACK)
                        stack[top++] = aFirst ? here.right : here.left;
                    continue;
                }
                if (useA || useB)
                {
                    node = useA ? here.left : here.right;
                    continue;
                }
            }

            if (top == 0)
                return;
            node = stack[--top];
        }
    }
}
