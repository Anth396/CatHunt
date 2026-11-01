using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Settings for the walkable-surface graph. They describe what the rat can do, so the graph only holds moves it can make.</summary>
[Serializable]
public class RatNavSettings
{
    [Tooltip("Distance between two sampled standing points along a surface.")]
    public float spacing = 0.5f;
    [Tooltip("Largest height difference between two neighbouring points that is still a walk (stairs, slopes).")]
    public float maxStepHeight = 0.8f;
    public float runSpeed = 8f;
    [Tooltip("Take-off speeds tried for jumps (up to the rat's full jump).")]
    public float[] jumpSpeeds = { 9f, 12f, 15f };
    [Tooltip("Horizontal speeds tried for jumps.")]
    public float[] airSpeeds = { 4f, 8f };
    public float gravity = 30f;
    public float fallMultiplier = 1.5f;
    public float simStep = 0.02f;
    public float maxAirTime = 3f;
    public string groundTag = "Ground";
    public string stairTag = "Stair";
}

/// <summary>
/// Walkable-surface graph of the level, built from the colliders in the scene. A node is a spot where the
/// rat can stand (on a Ground or Stair tagged top surface); an edge is a walk to the neighbouring spot, or a
/// jump / drop that was simulated with the rat's own gravity and checked against the level geometry.
/// Used for path finding only: it is built once and never changes.
/// </summary>
public class PlatformNavGraph
{
    public enum EdgeType { Walk, Ballistic }

    public class Node
    {
        public int id;
        public int column;
        public Vector2 pos;             // the surface point under the rat's centre
        public bool flat;               // a level surface (jumps start only from these)
        public float rest;              // on a slope the box rests on its lower corner: its feet sit this much above 'pos'
        public Collider2D support;
        public readonly List<Edge> edges = new List<Edge>();
    }

    public class Edge
    {
        public Node from, to;
        public EdgeType type;
        public int dir;                 // -1 / +1, the horizontal direction of the move
        public float vx;                // horizontal speed of a ballistic move
        public float vy;                // take-off speed of a ballistic move (0 = walk off a ledge)
        public float cost;              // seconds
        public SuperDashTarget gate;    // blocks a walk until it has been knocked down (armor, bookshelf)
        public int failures;            // times a rat tried this move and ended up somewhere else
        public bool Open => (gate == null || gate.IsUsed) && failures < 2;
        public bool IsWalkOff => type == EdgeType.Ballistic && vy <= 0.01f;
    }

    public readonly List<Node> nodes = new List<Node>();
    public RatNavSettings settings;
    public Vector2 bodySize;            // the rat's collider size in the world
    public Vector2 bounds;              // min x / max x of the level (for diagnostics)

    private List<Node>[] columns;
    private float minX;
    private ContactFilter2D solidFilter;
    private readonly Dictionary<Collider2D, bool> oneWayCache = new Dictionary<Collider2D, bool>();
    private readonly List<Collider2D> overlapBuffer = new List<Collider2D>();
    private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[16];

    // ------------------------------------------------------------------ building

    public static PlatformNavGraph Build(RatNavSettings settings, Vector2 bodySize)
    {
        PlatformNavGraph g = new PlatformNavGraph();
        g.settings = settings;
        g.bodySize = bodySize;
        g.Construct();
        return g;
    }

    private bool IsWalkable(Collider2D c)
    {
        return c != null && !c.isTrigger && (c.CompareTag(settings.groundTag) || c.CompareTag(settings.stairTag));
    }

    // Colliders the rat does not collide with, or that do not stop it
    private bool IsIgnored(Collider2D c)
    {
        if (c == null || c.isTrigger) return true;
        if (c.CompareTag("Player") || c.CompareTag("Enemy")) return true;
        if (c.GetComponent<PassableWhenGrounded>() != null) return true;
        return false;
    }

    private bool IsOneWay(Collider2D c)
    {
        if (!oneWayCache.TryGetValue(c, out bool oneWay))
        {
            PlatformEffector2D e = c.usedByEffector ? c.GetComponent<PlatformEffector2D>() : null;
            oneWay = e != null && e.useOneWay;
            oneWayCache[c] = oneWay;
        }
        return oneWay;
    }

    private void Construct()
    {
        Physics2D.SyncTransforms();
        solidFilter = new ContactFilter2D();
        solidFilter.useTriggers = false;
        solidFilter.useLayerMask = false;

        // Extent of everything the rat can stand on
        Bounds area = new Bounds();
        bool any = false;
        foreach (Collider2D c in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsWalkable(c)) continue;
            if (!any) { area = c.bounds; any = true; }
            else area.Encapsulate(c.bounds);
        }
        if (!any) return;

        minX = area.min.x;
        bounds = new Vector2(area.min.x, area.max.x);
        float s = settings.spacing;
        int columnCount = Mathf.CeilToInt(area.size.x / s) + 1;
        columns = new List<Node>[columnCount];

        // 1) standing points: a vertical ray per column, every walkable top surface it crosses
        float top = area.max.y + 1f;
        float rayLength = area.size.y + 2f;
        for (int ix = 0; ix < columnCount; ix++)
        {
            columns[ix] = new List<Node>();
            float x = minX + ix * s;
            RaycastHit2D[] hits = Physics2D.RaycastAll(new Vector2(x, top), Vector2.down, rayLength);
            Array.Sort(hits, (a, b) => b.point.y.CompareTo(a.point.y));
            foreach (RaycastHit2D h in hits)
            {
                if (!IsWalkable(h.collider) || h.normal.y < 0.5f) continue;
                Vector2 p = new Vector2(x, h.point.y);

                bool duplicate = false;
                foreach (Node n in columns[ix]) if (Mathf.Abs(n.pos.y - p.y) < 0.15f) { duplicate = true; break; }
                if (duplicate) continue;
                if (!HasHeadroom(p, h.collider)) continue;

                bool flat = h.normal.y > 0.98f;
                float rest = flat ? 0f : Mathf.Min(0.7f, bodySize.x * 0.5f * Mathf.Abs(h.normal.x / Mathf.Max(0.2f, h.normal.y)));
                Node node = new Node { id = nodes.Count, column = ix, pos = p, flat = flat, rest = rest, support = h.collider };
                nodes.Add(node);
                columns[ix].Add(node);
            }
        }

        // 2) walks between neighbouring points
        foreach (Node a in nodes)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                Node b = WalkNeighbour(a, d, out SuperDashTarget gate, out bool blocked);
                if (b == null || blocked) continue;
                a.edges.Add(new Edge
                {
                    from = a, to = b, type = EdgeType.Walk, dir = d, gate = gate,
                    cost = Vector2.Distance(a.pos, b.pos) / settings.runSpeed
                });
            }
        }

        // 3) jumps and drops, simulated
        HashSet<long> known = new HashSet<long>();
        List<Node> snapshot = new List<Node>(nodes);
        foreach (Node a in snapshot)
        {
            for (int d = -1; d <= 1; d += 2)
            {
                bool ledge = true;
                foreach (Edge e in a.edges) if (e.type == EdgeType.Walk && e.dir == d) { ledge = false; break; }

                // Jumps and drops start from level ground only: on a slope the rat's box rests on a corner
                // and leaves from a different spot than the sampled point
                if (!a.flat) continue;

                // Walking off a ledge
                if (ledge) TrySimulate(a, d, settings.runSpeed, 0f, known);

                // Jumps start every other point (and at every ledge) to keep the graph small
                if (ledge || a.column % 2 == 0)
                {
                    foreach (float vx in settings.airSpeeds)
                        foreach (float vy in settings.jumpSpeeds)
                            TrySimulate(a, d, vx, vy, known);
                }
            }
        }
    }

    // Room for the rat's body above the surface point (a slope may rise next to it, so the test box is narrow)
    private bool HasHeadroom(Vector2 feet, Collider2D support)
    {
        Vector2 size = new Vector2(Mathf.Min(0.3f, bodySize.x * 0.5f), Mathf.Max(0.1f, bodySize.y - 0.25f));
        Vector2 center = feet + new Vector2(0f, 0.2f + size.y * 0.5f);
        overlapBuffer.Clear();
        Physics2D.OverlapBox(center, size, 0f, solidFilter, overlapBuffer);
        foreach (Collider2D c in overlapBuffer)
        {
            if (IsIgnored(c) || c == support) continue;
            if (c.CompareTag(settings.stairTag)) continue;      // slopes next to the point
            if (c.GetComponentInParent<SuperDashTarget>() != null) continue;    // armor and the like: handled as a gate
            return false;
        }
        return true;
    }

    private Node WalkNeighbour(Node a, int dir, out SuperDashTarget gate, out bool blocked)
    {
        gate = null;
        blocked = false;
        int ix = a.column + dir;
        if (ix < 0 || ix >= columns.Length) return null;

        // Where two surfaces meet the rat on (a fork of stairs, a step), walking on takes it onto the highest one
        Node best = null;
        foreach (Node b in columns[ix])
        {
            if (Mathf.Abs(b.pos.y - a.pos.y) > settings.maxStepHeight) continue;
            if (best == null || b.pos.y > best.pos.y) best = b;
        }
        if (best == null) return null;

        // Nothing solid in the way of the rat's body between the two points. Ground and stairs are left out:
        // the rat walks along them (the sides of a slope would otherwise look like a wall).
        float h = bodySize.y + 0.1f;
        Vector2 size = new Vector2(Mathf.Max(0.1f, bodySize.x - 0.04f), h);
        Vector2 from = a.pos + new Vector2(0f, 0.05f + h * 0.5f);
        Vector2 delta = (best.pos + new Vector2(0f, 0.05f + h * 0.5f)) - from;
        float dist = delta.magnitude;
        int count = Physics2D.BoxCast(from, size, 0f, delta / dist, solidFilter, castBuffer, dist);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = castBuffer[i].collider;
            if (IsIgnored(c) || IsWalkable(c)) continue;
            SuperDashTarget target = c.GetComponentInParent<SuperDashTarget>();
            if (target != null) { gate = target; continue; }       // passable once it has been knocked down
            blocked = true;
            return null;
        }
        return best;
    }

    // Flight of a rat that leaves node 'a' with the given speeds: the first walkable surface it lands on becomes an edge
    private void TrySimulate(Node a, int dir, float vx, float vy, HashSet<long> known)
    {
        float dt = settings.simStep;
        float halfH = bodySize.y * 0.5f;
        Vector2 size = new Vector2(Mathf.Max(0.1f, bodySize.x - 0.04f), Mathf.Max(0.1f, bodySize.y - 0.04f));

        Vector2 center = a.pos + new Vector2(0f, halfH + 0.03f);
        Vector2 castFrom = center;
        Vector2 v = new Vector2(dir * vx, vy);
        float t = 0f;
        int step = 0;
        float floorY = a.pos.y - 40f;

        while (t < settings.maxAirTime)
        {
            float g = settings.gravity * (v.y < 0f ? settings.fallMultiplier : 1f);
            v.y -= g * dt;
            center += v * dt;
            t += dt;
            step++;
            if (center.y < floorY) return;
            if (step % 2 != 0) continue;

            Vector2 delta = center - castFrom;
            float dist = delta.magnitude;
            if (dist < 0.0001f) continue;

            int count = Physics2D.BoxCast(castFrom, size, 0f, delta / dist, solidFilter, castBuffer, dist);
            RaycastHit2D? hit = null;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D h = castBuffer[i];
                Collider2D c = h.collider;
                if (IsIgnored(c)) continue;
                if (c == a.support && h.distance < 0.05f) continue;                      // the surface it just left
                if (IsOneWay(c) && (v.y >= 0f || h.normal.y < 0.5f))
                {
                    // Cutting through the side or underside of a stair flight is not a move the rat should try:
                    // it gets caught in the platform. Only the surface it starts on is no obstacle.
                    if (c == a.support) continue;
                    return;
                }
                if (!hit.HasValue || h.distance < hit.Value.distance) hit = h;
            }

            if (hit.HasValue)
            {
                RaycastHit2D h = hit.Value;
                if (IsWalkable(h.collider) && h.normal.y >= 0.5f && v.y <= 0f)
                {
                    Vector2 landed = castFrom + (delta / dist) * h.distance;
                    Vector2 feet = new Vector2(landed.x, landed.y - halfH - 0.03f);
                    Node target = NearestAt(feet, 0.3f, 0.3f);
                    if (target != null && target != a)
                    {
                        long key = ((long)a.id << 32) | (uint)target.id;
                        if (known.Add(key))
                        {
                            a.edges.Add(new Edge
                            {
                                from = a, to = target, type = EdgeType.Ballistic, dir = dir, vx = vx, vy = vy,
                                cost = t + 0.25f
                            });
                        }
                    }
                }
                return;     // landed, or hit a wall / ceiling: the flight ends here
            }
            castFrom = center;
        }
    }

    private Node NearestAt(Vector2 feet, float maxDx, float maxDy)
    {
        int ix = Mathf.RoundToInt((feet.x - minX) / settings.spacing);
        Node best = null;
        float bestScore = float.MaxValue;
        for (int c = ix - 1; c <= ix + 1; c++)
        {
            if (c < 0 || c >= columns.Length) continue;
            foreach (Node n in columns[c])
            {
                float dx = Mathf.Abs(n.pos.x - feet.x);
                float dy = Mathf.Abs(n.pos.y + n.rest - feet.y);
                if (dx > maxDx || dy > maxDy) continue;
                float score = dx + dy * 2f;
                if (score < bestScore) { bestScore = score; best = n; }
            }
        }
        return best;
    }

    // ------------------------------------------------------------------ queries

    /// <summary>The graph node closest to a spot (usually the rat's feet or an item). Null if none within the range.</summary>
    public Node Nearest(Vector2 pos, float maxDx = 0.8f, float maxDyBelow = 2f, float maxDyAbove = 0.6f)
    {
        if (columns == null) return null;
        int ix = Mathf.RoundToInt((pos.x - minX) / settings.spacing);
        int reach = Mathf.CeilToInt(maxDx / settings.spacing);
        Node best = null;
        float bestScore = float.MaxValue;
        for (int c = ix - reach; c <= ix + reach; c++)
        {
            if (c < 0 || c >= columns.Length) continue;
            foreach (Node n in columns[c])
            {
                float dx = Mathf.Abs(n.pos.x - pos.x);
                float dy = n.pos.y + n.rest - pos.y;
                if (dx > maxDx || dy > maxDyAbove || -dy > maxDyBelow) continue;
                float score = dx + Mathf.Abs(dy) * 1.5f;
                if (score < bestScore) { bestScore = score; best = n; }
            }
        }
        return best;
    }

    /// <summary>
    /// Standing points from which a rat can reach an item hanging in the air at 'target'. Points where the item is
    /// within 'tightDy' of the rat's body centre are preferred; if there are none, 'looseDy' is used.
    /// </summary>
    public List<Node> NodesAround(Vector2 target, float maxDx, float tightDy, float looseDy)
    {
        List<Node> tight = new List<Node>();
        List<Node> loose = new List<Node>();
        float halfH = bodySize.y * 0.5f;
        foreach (Node n in nodes)
        {
            if (Mathf.Abs(n.pos.x - target.x) > maxDx) continue;
            float dy = Mathf.Abs(n.pos.y + n.rest + halfH - target.y);
            if (dy <= tightDy) tight.Add(n);
            else if (dy <= looseDy) loose.Add(n);
        }
        return tight.Count > 0 ? tight : loose;
    }

    /// <summary>Every node that can be reached from 'start' (following the edges).</summary>
    public HashSet<int> Reachable(Node start)
    {
        HashSet<int> seen = new HashSet<int>();
        if (start == null) return seen;
        Stack<Node> open = new Stack<Node>();
        open.Push(start);
        seen.Add(start.id);
        while (open.Count > 0)
        {
            Node n = open.Pop();
            foreach (Edge e in n.edges) if (e.Open && seen.Add(e.to.id)) open.Push(e.to);
        }
        return seen;
    }

    /// <summary>
    /// Cheapest path from 'start' to any of the goals. 'extraCost' adds seconds for entering a node (null = none)
    /// and may return infinity to forbid a node. Returns null if no goal can be reached.
    /// </summary>
    public List<Edge> FindPath(Node start, ICollection<Node> goals, Func<Node, float> extraCost = null)
    {
        if (start == null || goals == null || goals.Count == 0) return null;
        int n = nodes.Count;
        float[] dist = new float[n];
        Edge[] via = new Edge[n];
        bool[] done = new bool[n];
        for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;
        dist[start.id] = 0f;

        while (true)
        {
            int u = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0) return null;
            done[u] = true;

            Node node = nodes[u];
            if (goals.Contains(node)) return Trace(via, node, start);

            foreach (Edge e in node.edges)
            {
                if (!e.Open) continue;
                float extra = extraCost != null ? extraCost(e.to) : 0f;
                if (float.IsInfinity(extra)) continue;
                float nd = dist[u] + e.cost + extra;
                if (nd < dist[e.to.id]) { dist[e.to.id] = nd; via[e.to.id] = e; }
            }
        }
    }

    /// <summary>
    /// Path to the node that is safest within 'maxCost' seconds of travel: the one with the highest 'score'.
    /// Used to run away when no safe way to the goal exists.
    /// </summary>
    public List<Edge> FindBestWithin(Node start, float maxCost, Func<Node, float> score, Func<Node, float> extraCost = null)
    {
        if (start == null) return null;
        int n = nodes.Count;
        float[] dist = new float[n];
        Edge[] via = new Edge[n];
        bool[] done = new bool[n];
        for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;
        dist[start.id] = 0f;

        Node bestNode = start;
        float bestScore = score(start);
        while (true)
        {
            int u = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < n; i++) if (!done[i] && dist[i] < best) { best = dist[i]; u = i; }
            if (u < 0 || best > maxCost) break;
            done[u] = true;

            Node node = nodes[u];
            float s = score(node);
            if (s > bestScore) { bestScore = s; bestNode = node; }

            foreach (Edge e in node.edges)
            {
                if (!e.Open) continue;
                float extra = extraCost != null ? extraCost(e.to) : 0f;
                if (float.IsInfinity(extra)) continue;
                float nd = dist[u] + e.cost + extra;
                if (nd < dist[e.to.id]) { dist[e.to.id] = nd; via[e.to.id] = e; }
            }
        }
        return bestNode == start ? new List<Edge>() : Trace(via, bestNode, start);
    }

    private List<Edge> Trace(Edge[] via, Node goal, Node start)
    {
        List<Edge> path = new List<Edge>();
        Node n = goal;
        while (n != start)
        {
            Edge e = via[n.id];
            path.Add(e);
            n = e.from;
        }
        path.Reverse();
        return path;
    }
}
