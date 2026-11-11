using UnityEngine;

// The wind you hear (Game): the strong wind all the time, turning into the light one as you get into a calm spot
// (WindCalmZone: an island's middle). Two loops for the whole game, heard everywhere (2D), crossfading: the edge of an
// island, a parkour, falling into the void: strong; the middle of an island: light. Through the mixer's Sfx (its
// slider), and lower while a meme sounds (as the world's sounds).
// Goes on Managers (one per scene). It replaces the old light wind sources in each island (Memetica > Audio > Wind
// Sources To Calm Zones) and the AudioManager's Ambience in Game.
public class WindAmbience : MonoBehaviour
{
    public AudioClip strongWind;
    public AudioClip lightWind;
    [Range(0f, 1f)] public float strongVolume = 0.6f;
    [Range(0f, 1f)] public float lightVolume = 0.5f;
    [Tooltip("How fast it turns from one to the other (per second: 1 = a second from all strong to all light)")]
    public float blendSpeed = 1.2f;

    private AudioSource strong, calmSource;
    private float calm;
    private Camera cam;

    void Start()
    {
        strong = MakeLoop(strongWind, "Wind (strong)");
        calmSource = MakeLoop(lightWind, "Wind (light)");
        calm = Target();   // (as it is here from the first frame)
        Apply();
    }

    AudioSource MakeLoop(AudioClip clip, string label)
    {
        if (clip == null) return null;
        var go = new GameObject(label);
        go.transform.SetParent(transform, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // (heard everywhere)
        source.volume = 0f;
        AudioManager.Route(source);
        source.Play();
        source.time = Random.Range(0f, clip.length);   // (not both from their beginning)
        return source;
    }

    void Update()
    {
        calm = Mathf.MoveTowards(calm, Target(), blendSpeed * Time.unscaledDeltaTime);
        Apply();
    }

    // How calm it is where you are: the calmest spot that reaches you
    float Target()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return 0f;
        Vector3 here = cam.transform.position;
        float best = 0f;
        foreach (WindCalmZone zone in WindCalmZone.All) best = Mathf.Max(best, zone.CalmAt(here));
        return best;
    }

    // Equal power: the sum keeps its loudness halfway
    void Apply()
    {
        float duck = AudioManager.WorldVolume;
        float a = calm * Mathf.PI * 0.5f;
        if (strong != null) strong.volume = strongVolume * Mathf.Cos(a) * duck;
        if (calmSource != null) calmSource.volume = lightVolume * Mathf.Sin(a) * duck;
    }
}
