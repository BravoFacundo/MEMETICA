using System.Collections;
using UnityEngine;

// The white (marble) pedestal: it waits for one meme, its Key. While it charges the meme window sweeps through its
// images; then right (its way rises, the meme goes into the MemeDex, optionally downloaded) or wrong (pushed out).
// Its clue is a painting on an easel (Hint): the easel shows one of the Key's hint videos (Painting > Hint Index).
// Without a Hint dragged in, it takes the first painting on its own island.
// Taken off after being right, its bridges go down again (as a black one); a wrong meme changes nothing.
public class KeyPedestal : Pedestal
{
    [Section("Key")]
    [Tooltip("The meme it waits for (one of our MemeData: the meme window has to be showing it)")]
    public MemeData key;
    [Tooltip("Orbs (OriginalGame): the meme is right if its ID starts with this (Meme_3Aa → ID 3Aa). Used if there is no Key")]
    public string keyId;
    [Tooltip("Game: right, the meme window empties (its meme is downloaded)")]
    public bool downloadMeme = false;
    [Tooltip("The painting (easel) with its clue. Empty: the first painting on its island")]
    public Painting hint;

    public override Transform Hint => hint != null ? hint.transform : null;

    protected override bool IsWhite => true;

    protected override void Awake()
    {
        base.Awake();
        if (hint == null) hint = PaintingOnIsland();   // (before the paintings' Start: they read their clue from it)
    }

    protected override void Start()
    {
        base.Start();
        WarnIfNoClue();
    }

    // The first painting on its island (the island's root, any depth) that no other pedestal claims
    Painting PaintingOnIsland()
    {
        Transform island = Island.Of(transform);
        if (island == null) return null;
        foreach (Painting painting in island.GetComponentsInChildren<Painting>(true))
        {
            Pedestal other = WithClue(painting.transform);
            if (other == null || other == this) return painting;
        }
        return null;
    }

    // A white pedestal waits for a meme that has a painting (the clue); a meme without one belongs on black ones
    void WarnIfNoClue()
    {
        if (key != null) return;   // a MemeData: the meme window shows it when it carries it
        Meme meme = Meme.WithKey(keyId);
        if (meme == null) Debug.LogWarning($"[KeyPedestal] '{name}': no Key, and no meme with Key ID '{keyId}' in the scene.", this);
        else if (meme.Data != null && !meme.Data.HasPainting)
            Debug.LogWarning($"[KeyPedestal] '{name}' waits for '{meme.name}', whose meme '{meme.Data.title}' has no painting (MemeData: Hints). " +
                             "Give it one, or use a black pedestal.", this);
    }

    // The one it waits for: showing its Key (if set), else its ID starts with Key ID
    public bool Fits(GameObject memeObject)
    {
        if (memeObject == null) return false;
        if (key != null) return memeObject.TryGetComponent(out Meme meme) && meme.Data == key;
        return !string.IsNullOrEmpty(keyId) && Meme.IDOf(memeObject).StartsWith(keyId);
    }

    // The sweep of images while it charges, then it decides
    protected override IEnumerator Judge()
    {
        Play(chargeClip);
        Think(true);
        if (CurrentMeme != null) CurrentMeme.Sweep(Length(chargeClip));
        yield return new WaitForSeconds(Length(chargeClip));
        if (Fits(MemeObject)) SetCorrect();
        else SetIncorrect();
    }

    protected override void OnRight()
    {
        if (CurrentMeme != null && CurrentMeme.Data != null) MemeDex.Report(CurrentMeme.Data, CurrentMeme.Data.baseImage);   // its Base into the MemeDex
        if (downloadMeme && CurrentMeme is MemeChanger window) window.SetMeme(null);   // downloaded
    }

    protected override bool LowersWhenTakenOff(GameObject meme) => Fits(meme);

    // Snap In Range: carrying its Key within reach, it goes in without looking
    protected override bool AutoSnapsWhen(GameObject inHand, bool inRange) => inRange && key != null && Fits(inHand);

#if UNITY_EDITOR
    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (hint == null || socket == null) return;
        Gizmos.color = new Color(0.5f, 0.8f, 1f);   // to its clue
        Gizmos.DrawLine(socket.position, hint.transform.position);
    }
#endif
}
