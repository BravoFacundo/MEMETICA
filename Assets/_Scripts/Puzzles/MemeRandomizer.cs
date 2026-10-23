using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The randomizer: a pedestal that gives the meme window its meme (the hub). Put a meme window (MemeWindow, empty or
// not) on it: the axes still to draw go by on the window, faster and then slower and slower, it stops on one at random,
// the window becomes that meme (MemeChanger.SetMeme) and that axis' islands and bridges rise (the way to its course).
// Drawn axes don't come out again; once every axis was drawn, all of them can again.
// Goes on a Pedestal (black, Keyless): the pedestal snaps the meme and calls Draw instead of judging it.
[RequireComponent(typeof(Pedestal))]
public class MemeRandomizer : MonoBehaviour
{
    [System.Serializable]
    public class Axis
    {
        public MemeData meme;
        [Tooltip("What rises when this axis comes out (IDs, as in a Pedestal)")]
        public string[] islands;
        public string[] bridges;
    }

    [Section("Axes")]
    public List<Axis> axes = new List<Axis>();
    [Tooltip("One after the other (islands, then bridges), from the hub toward the course. Off: all at once")]
    public bool inOrder = true;

    [Section("Raffle")]
    [Tooltip("Seconds the images go by before it stops")]
    public float raffleTime = 3f;
    [Tooltip("Each image going by (empty: MemeSettings' sweep tick)")]
    public AudioClip tickClip;
    [Tooltip("When it stops on the winner (empty: MemeSettings' change sound). The winner's grab sound plays too")]
    public AudioClip winClip;

    private readonly List<Axis> drawn = new List<Axis>();

    void Reset()
    {
        Pedestal pedestal = GetComponent<Pedestal>();
        pedestal.KeylessPedestal = true;   // it doesn't judge, it gives
        pedestal.ApplyVisual();
    }

    // Called by the pedestal once the meme snapped in. Ends when the winner is shown and its way rises
    public IEnumerator Draw(Meme meme)
    {
        if (!(meme is MemeChanger window)) yield break;   // a plain meme (the tutorial orb): nothing to show it on
        List<Axis> left = axes.FindAll(a => a.meme != null && MemeImage.Exists(a.meme.baseImage) && !drawn.Contains(a));
        if (left.Count == 0)
        {
            drawn.Clear();
            left = axes.FindAll(a => a.meme != null && MemeImage.Exists(a.meme.baseImage));
        }
        if (left.Count == 0) { Debug.LogWarning($"[MemeRandomizer] '{name}' has no axes with a meme (MemeData with a base image).", this); yield break; }

        // Never the one it already holds, if there is another
        List<Axis> pool = left.Count > 1 ? left.FindAll(a => a.meme != window.Data) : left;
        Axis winner = pool[Random.Range(0, pool.Count)];
        yield return window.Raffle(left.ConvertAll(a => a.meme), winner.meme, raffleTime, tickClip, winClip);
        if (window.Data != winner.meme) yield break;   // taken off halfway

        drawn.Add(winner);
        if (inOrder) LevelManager.Instance.RaiseInOrder(winner.islands, winner.bridges);
        else
        {
            foreach (string id in winner.islands) LevelManager.Instance.SubirIsla(id);
            foreach (string id in winner.bridges) LevelManager.Instance.SubirPuente(id);
        }
    }
}
