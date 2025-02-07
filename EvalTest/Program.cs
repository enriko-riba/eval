// See https://aka.ms/new-console-template for more information
using EvalTest;

Console.WriteLine("loading automation...");
var fileContent = File.ReadAllText("automation1.json");
var automation = AutomationTriggerBase.LoadAutomationFromJson(fileContent);
var devices = AutomationTriggerBase.GetReferencedDevices(automation);
Console.WriteLine($"automation references devices:\n{string.Join("\n", devices)}");

Console.WriteLine("evaluating automation...");

// Mock reading data for devices included in automation1.json
// automation1 is defined as:
// device1.temperature > 100 OR (device2.temperature < 30 AND device3.pressure > 150)
var mockDeviceReadings = new Dictionary<Guid, LastReadings>
{
    { Guid.Parse("C30616B7-C70A-427F-84BB-6EC19FB4B67E"), new LastReadings { { "temperature", 100.0 }, { "unusedDataPoint1", 1 }, { "unusedDataPoint2", 2 } } },
    { Guid.Parse("44C22283-0F08-4B00-948B-1A089E75C114"), new LastReadings { { "temperature", 30.0 },  { "unusedDataPoint1", 1 }, { "unusedDataPoint2", 2 } } },
    { Guid.Parse("B6B12AEB-DF55-404D-9ECF-F0BB6D582622"), new LastReadings { { "pressure", 200.0 }, { "unusedDataPoint1", 1 }, { "unusedDataPoint2", 2 } } }
};

var result = automation?.Evaluate(mockDeviceReadings);
Console.WriteLine($"Automation evaluated to: {result}");
