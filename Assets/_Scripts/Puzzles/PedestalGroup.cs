using System.Collections.Generic;
using UnityEngine;

// Pedestals that complete together: none of them judges alone; once all of them have a meme, each one raises what it
// raises (before: the same Group ID on each pedestal). Put it anywhere (e.g. on an empty next to them) and drag them in.
public class PedestalGroup : MonoBehaviour
{
    [Tooltip("The pedestals of the group")]
    public List<Pedestal> members = new List<Pedestal>();

    static readonly List<PedestalGroup> all = new List<PedestalGroup>();
    bool completed;

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    // The group that pedestal is in, or null
    public static PedestalGroup Of(Pedestal pedestal) => all.Find(g => g.members.Contains(pedestal));

    // Every member has a meme: all of them are right at once
    public void CompleteIfFull()
    {
        if (completed || members.Exists(p => p != null && !p.HasMeme)) return;
        completed = true;
        foreach (Pedestal member in members) if (member != null) member.CompleteFromGroup();
    }

#if UNITY_EDITOR
    // Selected: a line to each member
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 1f);
        foreach (Pedestal member in members)
            if (member != null) Gizmos.DrawLine(transform.position, member.transform.position);
    }
#endif
}
