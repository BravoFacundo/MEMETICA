// Cursor del SO retro: maneja qué imagen muestra el mouse según el estado.
// Va en un objeto vacío en la raíz de la escena del menú (por ejemplo "RetroCursor").
// Sobrevive al cambio de escena para que el relojito siga mientras carga el juego.
//
// Cómo se usa desde otros scripts:
//   RetroCursor.Instance.BeginBusy();  ...  RetroCursor.Instance.EndBusy();   // relojito
//   RetroCursor.Instance.SetHover(CursorState.Link);  /  ClearHover();        // lo maneja CursorHover

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum CursorState { Default, Link, Busy, Text, Move, Resize, Forbidden }

public class RetroCursor : MonoBehaviour
{
    [System.Serializable]
    public class CursorStyle
    {
        public CursorState state;
        public Texture2D[] frames;            // 1 imagen = cursor fijo; varias = animado (relojito)
        public Vector2 hotspot;               // el punto que "hace clic", en píxeles desde arriba a la izquierda
        public float framesPerSecond = 8f;
    }

    public static RetroCursor Instance { get; private set; }

    [Tooltip("Un estilo por estado. Los estados sin imagen usan el Default; si tampoco hay Default, el cursor de Windows.")]
    public List<CursorStyle> styles = new List<CursorStyle>();

    private readonly Dictionary<CursorState, CursorStyle> lookup = new Dictionary<CursorState, CursorStyle>();
    private CursorState hoverState = CursorState.Default;  // lo que pide el elemento que está bajo el mouse
    private int busyCount;                                  // mientras sea mayor a 0, manda el relojito
    private CursorStyle current;
    private int frame;
    private float timer;

    void Awake()
    {
        // Uno solo en todo el juego, aunque se vuelva a cargar el menú
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);   // el objeto tiene que estar en la raíz de la jerarquía

        foreach (CursorStyle s in styles)
            if (s.frames != null && s.frames.Length > 0) lookup[s.state] = s;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Apply();
    }

    void Update()
    {
        // Solo los cursores animados necesitan trabajo por frame
        if (current == null || current.frames.Length < 2) return;

        timer += Time.unscaledDeltaTime;   // unscaled: sigue animando aunque el juego esté en pausa
        if (timer >= 1f / current.framesPerSecond)
        {
            timer = 0f;
            frame = (frame + 1) % current.frames.Length;
            Cursor.SetCursor(current.frames[frame], current.hotspot, CursorMode.Auto);
        }
    }

    // ---------- API ----------

    public void SetHover(CursorState state) { hoverState = state; Apply(); }
    public void ClearHover() { hoverState = CursorState.Default; Apply(); }

    public void BeginBusy() { busyCount++; Apply(); }
    public void EndBusy() { busyCount = Mathf.Max(0, busyCount - 1); Apply(); }

    // ---------- Interno ----------

    void Apply()
    {
        CursorState wanted = busyCount > 0 ? CursorState.Busy : hoverState;

        CursorStyle style;
        if (!lookup.TryGetValue(wanted, out style)) lookup.TryGetValue(CursorState.Default, out style);
        if (style == current) return;   // ya está puesto: no reiniciar la animación

        current = style;
        frame = 0; timer = 0f;
        if (style == null) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);   // cursor del sistema
        else Cursor.SetCursor(style.frames[0], style.hotspot, CursorMode.Auto);
    }

    // Para probar desde el Inspector: clic derecho en el componente → "Probar relojito 3 s"
    [ContextMenu("Probar relojito 3 s")]
    void TestBusy() { if (Application.isPlaying) StartCoroutine(TestBusyRoutine()); }

    IEnumerator TestBusyRoutine()
    {
        BeginBusy();
        yield return new WaitForSecondsRealtime(3f);
        EndBusy();
    }
}
