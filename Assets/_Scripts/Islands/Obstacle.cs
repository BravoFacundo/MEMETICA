using System.Collections.Generic;
using UnityEngine;

// An obstacle to dodge (a floating pillar, a boulder…): never something to stand on. Put it on the obstacle's root,
// with FloatMotion on the same object if it should float (it bobs, drifts and tilts as an island does).
//   Spin    its Spinning child turns slowly and nonstop (FloatMotion keeps the root's own turn: so the model turns)
//   Slide   on its top you aren't on ground (no jump from it) and you slide off, away from its middle
//   Knock   when it moves or turns into you, you are thrown away from it (as the stones that push)
[SelectionBase]   // clicking any of its parts in the Scene view selects this
public class Obstacle : MonoBehaviour
{
    [Section("Spin")]
    [Tooltip("What turns (its model). Empty: its first child")]
    public Transform spinning;
    [Tooltip("Degrees per second (0: still)")]
    public float spinSpeed = 12f;
    [Tooltip("The axis it turns around, in its own space (up: like a top)")]
    public Vector3 spinAxis = Vector3.up;

    [Section("Player")]
    [Tooltip("Meters per second you slide off its top")]
    public float slideSpeed = 5f;
    [Tooltip("Thrown when it moves or turns into you")]
    public bool knock = true;
    [Tooltip("Meters per second you are thrown")]
    public float knockSpeed = 7f;

    private static CharacterController player;
    private static PlayerMove playerMove;
    private Collider[] colliders;
    private float lastKnock = -10f;

    void Start()
    {
        if (spinning == null && transform.childCount > 0) spinning = transform.GetChild(0);
        colliders = GetComponentsInChildren<Collider>();
        if (playerMove == null) { playerMove = FindObjectOfType<PlayerMove>(); if (playerMove != null) player = playerMove.GetComponent<CharacterController>(); }
    }

    void Update()
    {
        if (spinning != null && spinSpeed != 0f) spinning.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
    }

    void LateUpdate()   // (after it floated and turned this frame)
    {
        if (!knock || player == null || !player.enabled || Time.time - lastKnock < 0.4f) return;
        Vector3 body = player.transform.position;
        foreach (Collider c in colliders)
        {
            if (c == null || !c.enabled || c.isTrigger) continue;
            if (!Physics.ComputePenetration(player, body, player.transform.rotation, c, c.transform.position, c.transform.rotation, out Vector3 away, out float depth)) continue;
            if (depth < 0.03f || away.y > 0.7f) continue;   // just touching, or on its top (Slide does that)
            Vector3 flat = new Vector3(away.x, 0f, away.z);
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.ProjectOnPlane(body - transform.position, Vector3.up);
            playerMove.Knockback(flat.normalized * knockSpeed + Vector3.up * 2f);
            lastKnock = Time.time;
            return;
        }
    }
}
