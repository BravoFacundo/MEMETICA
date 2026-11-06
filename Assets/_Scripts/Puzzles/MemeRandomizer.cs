using System.Collections.Generic;
using UnityEngine;

// Obsolete: the randomizer is RandomizerPedestal now. Only its values are kept here, so Memetica > Check > Migrate
// Pedestals can move them into it (then it removes this component). Once it ran in every scene, delete this file.
public class MemeRandomizer : MonoBehaviour
{
    [System.Serializable]
    public class Axis
    {
        public MemeData meme;
        public string[] sequence = new string[0];
        public Transform aimAt;
    }

    public List<Axis> axes = new List<Axis>();
    public MemeData forceResult;
    public float raffleTime = 3f;
    public AudioClip tickClip;
    public AudioClip winClip;
    public float turnTime = 3f;
    public float targetRise = 2.5f;
    public float islandsAfter = 0.1f;
    public float islandRise = 2f;
    public float islandGap = 0.5f;
    public float bridgesAfter = 0.2f;
    public float bridgeRise = 1.5f;
    public float bridgeGap = 0.1f;
    public float snapRadius = 0f;
    public bool walkToStand = true;
    public Transform standPoint;
    public float standDistance = 1f;
    public float standWalkSpeed = 1.5f;
    public bool lockPlayer = true;
    public bool focusView = true;
    public float focusRadius = -1f;
    public float focusFov = 0f;
}
