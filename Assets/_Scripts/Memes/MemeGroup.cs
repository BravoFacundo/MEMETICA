using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A group of spare memes for the orbs that don't hold a fixed one (Meme: Reserve Group + Reserve Slot).
// Slot 1 takes the first of Reserve, slot 2 the second… With Random Reserve on, every time the scene starts the
// reserve is shuffled, so each slot gets a different meme drawn from all of them (never the same one twice).
[CreateAssetMenu(fileName = "MemeGroup", menuName = "Memetica/Meme Group")]
public class MemeGroup : ScriptableObject
{
    [Tooltip("What this group is (e.g. Expo spares)")]
    public string groupName;
    [Tooltip("The spare memes. Without Random Reserve, slot 1 is the first, slot 2 the second…")]
    public List<MemeData> reserve = new List<MemeData>();
    [Tooltip("Draw the slots at random from the whole reserve each time the scene starts")]
    public bool randomReserve;

    [System.NonSerialized] private List<MemeData> drawn;
    [System.NonSerialized] private int drawnForScene = -1;

    // The meme for that slot (1, 2…), or null if the reserve is that short
    public MemeData Reserve(int slot)
    {
        List<MemeData> order = Order();
        int i = slot - 1;
        return i >= 0 && i < order.Count ? order[i] : null;
    }

    // In list order, or shuffled once per scene load (the same draw for every slot)
    List<MemeData> Order()
    {
        if (!randomReserve) return reserve;
        int scene = SceneManager.GetActiveScene().handle;
        if (drawn != null && drawnForScene == scene) return drawn;

        drawn = reserve.FindAll(m => m != null);
        for (int i = drawn.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (drawn[i], drawn[j]) = (drawn[j], drawn[i]);
        }
        drawnForScene = scene;
        return drawn;
    }
}
