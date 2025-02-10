using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EvalTest;

using DatapointName = string;
using DeviceId = Guid;

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

public enum EvaluationError
{   
    NoError,
    
    //  TODO: extend with more errors if needed

    IncompleteData
}

public interface IAutomationTrigger
{
    /// <summary>
    /// Evaluates the trigger conditions based on the latest readings of devices and datapoints.
    /// </summary>
    /// <param name="allDevicesReadings"></param>
    /// <returns></returns>
    (bool IsAlert, EvaluationError error) Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings);

    /// <summary>
    /// Returns true if last evaluation resulted in an alert condition.
    /// </summary>
    bool CurrentSignal { get; }
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
/// Dictionary with datapoint name/value of latest readings for a single device.
/// </summary>
public class LastReadings : Dictionary<DatapointName, object> { }


/// <summary>
/// Abstract base class, needed to handle polymorphic JSON serialization.
/// </summary>
[JsonConverter(typeof(AutomationTriggerBaseConverter))]
[DebuggerDisplay("{GetType().Name}")]
public abstract class AutomationTriggerBase : IAutomationTrigger
{
    private static readonly JsonSerializerOptions options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public abstract (bool IsAlert, EvaluationError error) Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings);

    public bool CurrentSignal { get; protected set; }

    public EvaluationError CurrentError { get; protected set; }

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

    /// <summary>
    /// Returns a list of triggers that are in a signaled state (in alert condition).
    /// </summary>
    /// <param name="automationTrigger"></param>
    /// <returns></returns>
    public static IEnumerable<IAutomationTrigger> GetSignaledTriggers(IAutomationTrigger automationTrigger)
    {      
        List<IAutomationTrigger> signaledTriggers = [];

        if (automationTrigger.CurrentSignal)
        {
            signaledTriggers.Add(automationTrigger);
        }

        if (automationTrigger is CompositeTrigger compositeTrigger)
        {
            foreach (var trigger in compositeTrigger.Triggers)
            {
                signaledTriggers.AddRange(GetSignaledTriggers(trigger));
            }
        }

        return signaledTriggers;
    }
}


public class CompositeTrigger : AutomationTriggerBase
{
    public LogicalBinaryOperator Operator { get; set; }
    public List<AutomationTriggerBase> Triggers { get; set; } = [];

    public override (bool, EvaluationError) Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings)
    {
        var identitySeed = Operator == LogicalBinaryOperator.And;   // must start with true for ANDs and false for ORs
        var result = Triggers.Aggregate(
            (IsAlert: identitySeed, error: EvaluationError.NoError),
            (acc, trigger) =>
            {
                var (isAlert, triggerError) = trigger.Evaluate(allDevicesReadings);
                return (
                    IsAlert: Operator == LogicalBinaryOperator.And ? acc.IsAlert && isAlert : acc.IsAlert || isAlert,
                    error: triggerError != EvaluationError.NoError ? triggerError : acc.error
                );
            });
        CurrentSignal = result.IsAlert;
        CurrentError = result.error;
        return result;

        //
        //  The above is the same as the following two lines except it captures the EvaluationError:
        //
        //var result = Operator switch
        //{
        //    LogicalBinaryOperator.And => Triggers.All(trigger => trigger.Evaluate(allDevicesReadings).IsAlert),
        //    LogicalBinaryOperator.Or => Triggers.Any(trigger => trigger.Evaluate(allDevicesReadings).IsAlert),
        //    _ => throw new NotImplementedException(),
        //};
        //return (result, ErrorReason.NoError);
    }
}


public class SimpleTrigger : AutomationTriggerBase
{
    public DeviceId DeviceId { get; set; }
    public DatapointName DatapointName { get; set; } = default!;
    public ComparisonOperator Operator { get; set; }
    public double ConditionValue { get; set; }  // TODO: type based on metadata?

    public override (bool, EvaluationError) Evaluate(IDictionary<DeviceId, LastReadings> allDevicesReadings)   //  TODO: LastReadings should contain metadata e.g. data type
    {
        if (!allDevicesReadings.TryGetValue(DeviceId, out var lastReadings))
        {
            CurrentError = EvaluationError.IncompleteData;
            CurrentSignal = false;
            return (CurrentSignal, CurrentError);
        }

        if (!lastReadings.TryGetValue(DatapointName, out var dataPointValue))
        {
            CurrentError = EvaluationError.IncompleteData;
            CurrentSignal = false;
            return (CurrentSignal, CurrentError);
        }

        //  TODO: based on metadata, convert the datapoint value to strongly typed representation
        var value = Convert.ToDouble(dataPointValue);

        var result = Operator switch
        {
            ComparisonOperator.GreaterThan => value > ConditionValue,
            ComparisonOperator.LessThan => value < ConditionValue,
            ComparisonOperator.EqualTo => value == ConditionValue ,
            ComparisonOperator.NotEqualTo => value != ConditionValue,
            _ => throw new NotImplementedException(),
        };
        CurrentSignal = result;
        CurrentError = EvaluationError.NoError;
        return (CurrentSignal, CurrentError);
    }

    override public string ToString()
    {
        return $"device '{DeviceId}', datapoint '{DatapointName}' {Operator} {ConditionValue}";
    }
}
