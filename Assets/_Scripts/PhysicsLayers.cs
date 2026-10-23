using UnityEngine;

// Who collides with what, for the layers the code relies on.
//   Props   decoration without a collider of its own before (grass patches, rocks, hills…), made solid by
//           Memetica > Props: solid for memes. Memes (and debris) rest on it and never sink into it; the player walks
//           through it as before (it ignores the player's layer, Ignore Raycast), and the player's own checks (ground,
//           grabbing, paintings) leave it out.
// Set when the game starts, in the editor and in builds.
public static class PhysicsLayers
{
    public const string PropsName = "Props";
    private const string PlayerLayerName = "Ignore Raycast";   // the Player's layer

    private static int props = -2;   // not looked up yet

    // The Props layer, or -1 if the project doesn't have it
    public static int Props => props != -2 ? props : (props = LayerMask.NameToLayer(PropsName));
    public static int PropsMask => Props >= 0 ? 1 << Props : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void SetUp()
    {
        props = -2;
        if (Props < 0) return;   // no props yet (Memetica > Props: solid for memes adds the layer)
        Physics.IgnoreLayerCollision(Props, LayerMask.NameToLayer(PlayerLayerName), true);
    }
}
