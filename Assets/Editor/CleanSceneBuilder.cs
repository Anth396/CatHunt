#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Builds the clean-slate scenes (Assets/Scenes/SO): copies the prefabs the levels use, strips every old
/// script, then adds the new components and wires them to the ScriptableObject events. Safe to run again.
/// </summary>
public static class CleanSceneBuilder
{
    const string SceneDir = "Assets/Scenes/SO/";
    const string PrefabDir = "Assets/Prefabs/SO";
    const string Root = CleanArchAssetBuilder.Root;

    static readonly string[] OldPrefabs =
    {
        "Assets/Prefabs/WoodenCrate.prefab", "Assets/Prefabs/RockObstacle.prefab", "Assets/Prefabs/Building.prefab"
    };

    static StringBuilder log;

    [MenuItem("Tools/CatHunt/Build Clean Scenes")]
    public static string BuildAll()
    {
        log = new StringBuilder();
        try
        {
            CleanArchAssetBuilder.Build();
            BuildMainMenu();
            BuildLoading();
            BuildLevel("city");
            BuildLevel("sewer");
            AssetDatabase.SaveAssets();
        }
        catch (Exception e)
        {
            log.AppendLine("FAILED: " + e);
        }
        return log.ToString();
    }

    // ================================================================== scenes

    static void BuildMainMenu()
    {
        Scene scene = OpenScene("mainmenu");
        StripScene(scene);
        DeleteRoot(scene, "GameManager");

        GameObject holder = RenameRoot(scene, "GameObject", "MainMenuController");
        MainMenuView view = GetOrAdd<MainMenuView>(holder);
        view.highScoreText = Find(scene, "Canvas/ScoreText").GetComponent<TextMeshProUGUI>();
        view.gameScore = GameScore();
        view.sceneRequested = Ev<StringGameEvent>("SceneRequested");
        view.firstLevel = "city";
        EditorUtility.SetDirty(view);

        GameObject changer = MakeSceneChanger(scene);
        Button start = Find(scene, "Canvas/StartButton").GetComponent<Button>();
        ClearClicks(start);
        UnityEventTools.AddVoidPersistentListener(start.onClick, V(view, "StartNewGame"));

        Save(scene);
    }

    static void BuildLoading()
    {
        Scene scene = OpenScene("loading");
        StripScene(scene);

        GameObject holder = RenameRoot(scene, "SceneLoader", "LoadingController");
        LoadingView view = GetOrAdd<LoadingView>(holder);
        view.loadingTarget = Load<StringVariable>(CleanArchAssetBuilder.Variables + "/LoadingTarget.asset");
        view.levelText = Find(scene, "Canvas/Level").GetComponent<TextMeshProUGUI>();
        view.mainMenuRequested = Ev<SimpleGameEvent>("MainMenuRequested");
        EditorUtility.SetDirty(view);

        MakeSceneChanger(scene);
        Button back = Find(scene, "Canvas/MainMenuButton").GetComponent<Button>();
        ClearClicks(back);
        UnityEventTools.AddVoidPersistentListener(back.onClick, V(view, "CancelToMainMenu"));

        Save(scene);
    }

    static void BuildLevel(string name)
    {
        Scene scene = OpenScene(name);
        CopyPrefabInstances(scene);
        CaptureRatSettings(scene);
        StripScene(scene);
        DeleteRoot(scene, "GameManager");
        DeleteRoot(scene, "ActionManager");
        RenameRoot(scene, "OtherManager", "Music");   // keeps the background music AudioSource

        RemoveListeners(scene);
        GameObject systems = GetRoot(scene, "Systems") ?? new GameObject("Systems");
        if (systems.scene != scene) SceneManager.MoveGameObjectToScene(systems, scene);

        // ---- input
        CatInput input = GetOrAdd<CatInput>(Child(systems, "CatInput"));
        input.moveChanged = Ev<IntGameEvent>("MoveChanged");
        input.jumpPressed = Ev<SimpleGameEvent>("JumpPressed");
        input.dashPressed = Ev<SimpleGameEvent>("DashPressed");
        input.dashReleased = Ev<SimpleGameEvent>("DashReleased");
        input.superDashReady = Ev<SimpleGameEvent>("SuperDashReady");
        input.slashPressed = Ev<SimpleGameEvent>("SlashPressed");
        EditorUtility.SetDirty(input);

        // ---- level flow
        GameRunning running = new GameRunning();
        LevelStarter starter = GetOrAdd<LevelStarter>(Child(systems, "LevelStarter"));
        starter.gameStart = Ev<SimpleGameEvent>("GameStart");
        starter.gameRunning = running.flag;
        EditorUtility.SetDirty(starter);

        GameObject timerGo = Child(systems, "LevelTimer");
        LevelTimer timer = GetOrAdd<LevelTimer>(timerGo);
        timer.constants = Constants();
        timer.timeChanged = Ev<IntGameEvent>("TimeChanged");
        timer.gameOver = Ev<SimpleGameEvent>("GameOver");
        timer.gameRunning = running.flag;
        EditorUtility.SetDirty(timer);
        Listen(timerGo, "GameStart", V(timer, "StartTimer"));
        Listen(timerGo, "GameRestart", V(timer, "StartTimer"));

        GameObject scoreGo = Child(systems, "ScoreKeeper");
        ScoreKeeper score = GetOrAdd<ScoreKeeper>(scoreGo);
        score.gameScore = GameScore();
        score.levelStartScore = Load<IntVariable>(CleanArchAssetBuilder.Variables + "/LevelStartScore.asset");
        score.constants = Constants();
        score.gameRunning = running.flag;
        score.scoreChanged = Ev<IntGameEvent>("ScoreChanged");
        EditorUtility.SetDirty(score);
        Listen(scoreGo, "GameStart", V(score, "OnGameStart"));
        Listen(scoreGo, "GameRestart", V(score, "OnGameRestart"));
        Listen(scoreGo, "PreyCaught", V(score, "OnPreyCaught"));
        ListenInt(scoreGo, "ObjectBroken", I(score, "AddScore"));

        GameObject roundGo = Child(systems, "RoundResetter");
        RoundResetter round = GetOrAdd<RoundResetter>(roundGo);
        round.roundReset = Ev<SimpleGameEvent>("RoundReset");
        round.gameRunning = running.flag;
        EditorUtility.SetDirty(round);
        Listen(roundGo, "PreyCaught", V(round, "OnPreyCaught"));

        GameObject highGo = Child(systems, "HighScore");
        HighScoreKeeper high = GetOrAdd<HighScoreKeeper>(highGo);
        high.gameScore = GameScore();
        EditorUtility.SetDirty(high);
        Listen(highGo, "GameOver", V(high, "Save"));

        MakeSceneChanger(scene, systems);

        // ---- pause
        GameObject pauseGo = Child(systems, "PauseController");
        PauseController pause = GetOrAdd<PauseController>(pauseGo);
        pause.pausePanel = Find(scene, "Canvas/PausePopUp");
        pause.gameRunning = running.flag;
        EditorUtility.SetDirty(pause);
        Listen(pauseGo, "GameRestart", V(pause, "ClearPause"));

        // ---- HUD (on the canvas)
        GameObject canvas = Find(scene, "Canvas");
        HudView hud = GetOrAdd<HudView>(canvas);
        hud.timerText = Find(scene, "Canvas/timerText").GetComponent<TextMeshProUGUI>();
        hud.scoreText = Find(scene, "Canvas/scoreText").GetComponent<TextMeshProUGUI>();
        hud.popUpPanel = Find(scene, "Canvas/EndGamePopUp");
        hud.finalScoreText = Find(scene, "Canvas/EndGamePopUp/FinalScoreText").GetComponent<TextMeshProUGUI>();
        hud.gameScore = GameScore();
        EditorUtility.SetDirty(hud);
        ListenInt(canvas, "ScoreChanged", I(hud, "SetScore"));
        ListenInt(canvas, "TimeChanged", I(hud, "SetTime"));
        Listen(canvas, "GameOver", V(hud, "ShowGameOver"));
        Listen(canvas, "GameStart", V(hud, "HidePopup"));
        Listen(canvas, "GameRestart", V(hud, "HidePopup"));

        // ---- buttons
        SimpleGameEvent restart = Ev<SimpleGameEvent>("GameRestart");
        SimpleGameEvent mainMenu = Ev<SimpleGameEvent>("MainMenuRequested");
        StringGameEvent sceneRequested = Ev<StringGameEvent>("SceneRequested");

        Button pauseBtn = Find(scene, "Canvas/PauseButton").GetComponent<Button>();
        ClearClicks(pauseBtn);
        UnityEventTools.AddVoidPersistentListener(pauseBtn.onClick, V(pause, "Pause"));

        Button resumeBtn = Find(scene, "Canvas/PausePopUp/ResumeButton").GetComponent<Button>();
        ClearClicks(resumeBtn);
        UnityEventTools.AddVoidPersistentListener(resumeBtn.onClick, V(pause, "Resume"));

        Button pauseRestart = Find(scene, "Canvas/PausePopUp/RestartButton").GetComponent<Button>();
        ClearClicks(pauseRestart);
        UnityEventTools.AddVoidPersistentListener(pauseRestart.onClick, V(restart, "Raise"));

        Button pauseMenu = Find(scene, "Canvas/PausePopUp/MainMenuButton").GetComponent<Button>();
        ClearClicks(pauseMenu);
        UnityEventTools.AddVoidPersistentListener(pauseMenu.onClick, V(high, "Save"));
        UnityEventTools.AddVoidPersistentListener(pauseMenu.onClick, V(mainMenu, "Raise"));

        Button endRestart = Find(scene, "Canvas/EndGamePopUp/RestartButton").GetComponent<Button>();
        ClearClicks(endRestart);
        UnityEventTools.AddVoidPersistentListener(endRestart.onClick, V(restart, "Raise"));

        Button cont = Find(scene, "Canvas/EndGamePopUp/ContinueButton").GetComponent<Button>();
        ClearClicks(cont);
        string next = name == "city" ? "sewer" : "city";
        UnityEventTools.AddStringPersistentListener(cont.onClick, S(sceneRequested, "Raise"), next);

        // ---- the cat
        BuildCat(scene, running);

        // ---- world
        BuildRats(scene);
        BuildWorldObjects(scene);

        Save(scene);
    }

    // ================================================================== cat

    static void BuildCat(Scene scene, GameRunning running)
    {
        GameObject cat = Find(scene, "Cat");
        PlayerInputRemover(cat);

        // Sounds: the state machine plays dash / slash / knockback on the cat's own AudioSource, so only the
        // jump and catch sounds need their own child sources
        foreach (string old in new[] { "readySound", "ReadySound", "slashSound" })
        {
            Transform t = cat.transform.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        AudioSource jump = EnsureAudioChild(cat, "jumpSound", "jump.mp3");
        AudioSource caught = EnsureAudioChild(cat, "caughtSound", "rat-caught.mp3");
        AudioSource own = cat.GetComponent<AudioSource>();
        if (own != null) { own.playOnAwake = false; own.clip = null; }

        CatMotor motor = GetOrAdd<CatMotor>(cat);
        motor.constants = Constants();
        motor.jumpAudio = jump;
        EditorUtility.SetDirty(motor);

        State remain = Load<State>(Root + "/FSM/RemainInState.asset");

        CatStateController fsm = GetOrAdd<CatStateController>(cat);
        fsm.motor = motor;
        fsm.startState = Load<State>(CleanArchAssetBuilder.MoveStates + "/Normal.asset");
        fsm.currentState = null;
        fsm.remainState = remain;
        fsm.audioSource = own;
        fsm.superReadyClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/super-dash-ready.mp3");
        EditorUtility.SetDirty(fsm);

        CatBuffController buff = GetOrAdd<CatBuffController>(cat);
        buff.motor = motor;
        buff.catInvisible = running.invisible;
        buff.startState = Load<State>(CleanArchAssetBuilder.BuffStates + "/NoBuff.asset");
        buff.currentState = null;
        buff.remainState = remain;
        buff.audioSource = own;
        EditorUtility.SetDirty(buff);

        PreyCatchDetector catcher = GetOrAdd<PreyCatchDetector>(cat);
        catcher.preyCaught = Ev<SimpleGameEvent>("PreyCaught");
        catcher.caughtAudio = caught;
        EditorUtility.SetDirty(catcher);

        ListenInt(cat, "MoveChanged", I(motor, "SetMoveInput"));
        Listen(cat, "JumpPressed", V(motor, "Jump"), V(fsm, "OnJumpPressed"));
        Listen(cat, "DashPressed", V(fsm, "OnDashPressed"));
        Listen(cat, "DashReleased", V(fsm, "OnDashReleased"));
        Listen(cat, "SuperDashReady", V(fsm, "OnSuperDashReady"));
        Listen(cat, "SlashPressed", V(fsm, "OnSlashPressed"));
        Listen(cat, "ObstacleHit", V(fsm, "OnObstacleHit"));
        Listen(cat, "GameRestart", V(motor, "ResetState"), V(fsm, "ResetState"), V(buff, "ResetState"), V(catcher, "ResetState"));
        Listen(cat, "RoundReset", V(motor, "ResetState"), V(fsm, "ResetState"), V(catcher, "ResetState"));

        PowerupGameEventListener pl = cat.AddComponent<PowerupGameEventListener>();
        pl.gameEvent = Ev<PowerupGameEvent>("PowerupCollected");
        UnityEventTools.AddPersistentListener(pl.response, P(buff, "OnPowerupCollected"));
        EditorUtility.SetDirty(pl);
    }

    static void PlayerInputRemover(GameObject cat)
    {
        // Input is read by CatInput; the PlayerInput component would be a second, unused input path
        foreach (Component c in cat.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "PlayerInput") Object.DestroyImmediate(c);
        }
    }

    // ================================================================== rats and world objects

    static void CaptureRatSettings(Scene scene)
    {
        // Keep the tuned values of the old rat AI: copy them onto the new component before stripping
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb.GetType().Name != "EnemyMovement") continue;
                RatController rat = mb.GetComponent<RatController>();
                if (rat == null) rat = mb.gameObject.AddComponent<RatController>();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(mb), rat);
                log.AppendLine("copied rat settings: " + mb.gameObject.name);
            }
        }
    }

    static void BuildRats(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (RatController rat in root.GetComponentsInChildren<RatController>(true))
            {
                rat.gameConstants = Constants();
                rat.catInvisible = Load<BoolVariable>(CleanArchAssetBuilder.Variables + "/CatInvisible.asset");
                if (rat.detectBox == null)
                {
                    Transform box = rat.transform.Find("ratDetectBox");
                    if (box != null) rat.detectBox = box.GetComponent<BoxCollider2D>();
                }
                EditorUtility.SetDirty(rat);

                GameObject go = rat.gameObject;
                Listen(go, "GameRestart", V(rat, "ResetState"));
                Listen(go, "RoundReset", V(rat, "ResetState"));
            }
        }
    }

    static void BuildWorldObjects(Scene scene)
    {
        GameObject potion = FindOrNull(scene, "potion");
        PotionItem potionItem = null;
        if (potion != null)
        {
            potionItem = GetOrAdd<PotionItem>(potion);
            potionItem.powerup = Load<PowerupData>(CleanArchAssetBuilder.Powerups + "/InvisibilityPowerup.asset");
            potionItem.powerupCollected = Ev<PowerupGameEvent>("PowerupCollected");
            EditorUtility.SetDirty(potionItem);

            AudioSource oldAudio = potion.GetComponent<AudioSource>();
            if (oldAudio != null) Object.DestroyImmediate(oldAudio);
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (BreakableCrate crate in root.GetComponentsInChildren<BreakableCrate>(true))
            {
                if (potionItem == null) continue;
                // Only the first crate drops the potion
                crate.onBroken = new UnityEvent();
                crate.onRestored = new UnityEvent();
                UnityEventTools.AddVoidPersistentListener(crate.onBroken, V(potionItem, "Reveal"));
                UnityEventTools.AddVoidPersistentListener(crate.onRestored, V(potionItem, "Hide"));
                EditorUtility.SetDirty(crate);
                break;
            }
        }
    }

    // ================================================================== prefabs

    static void CopyPrefabInstances(Scene scene)
    {
        EnsureFolder(PrefabDir);
        foreach (string oldPath in OldPrefabs)
        {
            string newPath = PrefabDir + "/" + Path.GetFileName(oldPath);
            List<GameObject> instances = new List<GameObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    GameObject go = t.gameObject;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    if (source != null && AssetDatabase.GetAssetPath(source) == oldPath) instances.Add(go);
                }
            }

            foreach (GameObject inst in instances)
            {
                if (!File.Exists(newPath))
                {
                    // The first instance becomes the new prefab: unpack, strip the old scripts, save as a copy
                    Vector3 pos = inst.transform.position;
                    Quaternion rot = inst.transform.rotation;
                    PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    StripObject(inst);
                    inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    PrefabUtility.SaveAsPrefabAssetAndConnect(inst, newPath, InteractionMode.AutomatedAction);
                    inst.transform.SetPositionAndRotation(pos, rot);
                    log.AppendLine("prefab copy: " + newPath);
                }
                else
                {
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPath);
                    GameObject replacement = (GameObject)PrefabUtility.InstantiatePrefab(prefab, inst.transform.parent);
                    replacement.transform.localPosition = inst.transform.localPosition;
                    replacement.transform.localRotation = inst.transform.localRotation;
                    replacement.transform.localScale = inst.transform.localScale;
                    replacement.transform.SetSiblingIndex(inst.transform.GetSiblingIndex());
                    replacement.name = inst.name;
                    Object.DestroyImmediate(inst);
                }
            }
        }

        AddComponentsToPrefabCopies();
    }

    static void AddComponentsToPrefabCopies()
    {
        string crate = PrefabDir + "/WoodenCrate.prefab";
        if (File.Exists(crate))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(crate);
            BreakableCrate bc = GetOrAdd<BreakableCrate>(root);
            bc.scoreValue = 50;
            bc.objectBroken = Ev<IntGameEvent>("ObjectBroken");
            AudioSource audio = root.GetComponent<AudioSource>();
            if (audio != null)
            {
                audio.playOnAwake = false;
                audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/wood-break.mp3");
            }
            bc.breakAudio = audio;
            foreach (SimpleGameEventListener old in root.GetComponents<SimpleGameEventListener>()) Object.DestroyImmediate(old);
            SimpleGameEventListener l = root.AddComponent<SimpleGameEventListener>();
            l.gameEvent = Ev<SimpleGameEvent>("GameRestart");
            UnityEventTools.AddVoidPersistentListener(l.response, V(bc, "ResetState"));
            PrefabUtility.SaveAsPrefabAsset(root, crate);
            PrefabUtility.UnloadPrefabContents(root);
        }

        string rock = PrefabDir + "/RockObstacle.prefab";
        if (File.Exists(rock))
        {
            GameObject root = PrefabUtility.LoadPrefabContents(rock);
            ObstacleHazard hazard = GetOrAdd<ObstacleHazard>(root);
            hazard.obstacleHit = Ev<SimpleGameEvent>("ObstacleHit");
            hazard.hitAudio = null;     // the cat's Stunned state plays the knockback sound
            AudioSource audio = root.GetComponent<AudioSource>();
            if (audio != null) audio.playOnAwake = false;
            PrefabUtility.SaveAsPrefabAsset(root, rock);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ================================================================== stripping

    static bool IsOldProjectScript(MonoBehaviour mb)
    {
        MonoScript script = MonoScript.FromMonoBehaviour(mb);
        if (script == null) return false;
        string path = AssetDatabase.GetAssetPath(script);
        if (!path.StartsWith("Assets/Scripts/")) return false;
        return !path.StartsWith("Assets/Scripts/Clean/") && !path.StartsWith("Assets/Scripts/SOArch/");
    }

    static void StripObject(GameObject root)
    {
        // Missing scripts first (a component whose script no longer exists)
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }

        // Some old scripts need others (RequireComponent), so repeat until nothing is left
        for (int pass = 0; pass < 4; pass++)
        {
            bool removed = false;
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || !IsOldProjectScript(mb)) continue;
                log.AppendLine("removed " + mb.GetType().Name + " from " + mb.gameObject.name);
                Object.DestroyImmediate(mb);
                removed = true;
            }
            if (!removed) break;
        }
    }

    static void StripScene(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects()) StripObject(root);
    }

    static void RemoveListeners(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            DestroyAll<SimpleGameEventListener>(root);
            DestroyAll<IntGameEventListener>(root);
            DestroyAll<StringGameEventListener>(root);
            DestroyAll<PowerupGameEventListener>(root);
        }
    }

    static void DestroyAll<T>(GameObject root) where T : Component
    {
        foreach (T c in root.GetComponentsInChildren<T>(true))
        {
            // Components inside prefab instances belong to the prefab asset
            if (!PrefabUtility.IsPartOfPrefabInstance(c)) Object.DestroyImmediate(c);
        }
    }

    // ================================================================== small helpers

    class GameRunning
    {
        public BoolVariable flag = Load<BoolVariable>(CleanArchAssetBuilder.Variables + "/GameRunning.asset");
        public BoolVariable invisible = Load<BoolVariable>(CleanArchAssetBuilder.Variables + "/CatInvisible.asset");
    }

    static GameObject MakeSceneChanger(Scene scene, GameObject parent = null)
    {
        GameObject go = parent != null ? Child(parent, "SceneChanger") : (GetRoot(scene, "SceneChanger") ?? new GameObject("SceneChanger"));
        if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
        SceneChanger changer = GetOrAdd<SceneChanger>(go);
        changer.loadingTarget = Load<StringVariable>(CleanArchAssetBuilder.Variables + "/LoadingTarget.asset");
        EditorUtility.SetDirty(changer);

        foreach (StringGameEventListener old in go.GetComponents<StringGameEventListener>()) Object.DestroyImmediate(old);
        foreach (SimpleGameEventListener old in go.GetComponents<SimpleGameEventListener>()) Object.DestroyImmediate(old);
        ListenString(go, "SceneRequested", Str(changer, "OnSceneRequested"));
        Listen(go, "MainMenuRequested", V(changer, "OnMainMenuRequested"));
        return go;
    }

    static AudioSource EnsureAudioChild(GameObject parent, string name, string clipFile)
    {
        Transform t = parent.transform.Find(name);
        GameObject go = t != null ? t.gameObject : new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        AudioSource a = GetOrAdd<AudioSource>(go);
        a.playOnAwake = false;
        a.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Sound/" + clipFile);
        EditorUtility.SetDirty(a);
        return a;
    }

    static T Load<T>(string path) where T : Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) Debug.LogError("CleanSceneBuilder: missing asset " + path);
        return asset;
    }

    static T Ev<T>(string name) where T : Object
    {
        return Load<T>(CleanArchAssetBuilder.Events + "/" + name + ".asset");
    }

    static IntVariable GameScore() { return Load<IntVariable>("Assets/Scripts/ScriptableObjects/GameScore.asset"); }
    static GameConstants Constants() { return Load<GameConstants>("Assets/Scripts/ScriptableObjects/CatHuntConstant.asset"); }

    static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    static GameObject Child(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) return t.gameObject;
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    static GameObject GetRoot(Scene scene, string name)
    {
        foreach (GameObject g in scene.GetRootGameObjects()) if (g.name == name) return g;
        return null;
    }

    static void DeleteRoot(Scene scene, string name)
    {
        GameObject g = GetRoot(scene, name);
        if (g != null) Object.DestroyImmediate(g);
    }

    static GameObject RenameRoot(Scene scene, string oldName, string newName)
    {
        GameObject g = GetRoot(scene, oldName) ?? GetRoot(scene, newName);
        if (g != null) g.name = newName;
        return g;
    }

    static GameObject FindOrNull(Scene scene, string path)
    {
        string[] parts = path.Split('/');
        GameObject current = GetRoot(scene, parts[0]);
        for (int i = 1; current != null && i < parts.Length; i++)
        {
            Transform t = current.transform.Find(parts[i]);
            current = t != null ? t.gameObject : null;
        }
        return current;
    }

    static GameObject Find(Scene scene, string path)
    {
        GameObject g = FindOrNull(scene, path);
        if (g == null) throw new Exception("CleanSceneBuilder: cannot find " + path + " in " + scene.name);
        return g;
    }

    static Scene OpenScene(string name)
    {
        // OpenScene in Single mode discards unsaved changes of a half-built scene from an earlier failed run
        return EditorSceneManager.OpenScene(SceneDir + name + ".unity", OpenSceneMode.Single);
    }

    static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("saved " + scene.name);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    static void ClearClicks(Button button)
    {
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }
        EditorUtility.SetDirty(button);
    }

    // ---- delegates for persistent listeners

    static UnityAction V(Object target, string method)
    {
        return (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, method);
    }

    static UnityAction<int> I(Object target, string method)
    {
        return (UnityAction<int>)Delegate.CreateDelegate(typeof(UnityAction<int>), target, method);
    }

    static UnityAction<string> Str(Object target, string method)
    {
        return (UnityAction<string>)Delegate.CreateDelegate(typeof(UnityAction<string>), target, method);
    }

    static UnityAction<string> S(Object target, string method)
    {
        return Str(target, method);
    }

    static UnityAction<IPowerup> P(Object target, string method)
    {
        return (UnityAction<IPowerup>)Delegate.CreateDelegate(typeof(UnityAction<IPowerup>), target, method);
    }

    // ---- listener components

    static void Listen(GameObject go, string eventName, params UnityAction[] calls)
    {
        SimpleGameEventListener l = go.AddComponent<SimpleGameEventListener>();
        l.gameEvent = Ev<SimpleGameEvent>(eventName);
        foreach (UnityAction call in calls) UnityEventTools.AddVoidPersistentListener(l.response, call);
        EditorUtility.SetDirty(l);
    }

    static void ListenInt(GameObject go, string eventName, UnityAction<int> call)
    {
        IntGameEventListener l = go.AddComponent<IntGameEventListener>();
        l.gameEvent = Ev<IntGameEvent>(eventName);
        UnityEventTools.AddPersistentListener(l.response, call);
        EditorUtility.SetDirty(l);
    }

    static void ListenString(GameObject go, string eventName, UnityAction<string> call)
    {
        StringGameEventListener l = go.AddComponent<StringGameEventListener>();
        l.gameEvent = Ev<StringGameEvent>(eventName);
        UnityEventTools.AddPersistentListener(l.response, call);
        EditorUtility.SetDirty(l);
    }
}
#endif
