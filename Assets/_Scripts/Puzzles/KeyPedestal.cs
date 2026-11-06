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
    [Tooltip("The meme it waits for (one of our MemeData: the meme window or orb has to be showing it)")]
    public MemeData key;
    // Old (orbs by name: Meme_1Aa → "1A"). Opening the scene in the Editor turns it into Key (OnValidate); delete once
    // OriginalGame was opened and saved
    [HideInInspector] public string keyId;
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

    // A white pedestal waits for a meme; with an easel, that meme needs a painting (the clue)
    void WarnIfNoClue()
    {
        if (key == null) Debug.LogWarning($"[KeyPedestal] '{name}' has no Key (the meme it waits for).", this);
        else if (hint != null && !key.HasPainting)
            Debug.LogWarning($"[KeyPedestal] '{name}' waits for '{key.name}', which has no painting (MemeData: Hints) for its easel.", this);
    }

    // The one it waits for: showing its Key
    public bool Fits(GameObject memeObject) =>
        key != null && memeObject != null && memeObject.TryGetComponent(out Meme meme) && meme.Data == key;

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
    // The old Key ID → Key: the meme of the orb whose name starts with Meme_<Key ID> (once, when the scene opens)
    protected override void OnValidate()
    {
        base.OnValidate();
        if (key != null || string.IsNullOrEmpty(keyId) || Application.isPlaying) return;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null || key != null || string.IsNullOrEmpty(keyId)) return;
            foreach (Meme meme in FindObjectsOfType<Meme>(true))
            {
                if (meme.gameObject.scene != gameObject.scene || !meme.name.StartsWith("Meme_" + keyId) || meme.data == null) continue;
                UnityEditor.Undo.RecordObject(this, "Key ID into Key");
                key = meme.data;
                Debug.Log($"[KeyPedestal] '{name}': Key = '{key.name}' (was Key ID '{keyId}', the orb '{meme.name}'). Save the scene.", this);
                keyId = "";
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
                return;
            }
            Debug.LogWarning($"[KeyPedestal] '{name}': Key ID '{keyId}' matches no orb: drag its Key in.", this);
        };
    }

    protected override void OnDrawGizmosSelected()
    {
        base.OnDrawGizmosSelected();
        if (hint == null || socket == null) return;
        Gizmos.color = new Color(0.5f, 0.8f, 1f);   // to its clue
        Gizmos.DrawLine(socket.position, hint.transform.position);
    }
#endif
}
