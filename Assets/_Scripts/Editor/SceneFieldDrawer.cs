using System.Linq;
using UnityEditor;
using UnityEngine;

// Draws a SceneField (InspectorAttributes.cs) as a slot for a scene file: drag the scene in, its name is kept. A field that
// only has a name (written before, or by default) finds its scene by that name. Red label: the scene isn't in
// File > Build Settings, so it can't be loaded in a build.
[CustomPropertyDrawer(typeof(SceneField))]
public class SceneFieldDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        SerializedProperty asset = property.FindPropertyRelative("sceneAsset");
        SerializedProperty sceneName = property.FindPropertyRelative("sceneName");

        // Only a name: find the scene with that name
        if (asset.objectReferenceValue == null && !string.IsNullOrEmpty(sceneName.stringValue))
            asset.objectReferenceValue = FindScene(sceneName.stringValue);
        // Renamed scene: the name follows it
        if (asset.objectReferenceValue != null && sceneName.stringValue != asset.objectReferenceValue.name)
            sceneName.stringValue = asset.objectReferenceValue.name;

        bool inBuild = asset.objectReferenceValue == null || InBuild(asset.objectReferenceValue);
        Color color = GUI.color;
        if (!inBuild) { GUI.color = new Color(1f, 0.55f, 0.55f); label.tooltip = "Not in File > Build Settings: it can't be loaded in a build"; }

        EditorGUI.BeginProperty(position, label, property);
        EditorGUI.BeginChangeCheck();
        Object picked = EditorGUI.ObjectField(position, label, asset.objectReferenceValue, typeof(SceneAsset), false);
        if (EditorGUI.EndChangeCheck())
        {
            asset.objectReferenceValue = picked;
            sceneName.stringValue = picked != null ? picked.name : "";
        }
        EditorGUI.EndProperty();
        GUI.color = color;
    }

    static SceneAsset FindScene(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene " + name))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
        return null;
    }

    static bool InBuild(Object scene)
    {
        string path = AssetDatabase.GetAssetPath(scene);
        return EditorBuildSettings.scenes.Any(s => s.enabled && s.path == path);
    }
}
