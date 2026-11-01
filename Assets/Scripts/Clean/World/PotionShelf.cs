using UnityEngine;

/// <summary>
/// Holds the potions sitting on a bookshelf. DropOne (called when a super dash hits the shelf) lets one of
/// them fall to the ground, picked at random.
/// </summary>
public class PotionShelf : MonoBehaviour
{
    private PotionItem[] potions;

    void Awake()
    {
        potions = GetComponentsInChildren<PotionItem>(true);
    }

    public void DropOne()
    {
        var onShelf = new System.Collections.Generic.List<PotionItem>();
        foreach (PotionItem p in potions) if (p != null && p.IsOnShelf) onShelf.Add(p);
        if (onShelf.Count == 0) return;

        onShelf[Random.Range(0, onShelf.Count)].Drop();
    }

    // GameRestart response: every potion goes back onto the shelf
    public void ResetState()
    {
        foreach (PotionItem p in potions) if (p != null) p.ResetState();
    }
}
