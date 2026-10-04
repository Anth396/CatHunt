using System.Collections.Generic;
using UnityEngine;

// Sensing: what the rat sees around it (walls, obstacles, ledges) and a model of the cat.
public partial class EnemyMovement
{
    private struct DirScan
    {
        public bool wall;           // solid wall (or an obstacle too tall to jump)
        public float wallDist;
        public bool obstacle;       // jumpable obstacle
        public float obstacleDist, obstacleTop, obstacleWidth;
        public bool ledge, ledgeVoid;
        public float ledgeDist, dropDepth;
    }

    private struct CatInfo
    {
        public bool valid;
        public float x, vx, vy, feetY, height, halfW, groundY;
        public bool grounded, committed;    // committed = airborne or dashing (cannot change course)
        public float tUp, apexY, airRemaining, g, fm;
    }

    private CatInfo cat;

    private void DetectPlayer()
    {
        playerDetected = false;
        if (detectBox == null) return;

        overlaps.Clear();
        detectBox.Overlap(anyFilter, overlaps);
        foreach (Collider2D col in overlaps)
        {
            if (col == null || !col.CompareTag(playerTag)) continue;
            PlayerMovement found = col.GetComponentInParent<PlayerMovement>();
            if (found != null)
            {
                player = found;
                playerCollider = col;
                playerBody = found.GetComponent<Rigidbody2D>();
                playerDetected = true;
                return;
            }
        }
    }

    // Scans one direction from the rat: nearest wall, tallest jumpable obstacle (with whatever
    // rests on it), and optionally the next ledge.
    private DirScan ScanDirection(float dir, bool includeLedge)
    {
        DirScan r = new DirScan();
        r.wallDist = float.MaxValue;
        r.obstacleDist = float.MaxValue;

        float feetY = bodyCollider.bounds.min.y;
        float maxApex = jumpForce * jumpForce / (2f * gravity);
        float lookAhead = Mathf.Max(maxSpeed, airMinSpeed) * (jumpForce / gravity + jumpTimingMargin) + 0.5f;
        float dist = Mathf.Max(obstacleCheckDistance, wallCheckDistance, lookAhead);

        bool found = false;
        float obsTop = float.MinValue, obsDist = 0f;
        Bounds obsBounds = new Bounds();

        int count = bodyCollider.Cast(new Vector2(dir, 0f), anyFilter, castHits, dist);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = castHits[i];
            Collider2D col = hit.collider;
            if (col == null || col == detectBox || col.transform.IsChildOf(transform)) continue;
            if (col.isTrigger) continue;    // trigger colliders are not solid
            if (col.CompareTag(playerTag)) continue;

            bool horizontalSurface = hit.normal == Vector2.zero || Mathf.Abs(hit.normal.x) > 0.5f;
            if (!horizontalSurface) continue;

            if (col.CompareTag(wallTag))
            {
                r.wall = true;
                r.wallDist = Mathf.Min(r.wallDist, hit.distance);
            }
            else if (col is BoxCollider2D && !col.CompareTag(gameObject.tag))
            {
                float top = col.bounds.max.y;
                if (top - feetY < minObstacleHeight) continue;  // too low to matter
                if (top > obsTop)
                {
                    found = true;
                    obsTop = top;
                    obsDist = hit.distance;
                    obsBounds = col.bounds;
                }
            }
        }

        if (found)
        {
            float width = obsBounds.size.x;
            float top = GetStackTop(obsBounds, ref width);
            if (top - feetY > maxApex)
            {
                // Too tall to jump: it is a wall
                r.wall = true;
                r.wallDist = Mathf.Min(r.wallDist, obsDist);
            }
            else
            {
                r.obstacle = true;
                r.obstacleDist = obsDist;
                r.obstacleTop = top;
                r.obstacleWidth = width;
            }
        }

        if (includeLedge) ScanLedge(dir, ref r);
        return r;
    }

    // Probes the floor ahead with downward rays to find the next ledge (drop or void).
    private void ScanLedge(float dir, ref DirScan r)
    {
        const float step = 0.5f;
        Bounds b = bodyCollider.bounds;
        float frontX = b.center.x + dir * b.extents.x;
        float originY = b.min.y + 0.1f;

        for (float d = 0.15f; d <= ledgeScanRange; d += step)
        {
            // nothing beyond a wall / obstacle matters for the ledge
            if (d > r.wallDist || d > r.obstacleDist) break;

            Vector2 origin = new Vector2(frontX + dir * d, originY);
            int n = Physics2D.Raycast(origin, Vector2.down, solidFilter, rayHits, maxDropDepth + 0.1f);

            float groundDist = -1f;
            for (int i = 0; i < n; i++)
            {
                Collider2D c = rayHits[i].collider;
                if (c == null || c == detectBox || c.transform.IsChildOf(transform)) continue;
                if (groundDist < 0f || rayHits[i].distance < groundDist) groundDist = rayHits[i].distance;
            }

            bool isVoid = groundDist < 0f;
            float depth = isVoid ? float.MaxValue : groundDist - 0.1f;
            if (depth > ledgeMinDepth)
            {
                r.ledge = true;
                r.ledgeVoid = isVoid;
                r.ledgeDist = Mathf.Max(0f, d - step * 0.5f);
                r.dropDepth = isVoid ? maxDropDepth : depth;
                return;
            }
        }
    }

    /// <summary>
    /// Walks up a stack of colliders: looks just above the given bounds for solid colliders resting
    /// on it (including the player), then above those, and so on. Returns the top of the whole
    /// stack and widens 'width' to the union of the stack's horizontal extent.
    /// </summary>
    private float GetStackTop(Bounds baseBounds, ref float width)
    {
        float minX = baseBounds.min.x;
        float maxX = baseBounds.max.x;
        float currentTop = baseBounds.max.y;
        float unionMin = minX;
        float unionMax = maxX;

        for (int level = 0; level < maxStackLevels; level++)
        {
            // Thin slab starting slightly inside the current top, covering its horizontal span
            float slabWidth = Mathf.Max(0.01f, (maxX - minX) - 0.02f);
            Vector2 size = new Vector2(slabWidth, stackCheckHeight);
            Vector2 center = new Vector2((minX + maxX) * 0.5f, currentTop - 0.05f + stackCheckHeight * 0.5f);

            stackResults.Clear();
            Physics2D.OverlapBox(center, size, 0f, solidFilter, stackResults);

            Collider2D highest = null;
            foreach (Collider2D c in stackResults)
            {
                if (c == null || c == detectBox || c.transform.IsChildOf(transform)) continue;
                if (c.bounds.max.y <= currentTop + 0.001f) continue;   // not above the current top
                if (highest == null || c.bounds.max.y > highest.bounds.max.y) highest = c;
            }
            if (highest == null) break;

            Bounds hb = highest.bounds;
            currentTop = hb.max.y;
            minX = hb.min.x;
            maxX = hb.max.x;
            unionMin = Mathf.Min(unionMin, minX);
            unionMax = Mathf.Max(unionMax, maxX);
        }

        width = Mathf.Max(width, unionMax - unionMin);
        return currentTop;
    }

    // ------------------------------------------------------------ cat model

    private void BuildCat()
    {
        cat.valid = playerDetected && player != null && playerCollider != null;
        if (!cat.valid) return;

        Bounds cb = playerCollider.bounds;
        Vector2 v = playerBody != null ? playerBody.linearVelocity : Vector2.zero;
        cat.x = cb.center.x;
        cat.vx = v.x;
        cat.vy = v.y;
        cat.feetY = cb.min.y;
        cat.height = cb.size.y;
        cat.halfW = cb.extents.x;
        cat.grounded = player.onGroundState;
        cat.committed = !cat.grounded || player.IsDashing;
        cat.g = player.gravity;
        cat.fm = player.fallMultiplier;
        cat.groundY = cat.grounded ? cat.feetY : bodyCollider.bounds.min.y;   // assume a shared floor
        cat.tUp = 0f;
        cat.apexY = cat.feetY;
        cat.airRemaining = 0f;

        if (!cat.grounded)
        {
            if (cat.vy > 0f)
            {
                cat.tUp = cat.vy / cat.g;
                cat.apexY = cat.feetY + cat.vy * cat.vy / (2f * cat.g);
            }
            float drop = Mathf.Max(0f, cat.apexY - cat.groundY);
            cat.airRemaining = cat.tUp + Mathf.Sqrt(2f * drop / (cat.g * cat.fm));
        }
    }

    // Predicted cat position / height 'tau' seconds from now (constant horizontal velocity)
    private float CatX(float tau)
    {
        float horizon = cat.grounded ? 1.0f : cat.airRemaining + 0.35f;
        return cat.x + cat.vx * Mathf.Min(tau, horizon);
    }

    private float CatFeetY(float tau)
    {
        if (cat.grounded) return cat.feetY;
        if (tau <= cat.tUp) return cat.feetY + cat.vy * tau - 0.5f * cat.g * tau * tau;
        if (tau < cat.airRemaining)
        {
            float f = tau - cat.tUp;
            return cat.apexY - 0.5f * cat.g * cat.fm * f * f;
        }
        return cat.groundY;
    }
}
