using UnityEngine;

// Brain: decides what the rat does next and executes the chosen action.
public partial class EnemyMovement
{
    private enum ActionType { Run, Walk, Wait, Turn, Jump, Drop }
    private enum JumpHeight { Short, Full }
    private enum JumpTiming { Early, Late }

    private struct PreyMove
    {
        public ActionType type;
        public JumpHeight height;
        public JumpTiming timing;

        public PreyMove(ActionType t, JumpHeight h = JumpHeight.Short, JumpTiming ti = JumpTiming.Early)
        {
            type = t;
            height = h;
            timing = ti;
        }

        public override string ToString()
        {
            return type == ActionType.Jump ? "Jump-" + height + "-" + timing : type.ToString();
        }
    }

    private PreyMove current;
    private float moveTimer, decisionCooldown, speedScale = 1f;
    private float jumpRand, timingJitter, pendingTime, stuckTimer;
    private float timeSinceLanding = 99f;
    private bool jumpPending, dropping;
    private bool sawObstacle, sawLedge, sawCat, sawCommitted;
    private const float pendingTimeout = 1.6f;
    private readonly PreyMove[] cands1 = new PreyMove[10];
    private readonly PreyMove[] cands2 = new PreyMove[10];

    private void InitBrain()
    {
        current = new PreyMove(ActionType.Run);
        moveTimer = 0f;
        decisionCooldown = 0f;
        speedScale = 1f;
        pendingTime = 0f;
        stuckTimer = 0f;
        timeSinceLanding = 99f;
        jumpPending = false;
        dropping = false;
        sawObstacle = sawLedge = sawCat = sawCommitted = false;
        targetSpeed = 0f;
        debugMove = "";
    }

    private void OnLanded()
    {
        timeSinceLanding = 0f;
        moveTimer = 0f;
        decisionCooldown = 0f;
        jumpPending = false;
        dropping = false;
    }

    private float BaseSpeed(ActionType t)
    {
        switch (t)
        {
            case ActionType.Wait: return 0f;
            case ActionType.Walk: return walkSpeed;
            case ActionType.Drop: return walkSpeed;
            default: return maxSpeed;   // Run, Turn, Jump
        }
    }

    private void ForceTurn()
    {
        moveDir = -moveDir;
        current = new PreyMove(ActionType.Run);
        moveTimer = 0.3f;
        speedScale = 1f;
        jumpPending = false;
        dropping = false;
        stuckTimer = 0f;
        pendingTime = 0f;
        targetSpeed = maxSpeed;
        debugMove = "ForceTurn";
    }

    // Runs every physics tick while the rat is on the ground
    private void RunBrain(Vector2 vel, DirScan fwd, float dt)
    {
        decisionCooldown -= dt;
        moveTimer -= dt;

        // Hard rule: must turn when colliding with a wall
        if (fwd.wall && fwd.wallDist <= wallCheckDistance)
        {
            ForceTurn();
            return;
        }

        // A jump has been chosen: wait for the right take-off moment
        if (jumpPending)
        {
            pendingTime += dt;
            if (RealTakeoffDue(vel, fwd, out float v))
            {
                jumpRequest = true;
                pendingJumpSpeed = v;
                jumpPending = false;
                targetSpeed = Mathf.Abs(vel.x);
                return;
            }
            if (pendingTime > pendingTimeout)
            {
                jumpPending = false;
                moveTimer = 0f;
            }
        }
        if (dropping && moveTimer <= 0f) dropping = false;  // safety timeout

        // New hazards / a cat that just committed force a fresh decision
        bool hazard = (fwd.obstacle && !sawObstacle) || (fwd.ledge && !sawLedge)
                   || (cat.valid && !sawCat) || (cat.valid && cat.committed && !sawCommitted);
        sawObstacle = fwd.obstacle;
        sawLedge = fwd.ledge;
        sawCat = cat.valid;
        sawCommitted = cat.valid && cat.committed;
        if (hazard) moveTimer = Mathf.Min(moveTimer, 0f);

        if (!jumpPending && !dropping && moveTimer <= 0f && decisionCooldown <= 0f) Decide(fwd);

        // Speed for the current action, braking before edges / obstacles it cannot cross
        float sp = BaseSpeed(current.type) * speedScale;
        float speedAlong = Mathf.Max(0f, vel.x * moveDir);
        float brakeAt = 0.25f + speedAlong * 0.12f;
        if (fwd.ledge && current.type != ActionType.Drop && fwd.ledgeDist <= brakeAt) sp = 0f;
        if (fwd.obstacle && !jumpPending && fwd.obstacleDist <= brakeAt) sp = 0f;
        targetSpeed = sp;

        if (sp <= 0f && current.type != ActionType.Wait)
        {
            stuckTimer += dt;
            if (stuckTimer > stuckTime) ForceTurn();
        }
        else
        {
            stuckTimer = 0f;
        }
    }

    // Utility decision: score every candidate, look one move further ahead, pick the best.
    private void Decide(DirScan fwd)
    {
        decisionCooldown = minDecisionGap;
        DirScan back = ScanDirection(-moveDir, true);
        SimState s0 = BuildSimState(fwd, back);

        int n1 = BuildCandidates(s0, cands1);
        float bestU = float.NegativeInfinity;
        PreyMove best = new PreyMove(ActionType.Run);

        for (int i = 0; i < n1; i++)
        {
            float u = Score(s0, cands1[i], true, out SimResult r1);
            if (r1.invalid) continue;

            // 2-step lookahead: value of the best follow-up (this is what makes feints pay off)
            if (wLookahead > 0f && !r1.caught)
            {
                float best2 = float.NegativeInfinity;
                int n2 = BuildCandidates(r1.end, cands2);
                for (int j = 0; j < n2; j++)
                {
                    if (cands1[i].type == ActionType.Turn && cands2[j].type == ActionType.Turn) continue;
                    float u2 = Score(r1.end, cands2[j], false, out SimResult r2);
                    if (!r2.invalid && u2 > best2) best2 = u2;
                }
                if (best2 > float.NegativeInfinity) u += wLookahead * best2;
            }

            u += Random.Range(-noise, noise);
            if (u > bestU)
            {
                bestU = u;
                best = cands1[i];
            }
        }

        ApplyMove(best);
    }

    private void ApplyMove(PreyMove m)
    {
        current = m;
        moveTimer = Random.Range(minActionTime, maxActionTime);
        speedScale = 1f + Random.Range(-speedVariation, speedVariation);      // speed variation
        jumpRand = Random.value;                                              // jump height variation
        timingJitter = Random.Range(-timingVariation, timingVariation);       // timing variation
        pendingTime = 0f;
        jumpPending = false;
        dropping = false;
        stuckTimer = 0f;

        switch (m.type)
        {
            case ActionType.Turn:
                moveDir = -moveDir;
                moveTimer = 0.35f;
                break;
            case ActionType.Jump:
                jumpPending = true;
                break;
            case ActionType.Drop:
                dropping = true;
                moveTimer = 2f;
                break;
        }
        debugMove = m.ToString();
    }

    // ------------------------------------------------------------ take-off timing

    private bool RealTakeoffDue(Vector2 vel, DirScan fwd, out float jumpSpeed)
    {
        float feetY = bodyCollider.bounds.min.y;
        bool catAhead = false;
        float catGap = 999f, catTop = 0f, catW = 0f, catVelAlong = 0f;
        if (cat.valid)
        {
            float dx = cat.x - bodyCollider.bounds.center.x;
            catGap = Mathf.Abs(dx) - ratWidth * 0.5f - cat.halfW;
            catAhead = Mathf.Sign(dx) == moveDir && cat.grounded;
            catTop = cat.feetY + cat.height;
            catW = cat.halfW * 2f;
            catVelAlong = cat.vx * moveDir;
        }

        return TakeoffDue(current, Mathf.Abs(vel.x), feetY,
            fwd.obstacle ? fwd.obstacleDist : -1f, fwd.obstacleTop, fwd.obstacleWidth,
            catAhead, catGap, catTop, catW, catVelAlong, cat.valid ? catGap : 999f,
            pendingTime, jumpRand, timingJitter, out jumpSpeed);
    }

    // Shared by the real rat and the simulation: should the rat leave the ground now?
    // Target priority: obstacle ahead > cat ahead on the ground > free jump (no target).
    private bool TakeoffDue(PreyMove m, float speedAlong, float feetY,
        float obsDist, float obsTop, float obsW,
        bool catAhead, float catGap, float catTop, float catW, float catVelAlong, float catGapAny,
        float waited, float rand01, float jitter, out float jumpSpeed)
    {
        float vxAir = Mathf.Max(speedAlong, airMinSpeed);
        float alpha = Mathf.Clamp01((m.timing == JumpTiming.Early ? earlyAlpha : lateAlpha) + jitter);

        if (obsDist >= 0f)
        {
            float needH = obsTop - feetY;
            float cross = obsW + ratWidth;
            float vMin = RequiredJumpSpeed(needH, cross, vxAir);
            jumpSpeed = PickJumpSpeed(m.height, vMin, rand01);
            return obsDist <= JumpLeadGap(jumpSpeed, needH, cross, vxAir, alpha);
        }

        float maxApex = jumpForce * jumpForce / (2f * gravity);
        if (catAhead && catTop - feetY < maxApex * 0.95f)
        {
            float needH = catTop - feetY;
            float cross = catW + ratWidth;
            float closing = Mathf.Max(0.1f, vxAir - catVelAlong);
            float vMin = RequiredJumpSpeed(needH, cross, closing);
            jumpSpeed = PickJumpSpeed(m.height, vMin, rand01);
            return catGap <= JumpLeadGap(jumpSpeed, needH, cross, closing, alpha);
        }

        // Free jump: nothing to clear
        jumpSpeed = PickJumpSpeed(m.height, 0f, rand01);
        if (m.timing == JumpTiming.Early) return true;
        return waited >= freeLateDelay || catGapAny <= lateTriggerGap;
    }

    // Minimal take-off speed that clears needH (plus clearance, more for wide things)
    private float RequiredJumpSpeed(float needH, float crossWidth, float speed)
    {
        float timeFactor = 1f + 1f / Mathf.Sqrt(fallMultiplier);
        float crossTime = crossWidth / Mathf.Max(speed, 0.1f);
        float widthClearance = 0.5f * gravity * Mathf.Pow(crossTime / timeFactor, 2f);
        float clearance = Mathf.Max(jumpClearance, widthClearance);
        return Mathf.Min(jumpForce, Mathf.Sqrt(2f * gravity * (Mathf.Max(needH, 0f) + clearance)));
    }

    // Short = just enough (or a fraction of a full jump with no target), Full = (nearly) max
    private float PickJumpSpeed(JumpHeight h, float vMin, float rand01)
    {
        float v;
        if (h == JumpHeight.Full) v = jumpForce * (1f - jumpHeightVariation * rand01);
        else if (vMin > 0f) v = vMin * (1f + jumpHeightVariation * rand01);
        else v = shortJumpFraction * jumpForce * (1f + jumpHeightVariation * (rand01 * 2f - 1f));
        return Mathf.Clamp(v, Mathf.Max(vMin, 1f), jumpForce);
    }

    // Distance to the target at which take-off should start. alpha picks where in the safe window:
    // 0 = latest safe take-off, 1 = earliest safe take-off.
    private float JumpLeadGap(float v, float needH, float crossWidth, float closing, float alpha)
    {
        float apex = v * v / (2f * gravity);
        float h = Mathf.Clamp(needH + jumpClearance * 0.5f, 0.1f, apex * 0.98f);
        float tNeed = (v - Mathf.Sqrt(Mathf.Max(0f, v * v - 2f * gravity * h))) / gravity;
        float tAbove = (v / gravity - tNeed) + Mathf.Sqrt(2f * Mathf.Max(0f, apex - h) / (gravity * fallMultiplier));
        float slack = Mathf.Max(0f, tAbove - crossWidth / closing);
        return closing * (tNeed + slack * alpha + jumpTimingMargin + 0.5f * Time.fixedDeltaTime);
    }
}
