using UnityEngine;

// Viento global de la escena: lo leen todos los materiales "Memetica/Wind Sway".
// Uno solo por escena. Se actualiza en el editor al cambiar valores (no hace falta entrar en Play).
[ExecuteAlways]
public class WindController : MonoBehaviour
{
    [Header("Viento")]
    public Vector2 direction = new Vector2(1f, 0.3f);    // X, Z
    [Range(0, 1)] public float strength = 0.1f;          // balanceo
    [Range(0, 5)] public float speed = 1f;
    [Range(0, 0.5f)] public float lean = 0.05f;          // inclinación constante

    [Header("Altura (metros sobre la base de cada objeto)")]
    public float baseHeight = 0.3f;                      // sin movimiento hasta acá
    public float heightRange = 4f;                       // fuerza completa a partir de acá
    [Range(1, 6)] public float stiffness = 3f;           // más = la mitad del árbol se mueve menos

    [Header("Temblor de hojas")]
    [Range(0, 0.2f)] public float flutterStrength = 0.01f;
    [Range(0, 20)] public float flutterSpeed = 6f;
    [Range(0, 0.95f)] public float flutterStart = 0.6f;  // 0 base, 1 punta
    [Range(0.2f, 4)] public float flutterScale = 1f;     // más chico = más en bloque

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

    // Dirección en mundo (para que las hojas que caen sigan el mismo viento)
    public Vector3 WorldDirection => new Vector3(direction.x, 0f, direction.y).normalized;
}
