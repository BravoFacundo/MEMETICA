using UnityEngine;
using UnityEngine.Events;

// Grupo de pedestales enlazados: se completa cuando TODOS tienen un orbe.
// Cada pedestal del grupo lo referencia en su campo "Group" y le avisa cuando entra o sale un orbe.
public class PedestalGroup : MonoBehaviour
{
    public Pedestal[] pedestals;

    [Header("Al completarse")]
    public UnityEvent onComplete;

    bool completed;

    public void Refresh()
    {
        if (completed) return;
        foreach (Pedestal p in pedestals)
            if (p == null || !p.HasOrb) return;

        completed = true;
        foreach (Pedestal p in pedestals) p.PlayCorrect();
        onComplete.Invoke();
    }

#if UNITY_EDITOR
    // Seleccionado: líneas hacia cada pedestal del grupo
    void OnDrawGizmosSelected()
    {
        if (pedestals == null) return;
        Gizmos.color = Color.cyan;
        foreach (Pedestal p in pedestals)
            if (p != null) Gizmos.DrawLine(transform.position, p.transform.position);
    }
#endif
}
