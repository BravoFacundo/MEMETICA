// Mira del juego: decide qué ícono muestra RetroCursor (en modo mira, Follow Mouse ✘).
// Va en el mismo objeto que RetroCursor en la escena Game.
//   Zoom (clic derecho): lupa + mientras acerca, lupa mientras está acercado, lupa − mientras vuelve
//   Mirando un orbe: mano | Sosteniendo un orbe: elegible | Nada: Dot (el punto)
// En pausa no hace nada: ahí el cursor queda libre (lo maneja PauseMenu). Al volver, se re-aplica.

using UnityEngine;

public class Crosshair : MonoBehaviour
{
    [Header("Referencias (ambos están en la Main Camera)")]
    public PlayerLook playerLook;
    public PlayerHoldObjects playerHold;

    [Space(10)]
    [Header("Estados")]
    [Tooltip("Qué se muestra mientras se sostiene un orbe")]
    public CursorState whileHolding = CursorState.Grab;
    [Tooltip("Margen de FOV para dar por terminado el acercamiento o el alejamiento")]
    public float fovMargin = 1f;

    private Camera cam;
    private CursorState current;
    private bool started;

    void Start()
    {
        // Crosshair decides the cursor mode for its scene:
        //   no player (Menu) → free cursor that follows the mouse, and this component turns off
        //   player (Game)    → fixed aim in the center
        bool hasPlayer = playerLook != null && playerHold != null;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetFollowMouse(!hasPlayer);
        if (!hasPlayer) { enabled = false; return; }
        cam = playerLook.GetComponent<Camera>();
    }

    void Update()
    {
        if (PauseMenu.GameIsPaused) { started = false; return; }   // al salir de la pausa vuelve a poner la mira

        CursorState wanted = Decide();
        if (started && wanted == current) return;   // solo avisa cuando cambia
        started = true;
        current = wanted;
        if (RetroCursor.Instance != null) RetroCursor.Instance.SetState(wanted);
    }

    CursorState Decide()
    {
        float fov = cam.fieldOfView;

        if (playerLook.IsZooming)
            return fov > playerLook.ZoomFOV + fovMargin ? CursorState.ZoomIn : CursorState.Zoom;
        if (fov < playerLook.DefaultFOV - fovMargin)
            return CursorState.ZoomOut;

        if (playerHold.heldObject != null) return whileHolding;
        if (playerHold.LookingAtMeme) return CursorState.Grab;
        return CursorState.Dot;
    }
}
