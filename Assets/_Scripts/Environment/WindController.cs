using UnityEngine;

// The scene's wind: every "Memetica/Wind Sway" material reads it.
// One per scene. It updates in the editor as values change (no need to press Play).
[ExecuteAlways]
public class WindController : MonoBehaviour
{
    [Header("Wind")]
    public Vector2 direction = new Vector2(1f, 0.3f);    // X, Z
    [Range(0, 1)] public float strength = 0.1f;          // sway
    [Range(0, 5)] public float speed = 1f;
    [Range(0, 0.5f)] public float lean = 0.05f;          // constant lean

    [Header("Height (meters above each object's base)")]
    public float baseHeight = 0.3f;                      // no motion up to here
    public float heightRange = 4f;                       // full strength from here on
    [Range(1, 6)] public float stiffness = 3f;           // more = the lower half of a tree moves less

    [Header("Leaf Flutter")]
    [Range(0, 0.2f)] public float flutterStrength = 0.01f;
    [Range(0, 20)] public float flutterSpeed = 6f;
    [Range(0, 0.95f)] public float flutterStart = 0.6f;  // 0 base, 1 tip
    [Range(0.2f, 4)] public float flutterScale = 1f;     // smaller = more in one block

    public static WindController Instance { get; private set; }

    void OnEnable() { Instance = this; Apply(); }
    void OnValidate() => Apply();

    void Apply()
    {
        Shader.SetGlobalVector("_MW_Direction", new Vector4(direction.x, 0f, direction.y, 0f));
        Shader.SetGlobalFloat("_MW_Strength", strength);
        Shader.SetGlobalFloat("_MW_Speed", speed);
        Shader.SetGlobalFloat("_MW_Lean", lean);
        Shader.SetGlobalFloat("_MW_BaseHeight", baseHeight);
        Shader.SetGlobalFloat("_MW_HeightRange", heightRange);
        Shader.SetGlobalFloat("_MW_Stiffness", stiffness);
        Shader.SetGlobalFloat("_MW_FlutterStrength", flutterStrength);
        Shader.SetGlobalFloat("_MW_FlutterSpeed", flutterSpeed);
        Shader.SetGlobalFloat("_MW_FlutterStart", flutterStart);
        Shader.SetGlobalFloat("_MW_FlutterScale", flutterScale);
    }

    // Direction in the world (so falling leaves follow the same wind)
    public Vector3 WorldDirection => new Vector3(direction.x, 0f, direction.y).normalized;
}
