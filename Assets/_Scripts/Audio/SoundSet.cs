using UnityEngine;

// A list of sounds for one moment (a popup appearing, a click…): each time one of them, at random (never the same one
// twice in a row), a little higher or lower each time. Put one in any script: public SoundSet appearSounds; then
// appearSounds.Play(gameObject). Used by PopupManager (and the cursor, later).
// Each sound plays as it is, or Disguised: changed enough to sound like its own (an imported Windows sound, say):
//   Semitones  higher or lower for good (-3: a bit deeper)
//   Crush      old hardware: fewer bits and a lower sample rate (0 clean, 1 very crunchy 8-bit)
//   Muffle     as if heard through a wall: the highs go away (0 none, 1 a lot)
//   Echo       a short echo (0 none, 1 strong)
// On the screen (not in the world), through the mixer's Sfx (its slider), heard while the game is stopped.
[System.Serializable]
public class SoundSet
{
    [System.Serializable]
    public class Sound
    {
        public AudioClip clip;
        [Tooltip("Off: as it is. On: changed with the values below")]
        public bool disguise;
        [Range(-12, 12)] public int semitones = -3;
        [Range(0f, 1f)] public float crush = 0.4f;
        [Range(0f, 1f)] public float muffle = 0.3f;
        [Range(0f, 1f)] public float echo = 0f;
    }

    [Tooltip("One of these each time, at random (never the same twice in a row). Empty: silent")]
    public Sound[] sounds;
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("Each time a little higher or lower (0.05 = ±5%), so the same sound doesn't feel repeated")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.05f;

    [System.NonSerialized] private AudioSource clean, disguised;
    [System.NonSerialized] private AudioLowPassFilter lowPass;
    [System.NonSerialized] private AudioEchoFilter echoFilter;
    [System.NonSerialized] private BitCrusher crusher;
    [System.NonSerialized] private int last = -1;

    public bool IsEmpty => sounds == null || sounds.Length == 0;

    public void Play(GameObject host)
    {
        if (IsEmpty || host == null) return;
        int pick = Random.Range(0, sounds.Length);
        if (sounds.Length > 1 && pick == last) pick = (pick + 1 + Random.Range(0, sounds.Length - 1)) % sounds.Length;
        last = pick;
        Sound sound = sounds[pick];
        if (sound == null || sound.clip == null) return;
        if (clean == null) MakeSources(host);

        float vary = 1f + Random.Range(-pitchVariation, pitchVariation);
        if (!sound.disguise)
        {
            clean.pitch = vary;
            clean.PlayOneShot(sound.clip, volume);
            return;
        }
        disguised.pitch = Mathf.Pow(2f, sound.semitones / 12f) * vary;
        crusher.amount = sound.crush;
        lowPass.enabled = sound.muffle > 0.001f;
        lowPass.cutoffFrequency = Mathf.Lerp(22000f, 1200f, Mathf.Sqrt(sound.muffle));
        echoFilter.enabled = sound.echo > 0.001f;
        echoFilter.wetMix = sound.echo * 0.6f;
        echoFilter.decayRatio = 0.3f + sound.echo * 0.3f;
        disguised.PlayOneShot(sound.clip, volume);
    }

    // Two sources on a child of the host: one as it is, one with the effects (so a clean sound never goes through them)
    void MakeSources(GameObject host)
    {
        clean = MakeSource(host, "Sound");
        disguised = MakeSource(host, "Sound (disguised)");
        crusher = disguised.gameObject.AddComponent<BitCrusher>();
        lowPass = disguised.gameObject.AddComponent<AudioLowPassFilter>();
        echoFilter = disguised.gameObject.AddComponent<AudioEchoFilter>();
        echoFilter.delay = 90f;
    }

    static AudioSource MakeSource(GameObject host, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(host.transform, false);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;           // (on the screen, not in the world)
        source.ignoreListenerPause = true;  // (heard while the game is stopped for a modal popup)
        AudioManager.Route(source);
        return source;
    }
}
