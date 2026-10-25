using UnityEngine;

// What an island (or bridge) is for the meme window. Without it, an island is part of a course.
//   Course    each new one you carry the window onto gives it its next Move (going back to one already visited: nothing)
//   Hub       never gives a Move (you start and come back here)
//   Optional  an optional course (raised by a black pedestal): no Move, no Leave, and your window, left on that black
//             pedestal, doesn't come back to you while you are here (MemeRecall)
// Goes on the island's (or bridge's) root; its children count as part of it.
public class IslandRole : MonoBehaviour
{
    public enum Role { Course, Hub, Optional }

    public Role role = Role.Course;

    // The role of whatever that is part of (no IslandRole above it: Course)
    public static Role Of(Transform t)
    {
        IslandRole found = t != null ? t.GetComponentInParent<IslandRole>() : null;
        return found != null ? found.role : Role.Course;
    }
}
