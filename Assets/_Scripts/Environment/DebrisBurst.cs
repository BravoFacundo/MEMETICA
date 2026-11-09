using UnityEngine;

// A burst of small pieces (rock, grass) that fly out, spin, shrink and disappear.
// Bridges use it when they burst out of an island (Extend From A). Any small prefab works as a piece:
// its colliders and scripts are turned off, it only flies. The meteor uses it too: its rock breaking into chunks.
public class DebrisBurst : MonoBehaviour
{
    private float life, hang;
    private float age;
    private Vector3 thrown;
    // Floating (a stone's trail): floats bobbing and shrinking slowly, then drops a little, shrinking fast to nothing
    private bool floats;
    private float drop, dropSpeed, floatSeed, hangScale;
    private Vector3 floatPlace;

    // One piece that floats where it is (bobbing, shrinking slowly to Hang Scale of its size), Hang seconds, then drops
    // Drop meters, shrinking fast to nothing. In this material and colors (the stone it comes off)
    public static void Float(GameObject prefab, Vector3 position, float size, float hang, float drop, Material material = null,
                             MaterialPropertyBlock look = null, float hangScale = 0.75f)
    {
        if (prefab == null) return;
        GameObject piece = Instantiate(prefab, position, Random.rotation);
        piece.transform.localScale = prefab.transform.localScale * Random.Range(0.8f, 1.1f) * Fit(piece, size);
        foreach (Collider c in piece.GetComponentsInChildren<Collider>()) c.enabled = false;
        foreach (MonoBehaviour b in piece.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;
        foreach (Rigidbody body in piece.GetComponentsInChildren<Rigidbody>()) body.isKinematic = true;
        foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
        {
            if (material != null) r.sharedMaterial = material;
            if (look != null) r.SetPropertyBlock(look);
        }
        DebrisBurst burst = piece.AddComponent<DebrisBurst>();
        burst.floats = true;
        burst.hang = Mathf.Max(hang, 0.01f);
        burst.drop = Mathf.Max(drop, 0.05f);
        burst.hangScale = hangScale;
        burst.floatPlace = position;
        burst.floatSeed = Random.Range(0f, 10f);
        burst.life = float.MaxValue;   // (it goes when it has dropped)
    }
    private Vector3 startScale;

    // direction: where they fly (not normalized = it is fine), speed in m/s, spread: how much each piece strays from it,
    // size: meters wide each piece is (0 = the prefab's own size)
    // life: seconds each piece lasts (0: 0.8 to 1.4, at random). hang: seconds it first stays where it appeared, still,
    // then it is thrown (it shrinks all along)
    // material and look: in this material and these colors (the stone it comes off: the same color)
    public static void Spawn(GameObject[] prefabs, int count, Vector3 position, Vector3 direction, float speed, float spread = 0.5f, float size = 0f, float life = 0f, float hang = 0f,
                             Material material = null, MaterialPropertyBlock look = null)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            if (prefab == null) continue;

            float scatter = size > 0f ? size * 0.5f : 0.6f;   // (sized pieces: close to where they are asked for)
            GameObject piece = Instantiate(prefab, position + Random.insideUnitSphere * scatter, Random.rotation);
            piece.transform.localScale = prefab.transform.localScale * Random.Range(0.5f, 1.1f) * Fit(piece, size);
            foreach (Collider c in piece.GetComponentsInChildren<Collider>()) c.enabled = false;
            if (material != null || look != null)
                foreach (Renderer r in piece.GetComponentsInChildren<Renderer>())
                {
                    if (material != null) r.sharedMaterial = material;
                    if (look != null) r.SetPropertyBlock(look);
                }
            foreach (MonoBehaviour b in piece.GetComponentsInChildren<MonoBehaviour>()) b.enabled = false;

            Rigidbody body = piece.GetComponent<Rigidbody>();
            if (body == null) body = piece.AddComponent<Rigidbody>();
            body.isKinematic = false;
            body.useGravity = true;
            Vector3 flat = Random.insideUnitCircle * spread;   // sideways more than up/down
            body.velocity = (direction.normalized + new Vector3(flat.x, Random.Range(0f, 0.3f), flat.y)) * speed * Random.Range(0.6f, 1.2f);
            body.angularVelocity = Random.insideUnitSphere * 12f;

            DebrisBurst burst = piece.AddComponent<DebrisBurst>();
            burst.life = life > 0f ? life * Random.Range(0.8f, 1.2f) : Random.Range(0.8f, 1.4f);
            if (hang > 0f)
            {
                burst.hang = hang;
                burst.thrown = body.velocity;
                body.isKinematic = true;   // still, until Hang is over
            }
        }
    }

    // How much to scale a piece so it is size meters wide (1 when size is 0)
    static float Fit(GameObject piece, float size)
    {
        if (size <= 0f) return 1f;
        Renderer[] parts = piece.GetComponentsInChildren<Renderer>();
        if (parts.Length == 0) return 1f;
        Bounds b = parts[0].bounds;
        foreach (Renderer r in parts) b.Encapsulate(r.bounds);
        float biggest = Mathf.Max(b.size.x, b.size.y, b.size.z);
        return biggest > 0.0001f ? size / biggest : 1f;
    }

    void Start() => startScale = transform.localScale;

    void Floating()
    {
        age += Time.deltaTime;
        if (age < hang)   // floating: a slow bob and turn, shrinking slowly
        {
            float p = age / hang;
            transform.position = floatPlace + Vector3.up * Mathf.Sin((age + floatSeed) * 3f) * 0.06f;
            transform.Rotate(Vector3.up, 40f * Time.deltaTime, Space.World);
            transform.localScale = startScale * Mathf.Lerp(1f, hangScale, p);
            return;
        }
        // dropping: speeding up, shrinking fast to nothing once it has gone Drop meters
        dropSpeed += 9.8f * Time.deltaTime;
        transform.position += Vector3.down * dropSpeed * Time.deltaTime;
        float fallen = floatPlace.y - transform.position.y;
        float left = 1f - Mathf.Clamp01(fallen / drop);
        transform.localScale = startScale * hangScale * left * left;
        if (left <= 0f) Destroy(gameObject);
    }

    void Update()
    {
        if (floats) { Floating(); return; }
        age += Time.deltaTime;
        if (hang > 0f && age >= hang && TryGetComponent(out Rigidbody body) && body.isKinematic)
        {
            body.isKinematic = false;
            body.velocity = thrown;
        }
        float t = age / life;
        transform.localScale = startScale * (1f - t * t);   // shrinks faster at the end
        if (age >= life) Destroy(gameObject);
    }
}
