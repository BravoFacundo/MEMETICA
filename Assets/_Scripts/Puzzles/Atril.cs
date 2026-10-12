using UnityEngine;

// Va en la Pintura: sabe qué pedestal la controla (asignado en el Inspector).
public class Atril : MonoBehaviour
{
    public Pedestal pedestal;

    public bool IsCorrect => pedestal != null && pedestal.IsCorrect;
}
