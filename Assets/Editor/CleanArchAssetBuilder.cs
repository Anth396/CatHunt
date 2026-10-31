#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates (or updates) every ScriptableObject asset of the event / FSM architecture: events, shared
/// variables, powerups, and the two state machines (actions, decisions, states). Safe to run again.
/// Run it from Tools > CatHunt > Build SO Assets.
/// </summary>
public static class CleanArchAssetBuilder
{
    public const string Root = "Assets/ScriptableObjects";
    public const string Events = Root + "/Events";
    public const string Variables = Root + "/Variables";
    public const string Powerups = Root + "/Powerups";
    public const string Actions = Root + "/FSM/Actions";
    public const string Decisions = Root + "/FSM/Decisions";
    public const string MoveStates = Root + "/FSM/CatMovement";
    public const string BuffStates = Root + "/FSM/CatBuff";

    [MenuItem("Tools/CatHunt/Build SO Assets")]
    public static string Build()
    {
        BuildEvents();
        BuildVariables();
        BuildPowerups();
        BuildCatMovementFsm();
        BuildCatBuffFsm();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return "SO assets built";
    }

    // ------------------------------------------------------------------ helpers

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static T Asset<T>(string folder, string name) where T : ScriptableObject
    {
        string path = folder + "/" + name + ".asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
        {
            EnsureFolder(folder);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        EditorUtility.SetDirty(asset);
        return asset;
    }

    private static AudioClip Clip(string file)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/" + file);
        if (clip == null) Debug.LogWarning("CleanArchAssetBuilder: missing sound " + file);
        return clip;
    }

    // ------------------------------------------------------------------ events, variables, powerups

    private static void BuildEvents()
    {
        foreach (string n in new[]
        {
            "GameStart", "GameRestart", "GameOver", "RoundReset", "PreyCaught", "ObstacleHit", "MainMenuRequested",
            "JumpPressed", "DashPressed", "DashReleased", "SuperDashReady", "SlashPressed"
        })
            Asset<SimpleGameEvent>(Events, n);

        foreach (string n in new[] { "ScoreChanged", "TimeChanged", "ObjectBroken", "MoveChanged" })
            Asset<IntGameEvent>(Events, n);

        Asset<StringGameEvent>(Events, "SceneRequested");
        Asset<PowerupGameEvent>(Events, "PowerupCollected");
    }

    private static void BuildVariables()
    {
        Asset<IntVariable>(Variables, "LevelStartScore");
        Asset<BoolVariable>(Variables, "GameRunning");
        Asset<BoolVariable>(Variables, "CatInvisible");
        Asset<StringVariable>(Variables, "LoadingTarget");
    }

    private static void BuildPowerups()
    {
        PowerupData invis = Asset<PowerupData>(Powerups, "InvisibilityPowerup");
        invis.type = PowerupType.Invisibility;
        invis.duration = 8f;
        invis.displayName = "Invisibility";

        PowerupData haste = Asset<PowerupData>(Powerups, "HastePowerup");
        haste.type = PowerupType.Haste;
        haste.duration = 6f;
        haste.displayName = "Haste";
    }

    // ------------------------------------------------------------------ shared FSM pieces

    private static State Remain()
    {
        State remain = Asset<State>(Root + "/FSM", "RemainInState");
        remain.setupActions = new Action[0];
        remain.actions = new Action[0];
        remain.eventTriggeredActions = new EventAction[0];
        remain.exitActions = new Action[0];
        remain.transitions = new Transition[0];
        return remain;
    }

    private static Transition T(Decision decision, State whenTrue, State remain)
    {
        return new Transition { decision = decision, trueState = whenTrue, falseState = remain };
    }

    private static State MakeState(string folder, string name, Color gizmo)
    {
        State s = Asset<State>(folder, name);
        s.sceneGizmoColor = gizmo;
        s.setupActions = new Action[0];
        s.actions = new Action[0];
        s.eventTriggeredActions = new EventAction[0];
        s.exitActions = new Action[0];
        s.transitions = new Transition[0];
        return s;
    }

    private static SetMotorModeAction Mode(string name, CatMotorMode mode)
    {
        SetMotorModeAction a = Asset<SetMotorModeAction>(Actions, name);
        a.mode = mode;
        return a;
    }

    private static SetAnimationOverrideAction Anim(string name, string animation)
    {
        SetAnimationOverrideAction a = Asset<SetAnimationOverrideAction>(Actions, name);
        a.animationState = animation;
        return a;
    }

    private static PlaySoundAction Sound(string name, string file)
    {
        PlaySoundAction a = Asset<PlaySoundAction>(Actions, name);
        a.clip = Clip(file);
        return a;
    }

    private static CatFlagDecision Flag(string name, CatFlag flag)
    {
        CatFlagDecision d = Asset<CatFlagDecision>(Decisions, name);
        d.flag = flag;
        return d;
    }

    private static CatCountdownDecision Countdown(string name, CatDuration duration)
    {
        CatCountdownDecision d = Asset<CatCountdownDecision>(Decisions, name);
        d.duration = duration;
        return d;
    }

    // ------------------------------------------------------------------ cat movement FSM (6 states)

    private static void BuildCatMovementFsm()
    {
        State remain = Remain();

        // Actions
        Action motorFree = Mode("SetMotorMode_Free", CatMotorMode.Free);
        Action motorBrake = Mode("SetMotorMode_Brake", CatMotorMode.Brake);
        Action animNone = Anim("Anim_None", "");
        Action animHold = Anim("Anim_Hold", "cat-hold");
        Action animDash = Anim("Anim_Dash", "cat-dash");
        Action animSuper = Anim("Anim_SuperDash", "cat-super-dash");
        Action animSlash = Anim("Anim_Slash", "cat-slash");
        Action clearSuper = Asset<ClearSuperReadyAction>(Actions, "ClearSuperReady");
        StartDashAction startDash = Asset<StartDashAction>(Actions, "StartDash");
        startDash.super = false;
        StartDashAction startSuper = Asset<StartDashAction>(Actions, "StartSuperDash");
        startSuper.super = true;
        Action endDash = Asset<EndDashAction>(Actions, "EndDash");
        Action bounce = Asset<BounceAction>(Actions, "Bounce");
        Action slashCooldown = Asset<StartSlashCooldownAction>(Actions, "StartSlashCooldown");
        Action soundDash = Sound("Sound_Dash", "dash-sound.mp3");
        Action soundSlash = Sound("Sound_Slash", "slash.mp3");
        Action soundKnock = Sound("Sound_Knockback", "knockback.mp3");
        Action startCling = Asset<StartClingAction>(Actions, "StartCling");
        Action wallJump = Asset<WallJumpAction>(Actions, "WallJump");
        Action animCling = Anim("Anim_Cling", "cat-wall-hold");

        // Decisions
        Decision dashPressed = Flag("CatFlag_DashPressed", CatFlag.DashPressed);
        Decision dashReleased = Flag("CatFlag_DashReleased", CatFlag.DashReleased);
        Decision releasedSuper = Flag("CatFlag_DashReleasedAfterSuper", CatFlag.DashReleasedAfterSuper);
        Decision slashPressed = Flag("CatFlag_SlashPressed", CatFlag.SlashPressed);
        Decision jumpPressed = Flag("CatFlag_JumpPressed", CatFlag.JumpPressed);
        Decision obstacleHit = Flag("CatFlag_ObstacleHit", CatFlag.ObstacleHit);
        Decision notGrounded = Flag("CatFlag_NotGrounded", CatFlag.NotGrounded);
        Decision dashDone = Countdown("CatCountdown_Dash", CatDuration.Dash);
        Decision superDone = Countdown("CatCountdown_SuperDash", CatDuration.SuperDash);
        Decision slashDone = Countdown("CatCountdown_Slash", CatDuration.Slash);
        Decision stunDone = Countdown("CatCountdown_Stun", CatDuration.Stun);
        Decision clingToWall = Flag("CatFlag_ClingToWall", CatFlag.ClingToWall);
        Decision clingReleased = Flag("CatFlag_ClingReleased", CatFlag.ClingReleased);
        Decision grounded = Flag("CatFlag_Grounded", CatFlag.Grounded);
        Decision lostWall = Flag("CatFlag_LostWall", CatFlag.LostWall);
        Decision wallJumpDone = Countdown("CatCountdown_WallJump", CatDuration.WallJump);

        // States (asset names must match the CatMoveState enum)
        State normal = MakeState(MoveStates, "Normal", Color.green);
        State charging = MakeState(MoveStates, "Charging", Color.yellow);
        State dashing = MakeState(MoveStates, "Dashing", Color.cyan);
        State superDashing = MakeState(MoveStates, "SuperDashing", Color.blue);
        State slashing = MakeState(MoveStates, "Slashing", Color.magenta);
        State stunned = MakeState(MoveStates, "Stunned", Color.red);
        State wallClinging = MakeState(MoveStates, "WallClinging", new Color(1f, 0.5f, 0f));
        State wallJumping = MakeState(MoveStates, "WallJumping", Color.white);

        normal.setupActions = new[] { motorFree, animNone };
        normal.transitions = new[]
        {
            T(dashPressed, charging, remain),
            T(slashPressed, slashing, remain),
            T(obstacleHit, stunned, remain),
            T(clingToWall, wallClinging, remain)
        };

        // Holding the wall: released -> fall, jump -> kick off, ground or lost wall -> normal
        wallClinging.setupActions = new[] { startCling, animCling };
        wallClinging.exitActions = new[] { motorFree, animNone };
        wallClinging.transitions = new[]
        {
            T(obstacleHit, stunned, remain),
            T(jumpPressed, wallJumping, remain),
            T(clingReleased, normal, remain),
            T(grounded, normal, remain),
            T(lostWall, normal, remain)
        };

        wallJumping.setupActions = new[] { wallJump };
        wallJumping.transitions = new[]
        {
            T(obstacleHit, stunned, remain),
            T(wallJumpDone, normal, remain)
        };

        charging.setupActions = new[] { motorBrake, animHold, clearSuper };
        charging.exitActions = new[] { motorFree, animNone };
        charging.transitions = new[]
        {
            T(releasedSuper, superDashing, remain),     // must come before the plain release
            T(dashReleased, dashing, remain),
            T(jumpPressed, normal, remain),
            T(notGrounded, normal, remain),
            T(obstacleHit, stunned, remain)
        };

        dashing.setupActions = new[] { (Action)startDash, animDash, soundDash };
        dashing.exitActions = new[] { endDash, animNone };
        dashing.transitions = new[]
        {
            T(obstacleHit, stunned, remain),
            T(jumpPressed, normal, remain),
            T(dashDone, normal, remain)
        };

        superDashing.setupActions = new[] { (Action)startSuper, animSuper, soundDash };
        superDashing.exitActions = new[] { endDash, animNone };
        superDashing.transitions = new[]
        {
            T(obstacleHit, stunned, remain),
            T(jumpPressed, normal, remain),
            T(superDone, normal, remain)
        };

        slashing.setupActions = new[] { animSlash, soundSlash };
        slashing.exitActions = new[] { animNone, slashCooldown };
        slashing.transitions = new[]
        {
            T(obstacleHit, stunned, remain),
            T(dashPressed, charging, remain),
            T(jumpPressed, normal, remain),
            T(slashDone, normal, remain)
        };

        stunned.setupActions = new[] { bounce, soundKnock };
        stunned.exitActions = new[] { motorFree };
        stunned.transitions = new[] { T(stunDone, normal, remain) };

        foreach (State s in new[] { normal, charging, dashing, superDashing, slashing, stunned, wallClinging, wallJumping }) EditorUtility.SetDirty(s);
    }

    // ------------------------------------------------------------------ cat buff FSM (3 states)

    private static void BuildCatBuffFsm()
    {
        State remain = Remain();

        Action clearPending = Asset<ClearPendingPowerupAction>(Actions, "ClearPendingPowerup");
        SetInvisibleAction invisOn = Asset<SetInvisibleAction>(Actions, "SetInvisible_On");
        invisOn.invisible = true;
        SetInvisibleAction invisOff = Asset<SetInvisibleAction>(Actions, "SetInvisible_Off");
        invisOff.invisible = false;
        SetHasteAction hasteOn = Asset<SetHasteAction>(Actions, "SetHaste_On");
        hasteOn.haste = true;
        SetHasteAction hasteOff = Asset<SetHasteAction>(Actions, "SetHaste_Off");
        hasteOff.haste = false;
        Action soundPotion = Sound("Sound_Potion", "potion.mp3");

        PendingPowerupDecision pendInvis = Asset<PendingPowerupDecision>(Decisions, "Pending_Invisibility");
        pendInvis.powerupType = PowerupType.Invisibility;
        PendingPowerupDecision pendHaste = Asset<PendingPowerupDecision>(Decisions, "Pending_Haste");
        pendHaste.powerupType = PowerupType.Haste;
        Decision expired = Asset<BuffExpiredDecision>(Decisions, "BuffExpired");

        State noBuff = MakeState(BuffStates, "NoBuff", Color.grey);
        State invisible = MakeState(BuffStates, "Invisible", Color.white);
        State haste = MakeState(BuffStates, "Haste", Color.yellow);

        noBuff.setupActions = new[] { clearPending, (Action)invisOff, hasteOff };
        noBuff.transitions = new[]
        {
            T(pendInvis, invisible, remain),
            T(pendHaste, haste, remain)
        };

        invisible.setupActions = new[] { clearPending, (Action)invisOn, soundPotion };
        invisible.exitActions = new Action[] { invisOff };
        invisible.transitions = new[]
        {
            T(pendHaste, haste, remain),
            T(expired, noBuff, remain)
        };

        haste.setupActions = new[] { clearPending, (Action)hasteOn, soundPotion };
        haste.exitActions = new Action[] { hasteOff };
        haste.transitions = new[]
        {
            T(pendInvis, invisible, remain),
            T(expired, noBuff, remain)
        };

        foreach (State s in new[] { noBuff, invisible, haste }) EditorUtility.SetDirty(s);
    }
}
#endif
