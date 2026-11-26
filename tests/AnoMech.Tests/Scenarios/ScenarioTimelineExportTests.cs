using System.Collections;
using System.Globalization;
using System.Reflection;
using AnoMech.Core.Game;

namespace AnoMech.Tests;

// Runs one scenario headless and writes what it sent to the client as a replay-timeline-shaped CSV,
// for .claude/tools/replay_verify_scenario.py to diff against a replay. Parameters:
//   Scenario   full type name
//   Out        CSV path
//   Strat, Seed
//   Overrides  "Name=value|Name=value" (`dotnet test` eats `;`) onto the scenario's SettingsOverrides: a static member of the
//              property's type (Direction=E), an enum name, a number or bool, or a comma list or tuple of those. A list
//              element that is itself a list goes in brackets (ConeTargets=[MainTank],[OffTank,CasterDps,MeleeDpsA],null);
//              a dictionary is Key:value pairs (Debuffs=MainTank:LongSupport,OffTank:ShortSupport).
public class ScenarioTimelineExportTests
{
    [Explicit]
    [Test]
    public void ExportTimeline()
    {
        var name = TestContext.Parameters.Get("Scenario");
        var scenarioType = name is null ? null : typeof(Game).Assembly.GetType(name);
        Assert.That(scenarioType, Is.Not.Null, $"Scenario parameter '{name}' is not a scenario type's full name.");
        var output = TestContext.Parameters.Get("Out");
        Assert.That(output, Is.Not.Null.And.Not.Empty, "Out parameter (CSV path) is required.");
        var overrides = TestContext.Parameters.Get("Overrides") ?? "";

        var run = ScenarioRun.Execute(scenarioType!, TestContext.Parameters.Get("Strat", 0), TestContext.Parameters.Get("Seed", 0),
            new ScenarioRunOptions
            {
                TimelinePath = output,
                // The replay has the BGM; the sim sends it only with scenario BGM on.
                Configure = config => config.SuppressBgm = false,
                Overrides = overrides.Length == 0 ? null : target => Apply(target, overrides),
            });
        TestContext.Out.WriteLine(run.ToString());
        Assert.That(run.Failure, Is.Null, run.ToString);
    }

    private static void Apply(object target, string assignments)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var assignment in assignments.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var (path, text) = (assignment[..assignment.IndexOf('=')].Trim(), assignment[(assignment.IndexOf('=') + 1)..].Trim());
            // A dotted path reaches into a nested object, e.g. a PerRoleSetting's Seats.
            var owner = target;
            var members = path.Split('.');
            foreach (var member in members[..^1])
                owner = Property(owner, member).GetValue(owner)
                        ?? throw new ArgumentException($"{path}: {member} is null.");
            var property = Property(owner, members[^1]);
            var value = Parse(property.PropertyType, text);
            // A get-only array (EndAttacks { get; } = new EndAttack?[4]) is filled in place.
            if (!property.CanWrite && property.GetValue(owner) is Array existing && value is Array items)
                Array.Copy(items, existing, Math.Min(items.Length, existing.Length));
            else
                property.SetValue(owner, value);
        }

        static PropertyInfo Property(object owner, string member)
            => owner.GetType().GetProperty(member, flags)
               ?? throw new ArgumentException($"{owner.GetType().Name} has no property {member}.");
    }

    private static object? Parse(Type type, string text)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (text == "null") return null;
        if (text.StartsWith('[') && text.EndsWith(']')) text = text[1..^1];
        if (type.IsGenericType && type.FullName!.StartsWith("System.ValueTuple`"))
        {
            var fields = type.GetGenericArguments();
            var parts = SplitTopLevel(text);
            if (parts.Count != fields.Length) throw new ArgumentException($"{text}: expected {fields.Length} values.");
            return Activator.CreateInstance(type, fields.Select((f, i) => Parse(f, parts[i])).ToArray());
        }
        if (DictionaryTypes(type) is var (keyType, valueType))
        {
            var dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(keyType, valueType))!;
            foreach (var pair in SplitTopLevel(text))
            {
                var colon = pair.IndexOf(':');
                if (colon < 0) throw new ArgumentException($"{pair}: expected Key:value.");
                dictionary[Parse(keyType, pair[..colon].Trim())!] = Parse(valueType, pair[(colon + 1)..].Trim());
            }
            return dictionary;
        }
        if (type != typeof(string) && ElementType(type) is { } element)
        {
            var items = SplitTopLevel(text).Select(t => Parse(element, t)).ToArray();
            var array = Array.CreateInstance(element, items.Length);
            for (var i = 0; i < items.Length; i++) array.SetValue(items[i], i);
            return type.IsArray || type.IsInterface ? array : Activator.CreateInstance(type, array);
        }
        if (type.IsEnum) return Enum.Parse(type, text, ignoreCase: true);
        if (type == typeof(string)) return text;
        const BindingFlags statics = BindingFlags.Static | BindingFlags.Public;
        if (type.GetField(text, statics) is { } field && field.FieldType == type) return field.GetValue(null);
        if (type.GetProperty(text, statics) is { } property && property.PropertyType == type) return property.GetValue(null);
        if (type.GetConstructor([typeof(float)]) is { } fromFloat)
            return fromFloat.Invoke([float.Parse(text, CultureInfo.InvariantCulture)]);
        return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
    }

    // Commas inside brackets belong to a nested list.
    private static List<string> SplitTopLevel(string text)
    {
        var parts = new List<string>();
        var (depth, start) = (0, 0);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']') depth--;
            else if (text[i] == ',' && depth == 0)
            {
                parts.Add(text[start..i].Trim());
                start = i + 1;
            }
        }
        parts.Add(text[start..].Trim());
        return parts;
    }

    private static (Type Key, Type Value)? DictionaryTypes(Type type)
    {
        if (!type.IsGenericType) return null;
        var definition = type.GetGenericTypeDefinition();
        if (definition != typeof(IReadOnlyDictionary<,>) && definition != typeof(IDictionary<,>) && definition != typeof(Dictionary<,>))
            return null;
        var arguments = type.GetGenericArguments();
        return (arguments[0], arguments[1]);
    }

    private static Type? ElementType(Type type)
        => type.IsArray
            ? type.GetElementType()
            : type.IsGenericType && typeof(IEnumerable).IsAssignableFrom(type)
                ? type.GetGenericArguments()[0]
                : null;
}
