using FEZRepacker.Core.Definitions.Game.Level.Scripting;

namespace FezEditor.Tools;

public static class ScriptExtensions
{
    public static string Stringify(this ComparisonOperator @operator)
    {
        return @operator switch
        {
            ComparisonOperator.None => "?",
            ComparisonOperator.Equal => "==",
            ComparisonOperator.GreaterEqual => ">=",
            ComparisonOperator.LessEqual => "<=",
            ComparisonOperator.Greater => ">",
            ComparisonOperator.Less => "<",
            ComparisonOperator.NotEqual => "!=",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null)
        };
    }

    private static string ToPropertyIdentifier(Entity entity, string property)
    {
        if (string.IsNullOrEmpty(entity.Type))
        {
            return "?";
        }

        var output = entity.Type;
        if (entity.Identifier.HasValue)
        {
            output += $"[{entity.Identifier.Value}]";
        }

        output += string.IsNullOrEmpty(property) ? ".?" : $".{property}";
        return output;
    }

    public static string Stringify(this ScriptTrigger trigger)
    {
        return ToPropertyIdentifier(trigger.Object, trigger.Event);
    }

    public static string Stringify(this ScriptCondition condition)
    {
        var output = ToPropertyIdentifier(condition.Object, condition.Property);
        output += $" {condition.Operator.Stringify()} {condition.Value}";
        return output;
    }

    public static string Stringify(this ScriptAction action)
    {
        var output = ToPropertyIdentifier(action.Object, action.Operation);

        output += "(";
        for (var i = 0; i < action.Arguments.Length; i++)
        {
            if (i > 0)
            {
                output += ", ";
            }

            output += action.Arguments[i];
        }

        output += ")";
        if (action.Blocking)
        {
            output = "#" + output;
        }

        if (action.Killswitch)
        {
            output = "!" + output;
        }

        return output;
    }
}