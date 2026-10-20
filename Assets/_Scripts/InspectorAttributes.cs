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
