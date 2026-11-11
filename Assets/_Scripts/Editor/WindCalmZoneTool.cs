using UnityEditor;
using UnityEngine;

// Memetica > Audio > Wind Sources To Calm Zones (open scene): every AudioSource playing the light wind (the old way:
// one in each island's middle, heard near it) becomes a WindCalmZone in the same place, its Min Distance → Calm
// Radius and Max Distance → Wind Radius; the source is removed. The AudioManager's own source isn't touched.
// Then WindAmbience (Managers) plays both winds. Ctrl+Z undoes it. Save the scene after.
public static class WindCalmZoneTool
{
    [MenuItem("Memetica/Audio/Wind Sources To Calm Zones (open scene)")]
    static void Convert()
    {
        int made = 0;
        foreach (AudioSource source in Object.FindObjectsOfType<AudioSource>(true))
        {
            if (source.clip == null || !source.clip.name.ToLower().Contains("wind_light")) continue;
            if (source.GetComponent<AudioManager>() != null || source.GetComponentInParent<WindAmbience>() != null) continue;
            GameObject g = source.gameObject;
            WindCalmZone zone = g.GetComponent<WindCalmZone>();
            if (zone == null) zone = Undo.AddComponent<WindCalmZone>(g);
            Undo.RecordObject(zone, "Wind Calm Zone");
            zone.calmRadius = Mathf.Max(0.5f, source.minDistance);
            zone.windRadius = Mathf.Max(zone.calmRadius + 1f, source.maxDistance);
            Undo.DestroyObjectImmediate(source);
            made++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"[Wind] {made} light wind source(s) turned into WindCalmZones. Add WindAmbience to Managers (both clips) " +
                  "and empty the AudioManager's Ambience in Game. Save the scene.");
    }
}
