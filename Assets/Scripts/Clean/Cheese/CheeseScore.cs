using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scoring for the cheese game. The score lives in the shared GameScore asset (so the high score and the main
/// menu keep working) and every change is announced with ScoreChanged.
///   - catching a rat                                   : ratCaughtPoints
///   - first interaction with a mapped item per game    : interactPoints   (chandelier dropped, armor knocked
///                                                         down, stand lamp knocked over)
///   - blocking a rat hole (the first time it is covered): holeBlockedPoints
/// Nothing is scored while the game is not running (after it has been won or lost).
/// </summary>
public class CheeseScore : MonoBehaviour
{
    [Header("Points")]
    public int ratCaughtPoints = 200;
    public int interactPoints = 50;
    public int holeBlockedPoints = 100;

    [Header("Scene")]
    public CheeseGame game;
    [Tooltip("Items that give interactPoints once: FallingObject, SuperDashTarget, CatContactFall.")]
    public List<Component> interactItems = new List<Component>();

    [Header("Shared state")]
    public IntVariable gameScore;
    public IntGameEvent scoreChanged;
    public BoolVariable gameRunning;

    private readonly HashSet<Component> scored = new HashSet<Component>();
    private readonly Dictionary<RatHole, bool> wasCovered = new Dictionary<RatHole, bool>();

    void Start()
    {
        if (game == null) game = GetComponent<CheeseGame>();
        if (game != null) game.onRatCaught.AddListener(OnRatCaught);

        foreach (Component item in interactItems) Subscribe(item);
        ResetState();
    }

    void OnDestroy()
    {
        if (game != null) game.onRatCaught.RemoveListener(OnRatCaught);
    }

    // The three kinds of item announce their interaction in different ways
    private void Subscribe(Component item)
    {
        if (item == null) return;
        if (item is FallingObject f) f.onDropStarted.AddListener(() => ItemUsed(item));
        else if (item is CatContactFall l) l.onFall.AddListener(() => ItemUsed(item));
        else if (item is SuperDashTarget t) t.onHit.AddListener(() => ItemUsed(item));
    }

    private void ItemUsed(Component item)
    {
        if (scored.Add(item)) Add(interactPoints);
    }

    public void OnRatCaught()
    {
        Add(ratCaughtPoints);
    }

    void Update()
    {
        if (game == null) return;

        // A hole counts when it goes from open to covered
        foreach (RatHole hole in game.holes)
        {
            if (hole == null) continue;
            bool covered = hole.IsCovered;
            wasCovered.TryGetValue(hole, out bool before);
            if (covered && !before) Add(holeBlockedPoints);
            wasCovered[hole] = covered;
        }
    }

    public void Add(int points)
    {
        if (gameRunning != null && !gameRunning.Value) return;
        if (gameScore == null) return;
        gameScore.ApplyChange(points);
        Publish();
    }

    // GameRestart response (also run once at the start)
    public void ResetState()
    {
        scored.Clear();
        wasCovered.Clear();
        // Items that are still down when the restart comes in are not counted again once they are back up
        if (game != null) foreach (RatHole hole in game.holes) if (hole != null) wasCovered[hole] = hole.IsCovered;
        if (gameScore != null) gameScore.SetValue(0);
        Publish();
    }

    private void Publish()
    {
        if (scoreChanged != null && gameScore != null) scoreChanged.Raise(gameScore.Value);
    }
}
