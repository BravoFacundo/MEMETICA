// Draws the Inspector of our scripts that use [Section] / [ShowIf] / [ReadOnlyField] (see InspectorAttributes.cs):
// foldable sections (remembered while the editor is open), fields that only show when they matter.
// Scripts without [Section] are drawn exactly as Unity does by default.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MonoBehaviour), true), CanEditMultipleObjects]
public class SectionedInspector : Editor
{
    static readonly Dictionary<Type, bool> usesSections = new Dictionary<Type, bool>();
    static readonly Dictionary<(Type, string), FieldInfo> fields = new Dictionary<(Type, string), FieldInfo>();
    const BindingFlags AllFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    public override void OnInspectorGUI()
    {
        Type type = target.GetType();
        if (!UsesSections(type)) { base.OnInspectorGUI(); return; }

        serializedObject.Update();
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true, sectionShown = true, sectionOpen = true;

        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            if (property.propertyPath == "m_Script")
            {
                using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(property);
                continue;
            }

            FieldInfo field = FindField(type, property.name);
            SectionAttribute section = field?.GetCustomAttribute<SectionAttribute>();
            if (section != null)
            {
                sectionShown = section.When == null || Matches(section.When, section.Values);
                if (sectionShown) sectionOpen = SectionHeader(type, section);
            }
            if (!sectionShown || !sectionOpen) continue;

            ShowIfAttribute showIf = field?.GetCustomAttribute<ShowIfAttribute>();
            if (showIf != null && !Matches(showIf.Field, showIf.Values)) continue;

            bool readOnly = field?.GetCustomAttribute<ReadOnlyFieldAttribute>() != null;
            using (new EditorGUI.DisabledScope(readOnly))
                EditorGUILayout.PropertyField(property, true);
        }

        serializedObject.ApplyModifiedProperties();
    }

    static bool SectionHeader(Type type, SectionAttribute section)
    {
        string key = $"SectionedInspector.{type.FullName}.{section.Name}";
        bool open = SessionState.GetBool(key, !section.Closed);
        EditorGUILayout.Space(6f);
        bool now = EditorGUILayout.Foldout(open, section.Name, true, EditorStyles.foldoutHeader);
        if (now != open) SessionState.SetBool(key, now);
        return now;
    }

    // The other field has one of those values (no values: bool on / reference set / number not 0). Unknown field: shown
    bool Matches(string fieldName, object[] values)
    {
        SerializedProperty other = serializedObject.FindProperty(fieldName);
        if (other == null || other.hasMultipleDifferentValues) return true;
        bool any = values != null && values.Length > 0;

        switch (other.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return any ? values.Any(v => v is bool b && b == other.boolValue) : other.boolValue;
            case SerializedPropertyType.Enum:
                return !any || values.Any(v => Convert.ToInt32(v) == other.intValue);
            case SerializedPropertyType.Integer:
                return any ? values.Any(v => Convert.ToInt64(v) == other.longValue) : other.longValue != 0;
            case SerializedPropertyType.Float:
                return any ? values.Any(v => Convert.ToDouble(v) == other.doubleValue) : other.doubleValue != 0;
            case SerializedPropertyType.String:
                return any ? values.Any(v => (v as string) == other.stringValue) : !string.IsNullOrEmpty(other.stringValue);
            case SerializedPropertyType.ObjectReference:
                return any ? values.Contains(other.objectReferenceValue) : other.objectReferenceValue != null;
            default:
                return true;
        }
    }

    static bool UsesSections(Type type)
    {
        if (usesSections.TryGetValue(type, out bool uses)) return uses;
        uses = false;
        for (Type t = type; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
            if (t.GetFields(AllFields).Any(f => f.GetCustomAttribute<SectionAttribute>() != null)) { uses = true; break; }
        usesSections[type] = uses;
        return uses;
    }

    static FieldInfo FindField(Type type, string name)
    {
        if (fields.TryGetValue((type, name), out FieldInfo found)) return found;
        for (Type t = type; t != null && found == null; t = t.BaseType) found = t.GetField(name, AllFields);
        fields[(type, name)] = found;
        return found;
    }
}
