// Cursor por software: una Image que reemplaza al cursor de Windows.
// - Menú (Follow Mouse ✔): sigue al mouse y oculta el cursor real.
// - Juego (Follow Mouse ✘): queda fija en el centro como mira; el ícono lo decide la sección Aim (before: Crosshair).
// Aim: con Player Look asignado (escena Game) es mira fija; sin él (Menu) el cursor sigue al mouse.
//   Zoom (right click): magnifier + while zooming in, nothing once fully zoomed (a clean view), magnifier − while zooming out
//   Looking at a meme: hand | Holding a meme: nothing (the meme is seen clean) | Nothing: Dot
//   En pausa no hace nada: ahí el cursor queda libre (lo maneja PauseMenu). Al volver, se re-aplica.
// Default es siempre la flecha. En el juego, la mira usa Dot (el punto) y en la pausa vuelve la flecha.
// El punto de clic de cada cursor es el PIVOT de su sprite
// (flecha y mano que señala → donde apunta la punta, mano que agarra → Center). El cambio de cursor al pasar por un ícono lo hace DesktopIcon.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Point: algo clickeable (íconos) | Grab: algo que se agarra (orbes) | Busy: cargando (lo pone el UIManager)
// NotAllowed: algo interactuable pero bloqueado (no hace nada) | ZoomIn/Zoom/ZoomOut/Dot: mira del juego
// Hidden: no cursor at all (holding a meme, fully zoomed)
public enum CursorState { Default, Grab, Busy, Point, NotAllowed, ZoomIn, Zoom, ZoomOut, Dot, Hidden }

[RequireComponent(typeof(Canvas))]
public class RetroCursor : MonoBehaviour
{
    [System.Serializable]
    public class CursorStyle
    {
        public CursorState state;
        public Sprite sprite;
    }

    public static RetroCursor Instance { get; private set; }

    public Image cursorImage;
    [Tooltip("Menú: sigue al mouse. Juego: queda fija en el centro como mira")]
    public bool followMouse = true;
    public List<CursorStyle> styles = new List<CursorStyle>();

    [Header("Aim (Game only)")]
    [Tooltip("Main Camera. Empty (Menu) = free cursor that follows the mouse")]
    public PlayerLook playerLook;
    [Tooltip("Margen de FOV para dar por terminado el acercamiento o el alejamiento")]
    public float fovMargin = 1f;

    private RectTransform rect;
    private bool busy;   // mientras carga, nada más cambia el cursor
    private Camera aimCamera;
    private CursorState currentAim;
    private bool aimStarted;

    void Awake()
    {
        Instance = this;
        GetComponent<Canvas>().sortingOrder = 999;   // arriba de la UI (el marco va en 1000)
        rect = cursorImage.rectTransform;
        cursorImage.raycastTarget = false;           // que no bloquee clics
        SetState(CursorState.Default);
    }

    // Solo en modo menú se oculta el cursor de Windows (en el juego lo maneja el bloqueo de cámara y la pausa)
    void OnEnable() { if (followMouse) Cursor.visible = false; }
    void OnApplicationFocus(bool focused) { if (focused && followMouse) Cursor.visible = false; }   // Windows lo vuelve a mostrar al cambiar de ventana
    void OnDisable() { if (followMouse) Cursor.visible = true; }

    void Start()
    {
        // The scene decides the mode: player (Game) → fixed aim in the center; no player (Menu) → follows the mouse
        bool hasPlayer = playerLook != null;
        SetFollowMouse(!hasPlayer);
        if (hasPlayer) aimCamera = playerLook.GetComponent<Camera>();
    }

    void Update()
    {
        if (aimCamera == null) return;
        if (PauseMenu.GameIsPaused) { aimStarted = false; return; }   // al salir de la pausa vuelve a poner la mira

        CursorState wanted = DecideAimState();
        if (aimStarted && wanted == currentAim) return;   // solo cambia cuando hace falta
        aimStarted = true;
        currentAim = wanted;
        SetState(wanted);
    }

    void LateUpdate()
    {
        // En un Canvas Overlay, pantalla = posición
        rect.position = followMouse
            ? Input.mousePosition
            : new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
    }

    CursorState DecideAimState()
    {
        float fov = aimCamera.fieldOfView;

        if (playerLook.IsZooming)
            return fov > playerLook.ZoomFOV + fovMargin ? CursorState.ZoomIn : CursorState.Hidden;
        if (fov < playerLook.DefaultFOV - fovMargin)
            return CursorState.ZoomOut;

        if (playerLook.heldObject != null) return CursorState.Hidden;   // dropped: looking at it, the hand comes back
        if (playerLook.LookingAtMeme) return CursorState.Grab;
        return CursorState.Dot;
    }

    // Cambia entre mira fija (juego) y cursor libre (menú y pausa). Lo usa PauseMenu.
    public void SetFollowMouse(bool value)
    {
        followMouse = value;
        if (value)
        {
            Cursor.visible = false;                   // our arrow is shown, not the Windows one
            Cursor.lockState = CursorLockMode.None;   // free mouse (Menu and pause)
        }
    }

    // Cursor de carga: lo pone el UIManager y bloquea cualquier otro cambio hasta que se saque
    public void SetBusy(bool value)
    {
        busy = false;
        SetState(value ? CursorState.Busy : CursorState.Default);
        busy = value;
    }

    public void SetState(CursorState state)
    {
        if (busy) return;
        cursorImage.enabled = state != CursorState.Hidden;
        if (state == CursorState.Hidden) return;

        foreach (CursorStyle s in styles)
        {
            if (s.state != state || s.sprite == null) continue;
            cursorImage.sprite = s.sprite;
            // La Image no usa el pivot del sprite por sí sola: se lo copiamos al RectTransform
            rect.pivot = new Vector2(s.sprite.pivot.x / s.sprite.rect.width, s.sprite.pivot.y / s.sprite.rect.height);
            return;
        }
        Debug.LogWarning("[RetroCursor] No hay sprite asignado para el estado " + state + " en Styles.", this);
    }
}
