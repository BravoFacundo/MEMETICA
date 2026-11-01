using System;

// Inspector layout for our scripts, drawn by _Editor/SectionedInspector. They only change how the Inspector looks:
// fields keep their names, so no value or reference is ever lost.
//   [Section("Extend From A")]                                     starts a foldable section (until the next one)
//   [Section("Extend From A", nameof(appearance), Appearance.ExtendFromA)]  …only shown when that field has one of those values
//   [Section("Advanced", Closed = true)]                           …starts folded
//   [ShowIf(nameof(overrideTiming))]                               this field only when that bool is on / reference is set
//   [ShowIf(nameof(keyless), false)]                               …or when that field has one of those values
//   [ReadOnlyField]                                                shown but not editable (state, debug)

[AttributeUsage(AttributeTargets.Field)]
public class SectionAttribute : Attribute
{
    public readonly string Name;
    public readonly string When;
    public readonly object[] Values;
    public bool Closed { get; set; }

    public SectionAttribute(string name) { Name = name; }
    public SectionAttribute(string name, string when, params object[] values) { Name = name; When = when; Values = values; }
}

[AttributeUsage(AttributeTargets.Field)]
public class ShowIfAttribute : Attribute
{
    public readonly string Field;
    public readonly object[] Values;
    public ShowIfAttribute(string field, params object[] values) { Field = field; Values = values; }
}

[AttributeUsage(AttributeTargets.Field)]
public class ReadOnlyFieldAttribute : Attribute { }

// A scene picked by dragging its file in the Inspector (drawn by _Editor/SceneFieldDrawer): it keeps the scene's name,
// which is what SceneManager loads, and follows it if the scene is renamed. The scene must be in File > Build Settings.
//   public SceneField menuScene = new SceneField("Menu");   →   SceneManager.LoadScene(menuScene)
[Serializable]
public class SceneField
{
    [UnityEngine.SerializeField] private UnityEngine.Object sceneAsset;   // the scene file (only used in the Editor)
    [UnityEngine.SerializeField] private string sceneName;

    public SceneField(string name) { sceneName = name; }

    public string Name => sceneName;
    public static implicit operator string(SceneField scene) => scene != null ? scene.sceneName : null;
    public override string ToString() => sceneName;
}
