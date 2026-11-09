using UnityEngine;

// Old hardware sound on the AudioSource it is next to: fewer bits (a grainy, stepped wave) and a lower sample rate
// (each sample held a few times: dull, metallic). Amount 0: untouched, 1: very crunchy 8-bit.
// Made by SoundSet for its disguised sounds; it can go on any AudioSource by hand too.
public class BitCrusher : MonoBehaviour
{
    [Range(0f, 1f)] public float amount = 0.4f;

    private float[] held = new float[8];
    private int count;

    // (the audio thread: only plain math here)
    void OnAudioFilterRead(float[] data, int channels)
    {
        float a = amount;
        if (a <= 0.001f) return;
        if (held.Length < channels) held = new float[channels];
        float levels = Mathf.Pow(2f, Mathf.Lerp(12f, 4f, a));   // 12 bits down to 4
        int hold = 1 + (int)(a * 6f);                            // the same sample up to 7 times: down to ~1/7 the rate
        for (int i = 0; i < data.Length; i += channels)
        {
            if (count++ % hold == 0)
                for (int c = 0; c < channels; c++) held[c] = Mathf.Round(data[i + c] * levels) / levels;
            for (int c = 0; c < channels; c++) data[i + c] = held[c];
        }
    }
}
