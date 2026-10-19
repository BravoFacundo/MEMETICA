using UnityEngine;

// A burst of small pieces (rock, grass) that fly out, spin, shrink and disappear.
// Bridges use it when they burst out of an island (Extend From A). Any small prefab works as a piece:
// its colliders and scripts are turned off, it only flies.
public class DebrisBurst : MonoBehaviour
{
    private float life;
    private float age;
    private Vector3 startScale;

    // direction: where they fly (not normalized = it is fine), speed in m/s, spread: how much each piece strays from it
    public static void Spawn(GameObject[] prefabs, int count, Vector3 position, Vector3 direction, float speed, float spread = 0.5f)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;

            GameObject piece = Instantiate(prefab, position + Random.insideUnitSphere * 0.6f, Random.rotation);
            piece.transform.localScale = prefab.transform.localScale * Random.Range(0.5f, 1.1f);
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) c.enabled = false;
            foreach (MonoBehaviour b in piece.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;

            Rigidbody body = piece.GetComponent<Rigidbody>();
            if (body == null) body = piece.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            Vector3 flat = Random.insideUnitCircle * spread;   // sideways more than up/down
            body.velocity = (direction.normalized + new Vector3(flat.x, Random.Range(0f, 0.3f), flat.y)) * speed * Random.Range(0.6f, 1.2f);
            body.angularVelocity = Random.insideUnitSphere * 12f;

            piece.AddComponent<DebrisBurst>().life = Random.Range(0.8f, 1.4f);
        }
    }

    void Start() => startScale = transform.localScale;

    void Update()
    {
        age += Time.deltaTime;
        float t = age / life;
        transform.localScale = startScale * (1f - t * t);   // shrinks faster at the end
        if (age >= life) Destroy(gameObject);
    }
}
