using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The cheese thief game. One cheese appears at a random cheese spot. After a delay a rat comes out of a
/// random open rat hole, grabs the cheese and runs back into any open hole. Slashing the rat catches it.
/// Catch enough rats to win; lose enough cheeses and the game is lost.
/// Everything it needs is in the Inspector or found in the scene; no singletons. PreyCaught (from the cat) and
/// GameRestart reach it through event listeners on the same object.
/// </summary>
public class CheeseGame : MonoBehaviour
{
    public enum Outcome { None, Caught, Escaped, GaveUp }

    [Header("Rules")]
    public int ratsToWin = 3;
    public int cheeseToLose = 3;
    [Tooltip("Seconds between the cheese appearing and the rat coming out.")]
    public float ratDelay = 5f;
    [Tooltip("Seconds between the end of a round and the next cheese.")]
    public float roundGap = 1.5f;

    [Header("Scene")]
    public CheeseRat ratPrefab;
    [Tooltip("Filled from the scene when empty.")]
    public List<CheeseSpot> spots = new List<CheeseSpot>();
    public List<RatHole> holes = new List<RatHole>();
    public Transform cat;
    public BoolVariable catInvisible;
    [Tooltip("Shared flag: true while the game is being played (the pause menu and scoring read it).")]
    public BoolVariable gameRunning;

    [Header("Events")]
    public UnityEvent onReset = new UnityEvent();
    public UnityEvent onCheeseAppeared = new UnityEvent();
    public UnityEvent onRatSpawned = new UnityEvent();
    public UnityEvent onRatCaught = new UnityEvent();
    public UnityEvent onCheeseLost = new UnityEvent();
    public UnityEvent onWin = new UnityEvent();
    public UnityEvent onLose = new UnityEvent();

    public int RatsCaught { get; private set; }
    public int CheeseLost { get; private set; }
    public bool Ended { get; private set; }
    public bool Won { get; private set; }
    public CheeseSpot ActiveSpot { get; private set; }
    public CheeseRat ActiveRat { get; private set; }
    public float TimeToRat { get; private set; }
    public PlatformNavGraph Graph { get; private set; }

    private Outcome outcome;
    private CheeseSpot lastSpot;
    private Coroutine loop;

    void Start()
    {
        if (spots.Count == 0) spots.AddRange(FindObjectsByType<CheeseSpot>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (holes.Count == 0) holes.AddRange(FindObjectsByType<RatHole>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        if (cat == null)
        {
            CatMotor motor = FindFirstObjectByType<CatMotor>();
            if (motor != null) cat = motor.transform;
        }
        if (ratPrefab == null)
        {
            Debug.LogError("CheeseGame: no rat prefab assigned.", this);
            return;
        }

        BuildGraph();
        if (gameRunning != null) gameRunning.SetValue(true);
        loop = StartCoroutine(RoundLoop());
    }

    private void BuildGraph()
    {
        // The graph holds the moves the rat can make, so it is built from the rat's own settings
        float started = Time.realtimeSinceStartup;
        Graph = PlatformNavGraph.Build(ratPrefab.nav, CheeseRat.BodySizeOf(ratPrefab.gameObject));

        int edges = 0;
        foreach (PlatformNavGraph.Node n in Graph.nodes) edges += n.edges.Count;
        Debug.Log("CheeseGame: nav graph " + Graph.nodes.Count + " points, " + edges + " moves, built in " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("F0") + " ms.", this);

        foreach (RatHole hole in holes)
        {
            hole.EntryNode = Graph.Nearest(hole.transform.position, 1.5f, 6f, 1.5f);
            if (hole.EntryNode == null) Debug.LogWarning("CheeseGame: no standing point near " + hole.name + "; rats cannot use it.", hole);
        }
    }

    // ------------------------------------------------------------------ rounds

    private IEnumerator RoundLoop()
    {
        yield return null;
        while (!Ended)
        {
            CheeseSpot spot = PickSpot();
            if (spot == null)
            {
                Debug.LogError("CheeseGame: no cheese spot. Add CheeseSpot components to the cheese objects.", this);
                yield break;
            }

            ActiveSpot = spot;
            lastSpot = spot;
            spot.Show();
            onCheeseAppeared.Invoke();

            TimeToRat = ratDelay;
            while (TimeToRat > 0f)
            {
                TimeToRat -= Time.deltaTime;
                yield return null;
            }
            TimeToRat = 0f;

            RatHole hole = PickHole(spot);
            if (hole == null)
            {
                Debug.LogError("CheeseGame: no rat hole to spawn from.", this);
                yield break;
            }

            outcome = Outcome.None;
            SpawnRat(hole, spot);
            while (outcome == Outcome.None) yield return null;

            spot.Hide();
            ActiveSpot = null;
            if (ActiveRat != null) Destroy(ActiveRat.gameObject);
            ActiveRat = null;

            if (Ended) yield break;
            yield return new WaitForSeconds(roundGap);
        }
    }

    private void SpawnRat(RatHole hole, CheeseSpot spot)
    {
        CheeseRat rat = Instantiate(ratPrefab);
        rat.name = "CheeseRat";
        rat.cat = cat;
        rat.CheesePosition = () => spot.Position;
        rat.OpenHoles = OpenHoles;
        rat.CanEnterHole = h => !h.IsCovered || IsLastResort(h);
        rat.CatIsHidden = () => catInvisible != null && catInvisible.Value;
        rat.OnGrabbedCheese = r => { if (ActiveSpot != null) ActiveSpot.Hide(); };
        rat.OnReachedHole = (r, h) => { if (outcome == Outcome.None) RatEscaped(); };
        rat.OnGaveUp = r => { if (outcome == Outcome.None) outcome = Outcome.GaveUp; };
        rat.Init(Graph);

        Vector2 feet = hole.EntryNode != null ? hole.EntryNode.pos : (Vector2)hole.transform.position;
        rat.PlaceFeetAt(feet);
        ActiveRat = rat;
        onRatSpawned.Invoke();
    }

    private void RatEscaped()
    {
        CheeseLost++;
        outcome = Outcome.Escaped;
        onCheeseLost.Invoke();
        if (CheeseLost >= cheeseToLose) End(false);
    }

    // PreyCaught response (the slashing cat touched a rat)
    public void OnPreyCaught()
    {
        if (Ended || ActiveRat == null || outcome != Outcome.None) return;

        RatsCaught++;
        outcome = Outcome.Caught;
        // A cheese the rat was carrying is back on its spot, but the round is over either way
        onRatCaught.Invoke();
        if (RatsCaught >= ratsToWin) End(true);
    }

    private void End(bool won)
    {
        Ended = true;
        Won = won;
        if (gameRunning != null) gameRunning.SetValue(false);
        // Time keeps running: the player now walks to the rat hole that leads on to the next scene
        (won ? onWin : onLose).Invoke();
    }

    // GameRestart response
    public void ResetState()
    {
        if (loop != null) StopCoroutine(loop);
        if (ActiveRat != null) Destroy(ActiveRat.gameObject);
        ActiveRat = null;
        foreach (CheeseSpot s in spots) if (s != null) s.Hide();
        ActiveSpot = null;
        RatsCaught = 0;
        CheeseLost = 0;
        Ended = false;
        Won = false;
        outcome = Outcome.None;
        Time.timeScale = 1f;
        if (gameRunning != null) gameRunning.SetValue(true);
        if (Graph != null) loop = StartCoroutine(RoundLoop());
        onReset.Invoke();
    }

    // ------------------------------------------------------------------ picking

    private RatHole LowestHole()
    {
        RatHole lowest = null;
        foreach (RatHole h in holes)
            if (h != null && (lowest == null || h.transform.position.y < lowest.transform.position.y)) lowest = h;
        return lowest;
    }

    // The lowest hole is used when every hole is covered, so a rat can always come out and go back in
    private bool IsLastResort(RatHole h)
    {
        if (h != LowestHole()) return false;
        foreach (RatHole other in holes) if (other != null && !other.IsCovered) return false;
        return true;
    }

    private IList<RatHole> OpenHoles()
    {
        // A new list each time: callers loop over it while calling other methods that ask for it again
        List<RatHole> open = new List<RatHole>();
        foreach (RatHole h in holes) if (h != null && !h.IsCovered) open.Add(h);
        if (open.Count == 0)
        {
            RatHole low = LowestHole();
            if (low != null) open.Add(low);
        }
        return open;
    }

    private HashSet<int> ReachFrom(PlatformNavGraph.Node n)
    {
        if (n == null) return new HashSet<int>();
        return Graph.Reachable(n);      // not cached: a gate (armor) that has been knocked down opens new ways
    }

    // A rat can come out of 'hole', reach the spot, and get back into one of the open holes
    private bool Workable(RatHole hole, CheeseSpot spot)
    {
        if (hole.EntryNode == null) return false;
        List<PlatformNavGraph.Node> goals = Graph.NodesAround(spot.Position, ratPrefab.grabRange.x * 0.8f, 1.0f, ratPrefab.grabRange.y * 0.95f);
        HashSet<int> out_ = ReachFrom(hole.EntryNode);
        IList<RatHole> open = OpenHoles();

        foreach (PlatformNavGraph.Node cheese in goals)
        {
            if (!out_.Contains(cheese.id)) continue;
            HashSet<int> back = ReachFrom(cheese);
            foreach (RatHole h in open)
                if (h.EntryNode != null && back.Contains(h.EntryNode.id)) return true;
        }
        return false;
    }

    private CheeseSpot PickSpot()
    {
        List<CheeseSpot> candidates = new List<CheeseSpot>();
        IList<RatHole> open = OpenHoles();
        foreach (CheeseSpot s in spots)
        {
            if (s == null) continue;
            foreach (RatHole h in open)
                if (Workable(h, s)) { candidates.Add(s); break; }
        }

        if (candidates.Count == 0)
        {
            Debug.LogWarning("CheeseGame: no cheese spot can be reached by a rat from an open hole; picking any spot.", this);
            foreach (CheeseSpot s in spots) if (s != null) candidates.Add(s);
        }
        if (candidates.Count > 1) candidates.Remove(lastSpot);
        return candidates.Count == 0 ? null : candidates[Random.Range(0, candidates.Count)];
    }

    private RatHole PickHole(CheeseSpot spot)
    {
        List<RatHole> working = new List<RatHole>();
        IList<RatHole> open = OpenHoles();
        foreach (RatHole h in open) if (Workable(h, spot)) working.Add(h);
        if (working.Count == 0) foreach (RatHole h in open) working.Add(h);
        return working.Count == 0 ? null : working[Random.Range(0, working.Count)];
    }
}
