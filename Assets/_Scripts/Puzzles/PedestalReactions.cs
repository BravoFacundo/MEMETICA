using UnityEngine;

// Obsolete: the reactions are part of the Pedestal now (its Reactions section). Only its values are kept here, so
// Memetica > Check > Migrate Pedestals can move them (then it removes this component). Once it ran, delete this file.
public class PedestalReactions : MonoBehaviour
{
    public Sprite like;
    public Sprite dislike;
    public Sprite question;
    public float height = 1.2f;
    public float size = 0.45f;
    public float spread = 0.5f;
    public float riseSpeed = 1f;
    public float lifetime = 1.3f;
    public int burst = 5;
    public float questionRate = 3f;
    public int sortingOrder = 20;
}
