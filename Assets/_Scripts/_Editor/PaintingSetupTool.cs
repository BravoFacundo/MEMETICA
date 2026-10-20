// TEMP: Memetica > Paintings: set up (temporary). Run it once in each scene (Game_Old and Game), save, then delete this file.
// For every painting: its root is the group around the canvas tagged Pintura (Painting > Canvas, Easel, Chair…),
// or the canvas itself when it sits right in its island.
//   - the Painting component goes on the root, with what its image already has; one anywhere else inside moves up
//   - the canvas board gets the PaintingCanvas material: the bare canvas with the cloth weave
//   - names: the root "Painting_<KeyID of its pedestal>" (or "Painting"), the canvas "Canvas", the video plane "Image"
// Can be run again (it finds what a first run left). Ctrl+Z undoes everything. The Console lists each painting.

using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public static class PaintingSetupTool
{
    private const string BoardMaterialPath = "Assets/_Rendering/PaintingCanvas.mat";
    private const string UndoName = "Paintings set up";

    [MenuItem("Memetica/Paintings: set up (temporary)")]
    public static void SetUp()
    {
        Material boardMaterial = AssetDatabase.LoadAssetAtPath<Material>(BoardMaterialPath);
        if (boardMaterial == null) { Debug.LogError($"[Paintings] No material at {BoardMaterialPath}."); return; }

        Undo.SetCurrentGroupName(UndoName);
        int undoGroup = Undo.GetCurrentGroup();
        var done = new HashSet<Transform>();

        foreach (Transform canvas in Object.FindObjectsOfType<Transform>(true))
        {
            if (!canvas.CompareTag("Pintura")) continue;
            Transform root = RootOf(canvas);
            if (!done.Add(root)) continue;

            VideoPlayer video = root.GetComponentInChildren<VideoPlayer>(true);
            if (video == null) { Debug.LogWarning($"[Paintings] {root.name}: no VideoPlayer inside, skipped.", root); continue; }

            // The component on the root; any other inside (added before on the image or the canvas) gives its choices and goes
            Painting painting = root.GetComponent<Painting>();
            if (painting == null) painting = Undo.AddComponent<Painting>(root.gameObject);
            Undo.RecordObject(painting, UndoName);
            painting.FindParts();
            painting.ReadFromImage();
            foreach (Painting old in root.GetComponentsInChildren<Painting>(true))
            {
                if (old == painting) continue;
                if (old.frame != null) painting.frame = old.frame;
                painting.mirrorX = old.mirrorX;
                painting.mirrorY = old.mirrorY;
                painting.paintEdge = old.paintEdge;
                Undo.DestroyObjectImmediate(old);
            }

            // The board: bare canvas with the cloth
            string before = "none";
            if (painting.board != null)
            {
                before = painting.board.sharedMaterial != null ? painting.board.sharedMaterial.name : "none";
                if (painting.board.sharedMaterial != boardMaterial)
                {
                    Undo.RecordObject(painting.board, UndoName);
                    painting.board.sharedMaterial = boardMaterial;
                }
            }

            // Names that tell them apart
            Pedestal pedestal = Pedestal.WithClue(root);
            Rename(root, pedestal != null && !string.IsNullOrEmpty(pedestal.KeyID) ? $"Painting_{pedestal.KeyID}" : "Painting");
            if (canvas != root) Rename(canvas, "Canvas");
            Rename(video.transform, "Image");

            painting.ApplyImage();
            painting.ApplyBoard();
            PrefabUtility.RecordPrefabInstancePropertyModifications(painting);
            if (painting.board != null) PrefabUtility.RecordPrefabInstancePropertyModifications(painting.board);
            Debug.Log($"[Paintings] {root.name}: set up on '{root.name}' (canvas material before: {before}).", root);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[Paintings] Done: {done.Count} paintings. Save the scene.");
    }

    // The group the canvas is in (Painting > Canvas, Easel, Chair), or the canvas itself if it sits right in its island
    private static Transform RootOf(Transform canvas)
    {
        Transform parent = canvas.parent;
        bool parentIsIsland = parent == null || parent.CompareTag("Isla") || parent.GetComponent<Island>() != null
                              || parent.GetComponent<FloatMotion>() != null || parent.name == "Scenery";
        return parentIsIsland ? canvas : parent;
    }

    private static void Rename(Transform t, string newName)
    {
        if (t.name == newName) return;
        Undo.RecordObject(t.gameObject, UndoName);
        t.gameObject.name = newName;
    }
}
