using UnityEngine;

// What the player does, from the keyboard and mouse or a gamepad (Xbox layout), in one place:
//   Move    WASD / arrows   | left stick            Look   mouse        | right stick
//   Run     Left Shift      | left stick click      Jump   Space        | A
//   Grab / let go  left click | RT or RB            Zoom   right click  | LT or LB (held)
//   Drop    Q               | B                     Pause  Esc / P      | Start
// A gamepad's sticks have a Dead Zone (a worn one drifts: it never walks you by itself) and are ignored while no
// gamepad is connected (an unplugged one could stay stuck on its last value). Needs the Pad axes in Project Settings >
// Input Manager (Pad Move X/Y, Pad Look X/Y, Pad LT/RT); without them, keyboard and mouse only.
public static class GameInput
{
    const float DeadZone = 0.25f;     // of the stick's travel: below it, nothing
    const float TriggerPress = 0.5f;  // a trigger counts as pressed past this
    const float KeyEase = 3f;         // keys reach full in 1/3 s, as Unity's axes

    //---- Move and look

    // -1..1 each: x right, y forward
    public static Vector2 Move
    {
        get
        {
            Tick();
            Vector2 stick = Stick("Pad Move X", "Pad Move Y");
            Vector2 move = keyMove + stick;
            return Vector2.ClampMagnitude(move, 1f);
        }
    }

    // Degrees to turn this frame from the mouse (times its sensitivity)
    public static Vector2 Mouse => new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

    // -1..1 from the right stick (eased: small moves aim finely); 0 without a gamepad
    public static Vector2 LookStick
    {
        get
        {
            Vector2 stick = Stick("Pad Look X", "Pad Look Y");
            return stick * stick.magnitude;
        }
    }

    public static bool Run => Input.GetKey(KeyCode.LeftShift) || Pad(KeyCode.JoystickButton8);

    //---- Buttons (this frame)

    public static bool JumpDown => Input.GetKeyDown(KeyCode.Space) || Pad(KeyCode.JoystickButton0, true);
    public static bool GrabDown => Input.GetMouseButtonDown(0) || Pad(KeyCode.JoystickButton5, true) || TriggerDown(ref rt, "Pad RT");
    public static bool ZoomHeld => Input.GetMouseButton(1) || Pad(KeyCode.JoystickButton4) || Trigger("Pad LT");
    public static bool DropDown => Input.GetKeyDown(KeyCode.Q) || Pad(KeyCode.JoystickButton1, true);
    public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P) || Pad(KeyCode.JoystickButton7, true);

    //---- Inside

    private static Vector2 keyMove;
    private static int tickedFrame = -1;
    private static bool rt;
    private static int rtFrame = -1;
    private static bool rtDown;

    // Once a frame: the keys eased toward where they point (changing direction starts from 0, as Unity's Snap)
    static void Tick()
    {
        if (tickedFrame == Time.frameCount) return;
        tickedFrame = Time.frameCount;
        keyMove.x = Ease(keyMove.x, Key(KeyCode.D, KeyCode.RightArrow) - Key(KeyCode.A, KeyCode.LeftArrow));
        keyMove.y = Ease(keyMove.y, Key(KeyCode.W, KeyCode.UpArrow) - Key(KeyCode.S, KeyCode.DownArrow));
    }

    static float Key(KeyCode a, KeyCode b) => Input.GetKey(a) || Input.GetKey(b) ? 1f : 0f;

    static float Ease(float value, float target)
    {
        if (target != 0f && value != 0f && Mathf.Sign(target) != Mathf.Sign(value)) value = 0f;
        return Mathf.MoveTowards(value, target, KeyEase * Time.unscaledDeltaTime);
    }

    // A stick, 0 inside the dead zone, then 0..1 from its edge (no jump past it)
    static Vector2 Stick(string x, string y)
    {
        if (!PadConnected) return Vector2.zero;
        var stick = new Vector2(Axis(x), Axis(y));
        float amount = stick.magnitude;
        if (amount < DeadZone) return Vector2.zero;
        return stick / amount * Mathf.Clamp01((amount - DeadZone) / (1f - DeadZone));
    }

    static bool Pad(KeyCode button, bool down = false) => PadConnected && (down ? Input.GetKeyDown(button) : Input.GetKey(button));

    static bool Trigger(string axis) => PadConnected && Axis(axis) > TriggerPress;

    // Pressed this frame (the same answer for every caller in a frame)
    static bool TriggerDown(ref bool wasPressed, string axis)
    {
        if (rtFrame != Time.frameCount)
        {
            rtFrame = Time.frameCount;
            bool pressed = Trigger(axis);
            rtDown = pressed && !wasPressed;
            wasPressed = pressed;
        }
        return rtDown;
    }

    // A gamepad is plugged in now (checked once a second: Unity keeps an unplugged one's name empty)
    private static bool padConnected;
    private static float padCheckedAt = -10f;
    public static bool PadConnected
    {
        get
        {
            if (Time.unscaledTime - padCheckedAt < 1f) return padConnected;
            padCheckedAt = Time.unscaledTime;
            padConnected = false;
            foreach (string name in Input.GetJoystickNames()) if (!string.IsNullOrEmpty(name)) { padConnected = true; break; }
            return padConnected;
        }
    }

    // An axis of the Input Manager, 0 if it isn't there (warns once)
    private static bool missingAxes;
    static float Axis(string name)
    {
        if (missingAxes) return 0f;
        try { return Input.GetAxisRaw(name); }
        catch (System.ArgumentException)
        {
            missingAxes = true;
            Debug.LogWarning($"[GameInput] No '{name}' axis in Project Settings > Input Manager: gamepad sticks and triggers are off.");
            return 0f;
        }
    }
}
