//In the project, the Player can unlock new Areas in the form of floating Islands that rise from the Void.
//This Script is used simulate in the camera the shaking of a large moving object (The Islands).
//Giving Feedback to the Player about his interaction with an object that they cannot see yet.

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public IEnumerator ShakeTheCamera (float duration, float magnitude)
    {        
        Vector3 originalPosition = transform.localPosition;
        float elapsedTime = 0.0f;
        
        while(elapsedTime < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;

            transform.localPosition = new Vector3(x, y, originalPosition.z);
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        transform.localPosition = originalPosition;
    }
}
