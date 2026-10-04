using UnityEngine;

// Sim: a cheap forward simulation of the rat (and the cat's predicted path) used to score moves.
public partial class EnemyMovement
{
    // Everything in one direction from the rat; distances are from its front edge, -1 = none
    private struct Lane
    {
        public float wall, obs, obsTop, obsW, ledge, drop;
        public bool ledgeVoid;
    }

    private struct SimState
    {
        public float x, feetY, groundY, vx, dir, t;
        public Lane fwd, back;
    }

    private struct SimResult
    {
        public SimState end;
        public bool invalid, caught, blocked, clipped;
        public float minDangerGap, airTime;
    }

    private static Lane ToLane(DirScan s)
    {
        Lane l = new Lane();
        l.wall = s.wall ? s.wallDist : -1f;
        l.obs = s.obstacle ? s.obstacleDist : -1f;
        l.obsTop = s.obstacleTop;
        l.obsW = s.obstacleWidth;
        l.ledge = s.ledge ? s.ledgeDist : -1f;
        l.ledgeVoid = s.ledgeVoid;
        l.drop = s.dropDepth;
        return l;
    }

    private SimState BuildSimState(DirScan fwd, DirScan back)
    {
        Bounds b = bodyCollider.bounds;
        SimState s = new SimState();
        s.x = b.center.x;
        s.feetY = b.min.y;
        s.groundY = b.min.y;
        s.vx = enemyBody.linearVelocity.x;
        s.dir = moveDir;
        s.t = 0f;
        s.fwd = ToLane(fwd);
        s.back = ToLane(back);
        return s;
    }

    private int BuildCandidates(SimState s, PreyMove[] list)
    {
        int n = 0;
        list[n++] = new PreyMove(ActionType.Run);
        list[n++] = new PreyMove(ActionType.Walk);
        list[n++] = new PreyMove(ActionType.Wait);
        list[n++] = new PreyMove(ActionType.Turn);
        list[n++] = new PreyMove(ActionType.Jump, JumpHeight.Short, JumpTiming.Early);
        list[n++] = new PreyMove(ActionType.Jump, JumpHeight.Short, JumpTiming.Late);
        list[n++] = new PreyMove(ActionType.Jump, JumpHeight.Full, JumpTiming.Early);
        list[n++] = new PreyMove(ActionType.Jump, JumpHeight.Full, JumpTiming.Late);
        if (s.fwd.ledge >= 0f && !s.fwd.ledgeVoid) list[n++] = new PreyMove(ActionType.Drop);
        return n;
    }

    // Distance the rat can walk before something stops it (wall, obstacle, ledge)
    private float WalkStop(Lane l, ActionType type)
    {
        float d = 999f;
        if (l.wall >= 0f) d = Mathf.Min(d, l.wall - 0.05f);
        if (l.obs >= 0f) d = Mathf.Min(d, l.obs - 0.05f);
        if (l.ledge >= 0f && type != ActionType.Drop) d = Mathf.Min(d, l.ledge - 0.2f);
        return Mathf.Max(0f, d);
    }

    private static float ShiftFwd(float d, float travelled)
    {
        if (d < 0f) return d;
        float n = d - travelled;
        return n < 0f ? -1f : n;
    }

    private static float ShiftBack(float d, float travelled)
    {
        if (d < 0f) return d;
        float n = d + travelled;
        return n < 0f ? -1f : n;
    }

    private static void AdvanceLanes(ref SimState s, float travelled)
    {
        s.fwd.wall = ShiftFwd(s.fwd.wall, travelled);
        s.fwd.obs = ShiftFwd(s.fwd.obs, travelled);
        s.fwd.ledge = ShiftFwd(s.fwd.ledge, travelled);
        s.back.wall = ShiftBack(s.back.wall, travelled);
        s.back.obs = ShiftBack(s.back.obs, travelled);
        s.back.ledge = ShiftBack(s.back.ledge, travelled);
    }

    private SimResult Simulate(SimState s, PreyMove m)
    {
        SimResult r = new SimResult();
        r.minDangerGap = 999f;

        if (m.type == ActionType.Turn)
        {
            Lane tmp = s.fwd; s.fwd = s.back; s.back = tmp;
            s.dir = -s.dir;
        }
        if (m.type == ActionType.Drop && (s.fwd.ledge < 0f || s.fwd.ledgeVoid))
        {
            r.invalid = true;
            r.end = s;
            return r;
        }

        const float dt = 0.05f;
        float dir = s.dir;
        float x0 = s.x;
        float x = s.x, feetY = s.feetY, groundY = s.groundY, vx = s.vx, vy = 0f;
        float elapsed = 0f, airStart = 0f, airVx = 0f, waited = 0f;
        bool airborne = false, jumpTaken = false;

        float timed = m.type == ActionType.Turn ? 0.35f : 0.5f * (minActionTime + maxActionTime);
        bool openEnded = m.type == ActionType.Jump || m.type == ActionType.Drop;
        float maxTime = openEnded ? 1.6f : timed;
        float sp = BaseSpeed(m.type);
        float groundStop = WalkStop(s.fwd, m.type);
        float wallStop = s.fwd.wall >= 0f ? s.fwd.wall - 0.05f : 999f;
        float jumpSpeed = 0f;
        float rand01 = 0.5f;

        while (elapsed < maxTime)
        {
            float tau = s.t + elapsed;
            float travelled = (x - x0) * dir;
            bool landed = false;

            if (!airborne)
            {
                if (m.type == ActionType.Jump && !jumpTaken)
                {
                    float obsD = s.fwd.obs >= 0f ? Mathf.Max(0f, s.fwd.obs - travelled) : -1f;
                    bool catAhead = false;
                    float catGap = 999f, catTop = 0f, catW = 0f, catVelAlong = 0f, catAny = 999f;
                    if (cat.valid)
                    {
                        float dxc = CatX(tau) - x;
                        float cf = CatFeetY(tau);
                        catAny = Mathf.Abs(dxc) - ratWidth * 0.5f - cat.halfW;
                        catGap = catAny;
                        catAhead = Mathf.Sign(dxc) == dir && cf <= cat.groundY + 0.05f;
                        catTop = cf + cat.height;
                        catW = cat.halfW * 2f;
                        catVelAlong = cat.vx * dir;
                    }

                    waited += dt;
                    if (TakeoffDue(m, Mathf.Abs(vx), feetY, obsD, s.fwd.obsTop, s.fwd.obsW,
                        catAhead, catGap, catTop, catW, catVelAlong, catAny,
                        waited, rand01, 0f, out jumpSpeed))
                    {
                        airborne = true;
                        jumpTaken = true;
                        vy = jumpSpeed;
                        airStart = elapsed;
                        airVx = dir * Mathf.Max(Mathf.Abs(vx), airMinSpeed);
                    }
                }

                if (!airborne)
                {
                    float along = vx * dir;
                    bool blocked = travelled >= groundStop - 0.05f && sp > 0f;
                    if (blocked)
                    {
                        along = 0f;
                        if (elapsed < maxTime * 0.6f) r.blocked = true;
                    }
                    else if (sp <= 0f || along < -0.1f) along = Mathf.Lerp(along, 0f, groundFriction * dt);
                    else if (along < sp) along = Mathf.Min(along + acceleration * dt, sp);
                    else along = Mathf.Lerp(along, sp, groundFriction * dt);

                    vx = along * dir;
                    x += vx * dt;
                    if ((x - x0) * dir > groundStop) x = x0 + dir * groundStop;

                    // Drop: once past the edge the rat falls to the lower floor
                    if (m.type == ActionType.Drop && (x - x0) * dir >= s.fwd.ledge + ratWidth * 0.5f)
                    {
                        airborne = true;
                        vy = 0f;
                        airStart = elapsed;
                        airVx = vx;
                        groundY = s.groundY - s.fwd.drop;
                    }
                }
            }
            else
            {
                vy -= gravity * (vy < 0f ? fallMultiplier : 1f) * dt;
                feetY += vy * dt;
                x += airVx * dt;

                float tr = (x - x0) * dir;
                if (tr > wallStop)
                {
                    x = x0 + dir * wallStop;
                    airVx = 0f;
                }
                if (jumpTaken && s.fwd.obs >= 0f && tr > s.fwd.obs && tr < s.fwd.obs + s.fwd.obsW + ratWidth
                    && feetY < s.fwd.obsTop - 0.02f)
                {
                    r.clipped = true;
                }
                if (vy < 0f && feetY <= groundY)
                {
                    feetY = groundY;
                    landed = true;
                }
            }

            elapsed += dt;
            CheckCapture(ref r, s.t + elapsed, x, feetY);
            if (landed)
            {
                r.airTime = elapsed - airStart;
                airborne = false;
                vx = airVx;
                break;
            }
        }

        if (m.type == ActionType.Jump && !jumpTaken) r.blocked = true;
        if (airborne)
        {
            r.airTime = elapsed - airStart;
            vx = airVx;
        }

        AdvanceLanes(ref s, (x - x0) * dir);
        s.x = x;
        s.feetY = feetY;
        s.groundY = groundY;
        s.vx = vx;
        s.t += elapsed;
        r.end = s;
        return r;
    }

    // Does the rat overlap the cat's predicted body at this moment?
    private void CheckCapture(ref SimResult r, float tau, float x, float feetY)
    {
        if (!cat.valid) return;

        float cf = CatFeetY(tau);
        float gap = Mathf.Abs(x - CatX(tau)) - ratWidth * 0.5f - cat.halfW;
        bool overlapV = feetY < cf + cat.height - 0.05f && feetY + ratHeight > cf + 0.05f;
        if (!overlapV) return;

        if (gap <= 0f) r.caught = true;
        if (gap < r.minDangerGap) r.minDangerGap = Mathf.Max(gap, 0f);
    }

    // ------------------------------------------------------------ utility scoring

    private float Score(SimState s0, PreyMove m, bool isFirst, out SimResult r)
    {
        r = Simulate(s0, m);
        if (r.invalid) return -999f;

        SimState e = r.end;
        float u = 0f;

        if (cat.valid)
        {
            // 1. Distance from the cat (end of the move) and how close it got on the way
            float endGap = Mathf.Abs(e.x - CatX(e.t)) - ratWidth * 0.5f - cat.halfW;
            float safe = Mathf.Clamp01(endGap / safeGap);
            float prox = r.minDangerGap < dangerGap ? Mathf.Clamp01((dangerGap - r.minDangerGap) / dangerGap) : 0f;
            u += wDistance * (safe - prox);
            if (r.caught) u -= caughtPenalty;

            // 2. Time to land: long airtime is risky when the cat is near (cannot steer in the air)
            float gap0 = Mathf.Max(0f, Mathf.Abs(s0.x - cat.x) - ratWidth * 0.5f - cat.halfW);
            if (r.airTime > 0f)
            {
                float exposure = Mathf.Clamp01(1f - gap0 / safeGap);
                float approach = cat.vx * Mathf.Sign(s0.x - cat.x);
                float tCat = approach > 0.1f ? gap0 / approach : 99f;
                float landsFirst = tCat > r.airTime + 0.15f ? 0.25f : -0.5f;
                u += wTimeToLand * exposure * (landsFirst - Mathf.Clamp01(r.airTime / airRef));
            }

            // 3. Verticality: being above the cat is good (it has to jump to follow)
            float vert = Mathf.Clamp((e.feetY - CatFeetY(e.t)) / verticalRange, -1f, 1f);
            u += wVertical * vert;

            // 4. Bait value: tempting / punishing the cat
            if (!r.caught) u += wBait * BaitValue(s0, m, gap0);
        }
        else
        {
            // Nobody around: patrol with momentum
            if (m.type == ActionType.Run) u += 0.2f;
            else if (m.type == ActionType.Walk) u += 0.1f;
            else if (m.type == ActionType.Turn) u -= 0.3f;
            else if (m.type == ActionType.Wait) u -= 0.2f;
        }

        // 5. Fork count: more ways out from where the move ends
        u += wForks * Mathf.Clamp01(ForkCount(e) / 4f);

        if (r.blocked && m.type != ActionType.Wait) u -= blockedPenalty;
        if (r.clipped) u -= clipPenalty;
        if (m.type == ActionType.Turn) u -= turnCost;
        if (m.type == ActionType.Wait) u -= waitCost;

        // Chained jumps: another jump right after landing while an obstacle is still ahead
        if (isFirst && m.type == ActionType.Jump && timeSinceLanding <= chainWindow && s0.fwd.obs >= 0f)
            u += chainBonus;

        return u;
    }

    private float BaitValue(SimState s0, PreyMove m, float gap0)
    {
        float approach = cat.vx * Mathf.Sign(s0.x - cat.x);     // > 0: cat is closing on the rat
        bool feint = m.type == ActionType.Wait || m.type == ActionType.Turn || m.type == ActionType.Drop
                  || (m.type == ActionType.Jump && m.timing == JumpTiming.Late);
        float v = 0f;

        // The cat is airborne / dashing: it cannot change course, so dodge it
        if (cat.committed && feint && gap0 < baitGap * 1.5f) v += 1.0f;

        // Stand ground (wait / turn) while the cat closes in inside the bait band
        if (approach > 1f && gap0 > dangerGap && gap0 <= baitGap
            && (m.type == ActionType.Wait || m.type == ActionType.Turn)) v += 0.6f;

        // Cat is far and not committed: slow down or jump early to lure it in
        if (!cat.committed && gap0 > baitGap
            && (m.type == ActionType.Walk || (m.type == ActionType.Jump && m.timing == JumpTiming.Early))) v += 0.4f;

        return v;
    }

    private float ForkCount(SimState e)
    {
        return LaneOptions(e.fwd) + LaneOptions(e.back);
    }

    // Ways out of one side: open floor to run on, an obstacle to jump, a ledge to drop from
    private int LaneOptions(Lane l)
    {
        int c = 0;
        if (WalkStop(l, ActionType.Run) > forkMinOpen) c++;
        if (l.obs >= 0f) c++;
        if (l.ledge >= 0f && !l.ledgeVoid) c++;
        return c;
    }
}
