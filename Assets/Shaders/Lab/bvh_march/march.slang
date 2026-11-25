// The march: a ray a pixel, a tile of 8 × 8 pixels a group. First each tile lists the blobs it can
// see: thread 0 opens the BVH breadth first into up to 64 subtrees, then every thread walks one,
// keeping the leaves inside the tile's four side planes and nearer than the floor behind them.
// The list is sorted in groupshared memory by how near any ray from the eye can enter each
// blob (bitonic). Each pixel then gathers the K blobs it enters first from that list (or walks
// the BVH itself when the list overflowed), marches only those, and gathers again past them
// when it has to. The floor and the pillars are boxes it meets on the way; soft shadows read
// the sun's grid, the pillars' are hard.
import xanadu;

[Options("lit|nodes visited|normals|shadow cells|tile lists")] uniform int view;
/// Higher is a harder shadow edge.
[Default(10)] uniform float softness;
/// Radians a second the camera turns round the galaxy.
[Default(0.08)] uniform float spin;
/// Each tile lists what it sees first; off, every pixel walks the whole BVH.
[Default(1)] uniform bool tiles;

static const uint K = 8;                    // blobs a ray marches at once: the nearest-entering
static const uint PILLARS = 10;
static const uint TILE = 8;                 // pixels a tile's side: [Pixels(8, 8)] on march
static const uint THREADS = TILE * TILE;
static const uint LISTED = 256;             // blobs a tile can list; past that its pixels walk the BVH

/// The eye, swung round the galaxy and looking down on it.
Camera eyeOn(Canvas canvas) { return Camera(21 * U, canvas.size.y * 0.96).orbited(0.6 + time * spin, 0.42); }

/// A blob's colour: by its id, drifting with its height.
colour tintOf(uint id, float y) { return lerp(colours.white, colours.hue(frac(id * 0.000137 - y / U * 0.035 + 0.55)), 0.62); }

/// The blobs one ray marches at once: the K that enter first, of those still ahead of `from`.
struct Gather : IField
{
    float4 ball[K];
    uint id[K];
    float enter[K];
    uint kept;          // how many are kept
    float cut;          // the nearest entry of anything left out
    float leave;        // the farthest exit of anything kept
    float bound;        // the nearest plain blob the ray hits: the melted surface is no further

    /// The kept blobs melted, at p.
    Sample sampled(float3 p)
    {
        Melt melt = Melt(blend());
        for (uint k = 0; k < kept; k++)
            melt.add(Ball(ball[k]), p, tintOf(id[k], ball[k].y));
        return melt.sample;
    }

    [mutating]
    void add(float4 b, uint who, Span span, Ray ray, float from)
    {
        // A melt only adds to the blobs (smooth min ≤ min), so a plain hit bounds the surface.
        Span plain = Ball(b).entered(ray);
        if (!plain.missed && plain.far > from)
            bound = min(bound, max(plain.near, from));
        if (span.near >= bound)
        {
            cut = min(cut, span.near);
            return;
        }
        if (kept == K)
        {
            if (span.near >= enter[K - 1])
            {
                cut = min(cut, span.near);
                return;
            }
            cut = min(cut, enter[K - 1]);
            kept--;
        }
        int at = int(kept);
        for (; at > 0 && enter[at - 1] > span.near; at--)
        {
            ball[at] = ball[at - 1];
            id[at] = id[at - 1];
            enter[at] = enter[at - 1];
        }
        ball[at] = b;
        id[at] = who;
        enter[at] = span.near;
        kept++;
        leave = max(leave, span.far);
    }

    /// How far entering can be and still matter: past the last kept when the list is full.
    float horizon(float limit) { return min(bound, kept == K ? min(limit, enter[K - 1]) : limit); }
};

// ---- the tile's list --------------------------------------------------------------

groupshared float4 listBalls[LISTED];
groupshared uint listIds[LISTED];
groupshared float listKeys[LISTED];         // the nearest any ray from the eye can enter each, ascending
groupshared uint listPlaces[LISTED];        // each blob's place in sorted order
groupshared uint listed;                    // how many the tile found (past LISTED: overflowed)
groupshared uint front[THREADS];            // the subtrees the tile's threads walk, a thread each
groupshared uint fronts;

/// How far the floor is along a ray, or the far limit when it never meets it.
float floorAlong(Ray ray)
{
    float t = (LIFT * U - ray.origin.y) / ray.direction.y;
    return ray.direction.y > 1e-4 && t > 0 ? min(t, 200 * U) : 200 * U;
}

/// What a tile sees: inside the four planes through the eye and its corners, nearer than the floor behind them.
Frustum tileView(Camera camera, Canvas canvas)
{
    float2 corners[4] = { group.low, float2(group.high.x, group.low.y), group.high, float2(group.low.x, group.high.y) };
    float3 dirs[4];
    Frustum v;
    v.far = 0;
    for (uint k = 0; k < 4; k++)
    {
        Ray ray = camera.ray((canvas.uvAt(corners[k]) - 0.5) * canvas.size);
        dirs[k] = ray.direction;
        v.eye = ray.origin;
        // The floor under a tile is flat, so the farthest of it is at a corner; pillars only come nearer.
        v.far = max(v.far, floorAlong(ray));
    }

    float3 centre = camera.ray((canvas.uvAt((group.low + group.high) * 0.5) - 0.5) * canvas.size).direction;
    for (uint k = 0; k < 4; k++)
    {
        float3 plane = normalize(cross(dirs[k], dirs[(k + 1) & 3]));
        v.planes[k] = dot(plane, centre) < 0 ? -plane : plane;
    }
    return v;
}

/// The tile's list: the blobs it sees, sorted by the nearest any of its rays can enter them.
/// Every thread of the tile calls it (it waits for them all).
void listTile(Frustum v)
{
    uint n = count();
    if (group.index == 0)
    {
        listed = 0;
        // Breadth first from the root while every inner node can still open into both children.
        fronts = v.sees(nodes[0].box) ? 1 : 0;
        front[0] = 0;
        bool inner = true;
        while (inner && fronts > 0 && fronts * 2 <= THREADS)
        {
            uint next[THREADS];
            uint m = 0;
            inner = false;
            for (uint k = 0; k < fronts; k++)
            {
                uint node = front[k];
                if (bvh.isLeaf(node, n))
                {
                    next[m++] = node;
                    continue;
                }
                uint l = nodes[node].left, r = nodes[node].right;
                if (v.sees(nodes[l].box))
                    next[m++] = l;
                if (v.sees(nodes[r].box))
                    next[m++] = r;
                inner = true;
            }
            for (uint k = 0; k < m; k++)
                front[k] = next[k];
            fronts = m;
        }
    }
    GroupMemoryBarrierWithGroupSync();

    if (group.index < fronts)
    {
        ListVisit visit = { v };
        bvh.walk(nodes, n, front[group.index], visit);
    }
    GroupMemoryBarrierWithGroupSync();

    // Keyed by the nearest any ray from the eye could enter, then bitonic sorted on that key.
    uint many = listed <= LISTED ? listed : 0;
    uint size = 1;
    while (size < many)
        size <<= 1;
    for (uint k = group.index; k < size; k += THREADS)
    {
        if (k < many)
        {
            float4 b = sorted[listPlaces[k]];
            listKeys[k] = length(b.xyz - v.eye) - b.w - blend();
        }
        else
            listKeys[k] = 1e30;
    }
    GroupMemoryBarrierWithGroupSync();
    for (uint span = 2; span <= size; span <<= 1)
    {
        for (uint stride = span >> 1; stride > 0; stride >>= 1)
        {
            for (uint k = group.index; k < size; k += THREADS)
            {
                uint other = k ^ stride;
                if (other > k && (listKeys[k] > listKeys[other]) == ((k & span) == 0))
                {
                    float key = listKeys[k];
                    listKeys[k] = listKeys[other];
                    listKeys[other] = key;
                    uint place = listPlaces[k];
                    listPlaces[k] = listPlaces[other];
                    listPlaces[other] = place;
                }
            }
            GroupMemoryBarrierWithGroupSync();
        }
    }
    for (uint k = group.index; k < many; k += THREADS)
    {
        listBalls[k] = sorted[listPlaces[k]];
        listIds[k] = ids[listPlaces[k]];
    }
    GroupMemoryBarrierWithGroupSync();
}

// ---- a ray's gather ----------------------------------------------------------------

/// Fills `g` with what the ray meets between `from` and `limit`: from the tile's list when it
/// holds everything the tile sees, else walking the BVH, nearer box first.
void gather(inout Gather g, Ray ray, float from, float limit, bool fromList, inout uint visits)
{
    float grow = blend();
    g.kept = 0;
    g.cut = limit;
    g.leave = from;
    g.bound = limit;
    if (fromList)
    {
        // Sorted by the nearest possible entry, so the scan stops once nothing left can come first.
        for (uint k = 0; k < listed; k++)
        {
            if (listKeys[k] >= g.horizon(limit))
            {
                g.cut = min(g.cut, listKeys[k]);
                break;
            }
            visits++;
            float4 b = listBalls[k];
            Span span = Ball(b).grown(grow).entered(ray);
            if (!span.missed && span.far > from && span.near < limit)
                g.add(b, listIds[k], span, ray, from);
        }
        return;
    }

    GatherVisit visit = { g, ray, 1.0 / ray.direction, from, limit, grow, visits };
    bvh.walk(nodes, count(), 0, visit);
    g = visit.g;
    visits = visit.visits;
}

// ---- the floor and the pillars ---------------------------------------------------

/// Pillar k: a ring through the galaxy's arms, from the floor up.
Bounds pillar(uint k)
{
    float a = k * (2.0 * PI / PILLARS) + 0.3;
    float r = (k & 1) == 0 ? 6.2 : 3.6;
    float tall = (k & 1) == 0 ? 7.5 : 5.2;
    float3 foot = float3(cos(a) * r, LIFT, sin(a) * r) * U;
    return Bounds(foot - float3(0.35, tall, 0.35) * U, foot + float3(0.35, 0, 0.35) * U);
}

/// What a ray meets of the floor and the pillars: how far (1e30 for nothing), facing which way, in what colour.
struct Ground
{
    float t;
    float3 normal;
    float3 tint;
};

Ground groundOf(Ray ray)
{
    Ground g;
    g.t = 1e30;
    g.normal = float3(0, -1, 0);
    g.tint = float3(0.16, 0.15, 0.2);
    if (ray.direction.y > 1e-4)
    {
        float t = (LIFT * U - ray.origin.y) / ray.direction.y;
        if (t > 0)
        {
            g.t = t;
            // A faint grid a unit across.
            float2 cell = abs(frac(ray.at(t).xz / U) - 0.5);
            g.tint *= 1 + 0.35 * step(0.47, max(cell.x, cell.y));
        }
    }

    float3 inverse = 1.0 / ray.direction;
    for (uint k = 0; k < PILLARS; k++)
    {
        Bounds box = pillar(k);
        Span s = box.entered(ray, inverse);
        if (s.missed || s.near >= g.t)
            continue;
        g.t = s.near;
        // The face it enters: the axis the point sits furthest out along.
        float3 centre = (box.low + box.high) * 0.5, half = (box.high - box.low) * 0.5;
        float3 q = (ray.at(s.near) - centre) / half;
        float3 a = abs(q);
        g.normal = a.x > a.y && a.x > a.z ? float3(sign(q.x), 0, 0) : a.y > a.z ? float3(0, sign(q.y), 0) : float3(0, 0, sign(q.z));
        g.tint = float3(0.55, 0.5, 0.46);
    }

    return g;
}

// ---- light -------------------------------------------------------------------

/// Quilez's analytic soft shadow of one blob on the way from p to the sun.
float blobShadow(float3 p, float3 toSun, float4 b)
{
    float3 oc = p - b.xyz;
    float along = dot(oc, toSun);
    float h = along * along - dot(oc, oc) + b.w * b.w;
    float d = sqrt(max(0.0, b.w * b.w - h)) - b.w;
    float t = -along - sqrt(max(h, 0.0));
    return t > 0.0 ? smoothstep(0.0, 1.0, 2.5 * softness * d / t) : 1.0;
}

/// How lit p is by the blobs, walking the BVH toward the sun: every box the ray passes within a
/// penumbra of. The slow way, to check the grid against.
float walkedShadow(float3 p, float3 toSun, inout uint visits)
{
    ShadowVisit visit = { p, toSun, Ray(p, toSun), 1.0 / toSun, penumbra(), 1.0, visits };
    bvh.walk(nodes, count(), 0, visit);
    visits = visit.visits;
    return visit.lit < 0.01 ? 0.0 : visit.lit;
}

/// How lit p is: the pillars hard, then every blob listed in p's cell of the sun's grid (or met walking the BVH).
float sunlit(float3 p, inout uint cells)
{
    float3 toSun = sun();
    Ray up = Ray(p, toSun);
    float3 inverse = 1.0 / toSun;
    for (uint k = 0; k < PILLARS; k++)
        if (!pillar(k).entered(up, inverse).missed)
            return 0.0;
    if (!shadows)
        return 1.0;
    if (!sunGrid)
        return walkedShadow(p, toSun, cells);

    int2 cell = cellOf(p);
    if (any(cell < 0) || any(cell >= int(LIGHT)))
        return 1.0;
    uint c = uint(cell.y) * LIGHT + uint(cell.x);
    uint end = min(light[CELLS + c], ENTRIES);
    float lit = 1.0;
    for (uint k = light[c]; k < end; k++)
    {
        cells++;
        lit = min(lit, blobShadow(p, toSun, balls[light[2 * CELLS + k]]));
        if (lit < 0.01)
            return 0.0;
    }
    return lit;
}

float3 sky(float3 dir)
{
    float3 c = lerp(float3(0.09, 0.08, 0.16), float3(0.015, 0.02, 0.05), saturate(-dir.y * 1.6 + 0.2));
    float toward = saturate(dot(dir, sun()));
    return c + float3(1.0, 0.75, 0.5) * pow(toward, 300.0) * 6.0 + float3(0.5, 0.35, 0.6) * pow(toward, 6.0) * 0.12;
}

/// The heat scale the debug views use: dark, red, yellow, white.
float3 heat(float x)
{
    x = saturate(x);
    return saturate(float3(1.5 * x, 2.2 * x - 0.6, 3.0 * x - 2.0) + float3(0.05, 0.04, 0.12) * (1.0 - x));
}

// ---- the walks' visitors ---------------------------------------------------------

/// A tile's subtree walk: every leaf its frustum sees goes on the list.
struct ListVisit : bvh.IVisit
{
    Frustum view;

    [mutating] float near(bvh.Node node) { return view.sees(node.box) ? 0.0 : bvh.SKIP; }

    [mutating]
    bool leaf(bvh.Node node)
    {
        uint at;
        InterlockedAdd(listed, 1, at);
        if (at < LISTED)
            listPlaces[at] = node.left;
        return false;
    }
};

/// A ray's gather by walking the BVH: boxes it enters between from and limit, nearer first, so
/// the gather fills with near blobs and prunes the far boxes.
struct GatherVisit : bvh.IVisit
{
    Gather g;
    Ray ray;
    float3 inverse;
    float from;
    float limit;
    float grow;
    uint visits;

    [mutating]
    float near(bvh.Node node)
    {
        visits++;
        Span s = node.box.entered(ray, inverse);
        if (s.far < max(s.near, from) || s.near >= limit)
            return bvh.SKIP;
        // A box skipped only for entering past a full gather still bounds the window.
        if (s.near >= g.horizon(limit))
        {
            g.cut = min(g.cut, s.near);
            return bvh.SKIP;
        }
        return s.near;
    }

    [mutating]
    bool leaf(bvh.Node node)
    {
        float4 b = sorted[node.left];
        Span span = Ball(b).grown(grow).entered(ray);
        if (!span.missed && span.far > from && span.near < limit)
            g.add(b, node.right, span, ray, from);
        return false;
    }
};

/// The way to the sun from p, walking the BVH: every box it passes within a penumbra of darkens it.
struct ShadowVisit : bvh.IVisit
{
    float3 p;
    float3 toSun;
    Ray up;
    float3 inverse;
    float margin;
    float lit;
    uint visits;

    [mutating]
    float near(bvh.Node node)
    {
        visits++;
        Span s = node.box.grown(margin).entered(up, inverse);
        return !s.missed && s.far >= 0 ? s.near : bvh.SKIP;
    }

    [mutating]
    bool leaf(bvh.Node node)
    {
        lit = min(lit, blobShadow(p, toSun, sorted[node.left]));
        return lit < 0.01;
    }
};

[Pixels(8, 8)]       // TILE × TILE
void march(ItemInput i, Canvas canvas)
{
    float depth = 0;
    Camera camera = eyeOn(canvas);
    bool fromList = false;
    if (tiles)
    {
        listTile(tileView(camera, canvas));
        fromList = listed <= LISTED;
    }

    Ray ray = camera.ray(canvas.centred);
    float3 toSun = sun();
    Ground ground = groundOf(ray);
    float limit = min(ground.t, 200 * U);

    Gather g = {};
    uint visits = 0, cells = 0;
    Hit hit = {};
    float from = 0;
    for (uint round = 0; round < 4; round++)
    {
        gather(g, ray, from, limit, fromList, visits);
        if (g.kept == 0)
            break;
        float end = min(min(g.cut, g.leave), limit);
        hit = ray.marched(g, Span(max(from, g.enter[0]), end), 96, 0.3);
        if (hit.found || g.cut >= limit)
            break;
        from = g.cut;
    }

    float3 shown;
    float t;
    if (hit.found)
    {
        float3 normal = hit.normal;
        float lit = sunlit(hit.position + normal * 0.5, cells);
        float diffuse = saturate(dot(normal, toSun)) * lit;
        float spec = pow(saturate(dot(normal, normalize(toSun - ray.direction))), 60.0) * lit;
        float rim = pow(1.0 - saturate(-dot(normal, ray.direction)), 4.0);
        float ambient = 0.18 - 0.12 * normal.y;
        shown = hit.tint.rgb * (float3(1.0, 0.92, 0.82) * 2.2 * diffuse + float3(0.35, 0.38, 0.62) * ambient)
              + float3(1.0, 0.95, 0.9) * spec * 1.5 + float3(0.6, 0.5, 1.0) * rim * 0.35;
        t = hit.t;
        depth = camera.depth(hit.position) - camera.distance;
    }
    else if (ground.t < 1e29)
    {
        float lit = sunlit(ray.at(ground.t) - ray.direction * 0.5, cells);
        shown = ground.tint * (0.3 + 0.9 * lit * saturate(dot(ground.normal, toSun)));
        t = ground.t;
        depth = camera.depth(ray.at(t)) - camera.distance;
    }
    else
    {
        shown = sky(ray.direction);
        t = 200 * U;
        depth = 1e6;
    }

    shown = lerp(shown, sky(ray.direction) * 0.8, saturate((t - 18 * U) / (60 * U)));
    colour result = tonemap.aces(colour(shown, 1));
    if (view == 1)
        result = colour(heat(visits / 192.0), 1);
    else if (view == 2)
        result = colour(hit.found ? hit.normal * 0.5 + 0.5 : float3(0.05, 0.05, 0.07), 1);
    else if (view == 3)
        result = colour(heat(cells / 128.0), 1);
    else if (view == 4)
    {
        // Red where the list overflowed (its pixels walk the BVH), heat for how full it is.
        result = colour(!tiles ? float3(0.05) : listed > LISTED ? float3(0.9, 0.12, 0.2) : heat(listed / float(LISTED)), 1);
        float2 cell = frac(i.pixel / TILE);
        if (min(cell.x, cell.y) < 1.0 / TILE)
            result.rgb *= 0.6;
    }
    marched[group.pixel] = result;
    marchedDepth[group.pixel] = depth;
}
