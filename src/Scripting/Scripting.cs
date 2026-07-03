using System.Reflection;
using FEZRepacker.Core.Definitions.Game.Common;

namespace FezEditor.Scripting;

[AttributeUsage(AttributeTargets.Interface)]
internal class EntityAttribute : Attribute
{
    public Type? Model { get; set; }

    public ActorType[]? RestrictTo { get; set; }

    public bool Static { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Event)]
internal class DescriptionAttribute : Attribute
{
    public string Description { get; }

    public DescriptionAttribute(string description)
    {
        Description = description;
    }
}

[AttributeUsage(AttributeTargets.Event)]
internal class EndTriggerAttribute : Attribute
{
    public string Trigger { get; }

    public EndTriggerAttribute(string trigger)
    {
        Trigger = trigger;
    }
}

internal delegate void LongRunningAction();

public record ScriptTriggerDef(string Name, string? Description, string? EndTrigger);

public record ScriptConditionDef(string Name, string? Description, Type ReturnType);

public record ScriptActionDef(string Name, string? Description, ParameterInfo[] Parameters, bool IsLongRunning);

public record ScriptApiEntry(
    string TypeName,
    Type InterfaceType,
    bool IsStatic,
    Type? Model,
    ActorType[]? RestrictTo,
    ScriptTriggerDef[] Triggers,
    ScriptConditionDef[] Conditions,
    ScriptActionDef[] Actions
);

public static class ScriptingApi
{
    public static readonly ScriptApiEntry[] Entries = Build();

    private static ScriptApiEntry[] Build()
    {
        return Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsInterface && t.GetCustomAttribute<EntityAttribute>() != null)
            .Select(interfaceType =>
            {
                var attr = interfaceType.GetCustomAttribute<EntityAttribute>()!;

                // Strip leading "I" from interface name (IGomez -> Gomez)
                var typeName = interfaceType.Name[1..];

                // C# events
                var triggers = interfaceType
                    .GetEvents()
                    .Select(e => new ScriptTriggerDef(
                        e.Name,
                        e.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        e.GetCustomAttribute<EndTriggerAttribute>()?.Trigger))
                    .ToArray();

                // C# properties
                var propertyConditions = interfaceType
                    .GetProperties()
                    .Select(p => new ScriptConditionDef(
                        p.Name,
                        p.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        p.PropertyType));

                // Explicit get_* C# methods with parameters
                // IsSpecialName excludes compiler-generated property backing methods
                var methodConditions = interfaceType
                    .GetMethods()
                    .Where(m => m.Name.StartsWith("get_", StringComparison.Ordinal) && !m.IsSpecialName)
                    .Select(m => new ScriptConditionDef(
                        m.Name["get_".Length..],
                        m.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        m.ReturnType));

                var conditions = propertyConditions
                    .Concat(methodConditions)
                    .ToArray();

                // C# methods - exclude property accessors and On* internal callbacks
                var actions = interfaceType
                    .GetMethods()
                    .Where(m => !m.IsSpecialName
                             && !m.Name.StartsWith("On", StringComparison.Ordinal)
                             && !m.Name.StartsWith("get_", StringComparison.Ordinal))
                    .Select(m => new ScriptActionDef(
                        m.Name,
                        m.GetCustomAttribute<DescriptionAttribute>()?.Description,
                        m.GetParameters().Skip(attr.Static ? 0 : 1).ToArray(),
                        m.ReturnType == typeof(LongRunningAction)))
                    .ToArray();

                return new ScriptApiEntry(
                    typeName, interfaceType, attr.Static, attr.Model, attr.RestrictTo, triggers, conditions, actions);
            })
            .OrderBy(e => e.TypeName)
            .ToArray();
    }
}