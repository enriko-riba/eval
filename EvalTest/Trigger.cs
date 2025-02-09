using System.Text.Json;
using System.Text.Json.Serialization;

using DeviceId = System.Guid;
using DatapointName = string;

namespace EvalTest;

/*
  
Idea
=========================

Use AST (Abstract Syntax Tree) to represent trigger conditions and evaluate them based on the latest readings of devices and datapoints.

                     / - Simple Trigger (leaf node): condition value - Op - reading 
Composite trigger - Op 
                     \ - Simple Trigger (leaf node): condition value - Op - reading


                                               /- ...leaf node
                     / - Composite trigger - Op
Composite trigger - Op                         \- ... leaf
                     \ - Composite trigger ...

Leaf nodes can be evaluated directly, while non-leaf nodes require recursive evaluation of child nodes.



Proof
=========================

Given:
    Triggers (T):   A set of trigger conditions defined over devices (D) and datapoints (P).
    Devices (D):    A set of unique identifiers representing individual devices.
    Datapoints (P): A set of datapoint names that devices report.
    Values (V):     Latest readings or values associated with each device and datapoint.
    
Define:
    Evaluate(T, D, P, V) -> bool: A function that evaluates whether all trigger conditions (T) are satisfied based on the provided devices (D), datapoints (P), and their corresponding values (V).
    
Formal Proof
    1. Definition of Evaluate Function:
    For each trigger condition t in T, which is of the form (d, p, op, v_threshold), where:

    d is a device from D,
    p is a datapoint from P,
    op is a comparison operator (>, <, ==, !=),
    v_threshold is a threshold value.
        Evaluate(t, D, P, V) checks if:
        Retrieve v_actual from V corresponding to device d and datapoint p.
        Compare v_actual against v_threshold using op.
        Return true if all trigger conditions in T are satisfied, false otherwise.

    2. Correctness of Evaluate Function:
    Base Case (Simple Trigger):
        For a Simple trigger (d, p, op, v_threshold), Evaluate correctly retrieves v_actual from V and evaluates v_actual op v_threshold.
        The function returns true if the condition is satisfied, false otherwise.
    Composite Trigger (Composite):
        For a Composite trigger with logical operators (AND, OR), ensure Evaluate correctly handles operator precedence.
        Evaluate each sub-condition recursively and combine results according to the logical operator (AND: all must be true, OR: at least one must be true).
 */

public interface IAutomationTrigger
{
    bool Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings);
}

/// <summary>
/// Binary operator for combining multiple triggers.
/// </summary>
public enum LogicalBinaryOperator { And, Or }

/// <summary>
/// Comparison operator for evaluating trigger conditions.
/// </summary>
public enum ComparisonOperator { GreaterThan, LessThan, EqualTo, NotEqualTo }

/// <summary>
/// Dictionary with datapoint name/value of latest readings for a device.
/// </summary>
public class LastReadings : Dictionary<DatapointName, object> { }


/// <summary>
/// Abstract base class, needed to handle polymorphic JSON serialization.
/// </summary>
[JsonConverter(typeof(AutomationTriggerBaseConverter))]
public abstract class AutomationTriggerBase : IAutomationTrigger
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public abstract bool Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings);

    /// <summary>
    /// Helper to load automations.
    /// </summary>
    /// <param name="json"></param>
    /// <returns></returns>
    public static IAutomationTrigger? LoadAutomationFromJson(string json)
    {
        return JsonSerializer.Deserialize<AutomationTriggerBase>(json, options);
    }

    /// <summary>
    /// Helper to get devices referenced by an automation.
    /// </summary>
    /// <param name="automationTrigger"></param>
    /// <returns></returns>
    public static IEnumerable<DeviceId> GetReferencedDevices(IAutomationTrigger? automationTrigger)
    {
        HashSet<DeviceId> referencedDevices = [];

        if (automationTrigger is SimpleTrigger simpleTrigger)
        {
            referencedDevices.Add(simpleTrigger.DeviceId);
        }
        else if (automationTrigger is CompositeTrigger compositeTrigger)
        {
            foreach (var trigger in compositeTrigger.Triggers)
            {
                referencedDevices.UnionWith(GetReferencedDevices(trigger));
            }
        }

        return referencedDevices;
    }
}


public class CompositeTrigger : AutomationTriggerBase
{
    public LogicalBinaryOperator Operator { get; set; }
    public List<AutomationTriggerBase> Triggers { get; set; } = [];

    public override bool Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings)
    {
        return Operator switch
        {
            LogicalBinaryOperator.And => Triggers.TrueForAll(trigger => trigger.Evaluate(allDevicesReadings)),
            LogicalBinaryOperator.Or => Triggers.Exists(trigger => trigger.Evaluate(allDevicesReadings)),
            _ => throw new NotImplementedException(),
        };
    }
}


public class SimpleTrigger : AutomationTriggerBase
{
    public DeviceId DeviceId { get; set; }
    public DatapointName DatapointName { get; set; } = default!;
    public ComparisonOperator Operator { get; set; }
    public double ConditionValue { get; set; }  // TODO: type based on metadata?

    public override bool Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings)   //  TODO: LastReadings should contain metadata e.g. data type
    {
        if (!allDevicesReadings.TryGetValue(DeviceId, out var lastReadings))
            return false;

        if (!lastReadings.TryGetValue(DatapointName, out var dataPointValue))
            return false;

        //  TODO: based on metadata, convert the datapoint value to strongly typed representation
        var value = Convert.ToDouble(dataPointValue);

        return Operator switch
        {
            ComparisonOperator.GreaterThan => value > ConditionValue,
            ComparisonOperator.LessThan => value < ConditionValue,
            ComparisonOperator.EqualTo => value == ConditionValue,
            ComparisonOperator.NotEqualTo => value != ConditionValue,
            _ => throw new NotImplementedException(),
        };
    }
}
