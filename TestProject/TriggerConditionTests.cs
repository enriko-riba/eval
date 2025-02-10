namespace EvalTest.Tests;

public class TriggerConditionTests
{
    private static readonly Guid Device1Id = Guid.Parse("C30616B7-C70A-427F-84BB-6EC19FB4B67E");
    private static readonly Guid Device2Id = Guid.Parse("44C22283-0F08-4B00-948B-1A089E75C114");
    private static readonly Guid Device3Id = Guid.Parse("B6B12AEB-DF55-404D-9ECF-F0BB6D582622");

    [Fact]
    // Device1.temperature > 100 (true)
    public void SimpleCondition_GreaterThan_ReturnsExpectedResult()
    {
        // Arrange
        var condition = new SimpleTrigger
        {
            DeviceId = Device1Id,
            DatapointName = "temperature",
            Operator = ComparisonOperator.GreaterThan,
            ConditionValue = 100
        };

        var readings = new Dictionary<Guid, LastReadings>
        {
            { Device1Id, new LastReadings { { "temperature", 101.0 } } }
        };

        // Act
        var (result, _) = condition.Evaluate(readings);

        // Assert
        Assert.True(result);
    }

    [Fact]
    // Device1.temperature > 90 AND Device2.temperature < 40 (true)
    public void CompositeCondition_And_BothTrue_ReturnsTrue()
    {
        // Arrange
        var condition = new CompositeTrigger
        {
            Operator = LogicalBinaryOperator.And,
            Triggers =
            [
                new SimpleTrigger
                {
                    DeviceId = Device1Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    ConditionValue = 90
                },
                new SimpleTrigger
                {
                    DeviceId = Device2Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.LessThan,
                    ConditionValue = 40
                }
            ]
        };

        var readings = new Dictionary<Guid, LastReadings>
        {
            { Device1Id, new LastReadings { { "temperature", 95.0 } } },
            { Device2Id, new LastReadings { { "temperature", 35.0 } } }
        };

        // Act
        var (result, _) = condition.Evaluate(readings);

        // Assert
        Assert.True(result);
    }

    [Fact]
    // Device1.temperature > 90 AND Device2.temperature < 40 (false)
    public void CompositeCondition_And_OneTrue_ReturnsFalse()
    {
        // Arrange
        var condition = new CompositeTrigger
        {
            Operator = LogicalBinaryOperator.And,
            Triggers =
            [
                new SimpleTrigger
                {
                    DeviceId = Device1Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    ConditionValue = 90
                },
                new SimpleTrigger
                {
                    DeviceId = Device2Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.LessThan,
                    ConditionValue = 40
                }
            ]
        };

        var readings = new Dictionary<Guid, LastReadings>
        {
            { Device1Id, new LastReadings { { "temperature", 95.0 } } },
            { Device2Id, new LastReadings { { "temperature", 40.0 } } }
        };

        // Act
        var (result, _) = condition.Evaluate(readings);

        // Assert
        Assert.False(result);
    }

    [Fact]
    // Device1.temperature > 100 OR Device2.temperature < 30 (true)
    public void CompositeCondition_Or_OneTrue_ReturnsTrue()
    {
        // Arrange
        var condition = new CompositeTrigger
        {
            Operator = LogicalBinaryOperator.Or,
            Triggers =
            [
                new SimpleTrigger
                {
                    DeviceId = Device1Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    ConditionValue = 100
                },
                new SimpleTrigger
                {
                    DeviceId = Device2Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.LessThan,
                    ConditionValue = 30
                }
            ]
        };

        var readings = new Dictionary<Guid, LastReadings>
        {
            { Device1Id, new LastReadings { { "temperature", 95.0 } } },
            { Device2Id, new LastReadings { { "temperature", 25.0 } } }
        };

        // Act
        var (result, _) = condition.Evaluate(readings);

        // Assert
        Assert.True(result);
    }

    [Fact]
    // Device1.temperature > 100 OR (Device2.temperature < 30 AND Device3.pressure > 150) (true)
    public void NestedCondition_ComplexLogic_ReturnsExpectedResult()
    {
        // Arrange
        var condition = new CompositeTrigger
        {
            Operator = LogicalBinaryOperator.Or,
            Triggers =
            [
                new SimpleTrigger
                {
                    DeviceId = Device1Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    ConditionValue = 100
                },
                new CompositeTrigger
                {
                    Operator = LogicalBinaryOperator.And,
                    Triggers =
                    [
                        new SimpleTrigger
                        {
                            DeviceId = Device2Id,
                            DatapointName = "temperature",
                            Operator = ComparisonOperator.LessThan,
                            ConditionValue = 30
                        },
                        new SimpleTrigger
                        {
                            DeviceId = Device3Id,
                            DatapointName = "pressure",
                            Operator = ComparisonOperator.GreaterThan,
                            ConditionValue = 150
                        }
                    ]
                }
            ]
        };

        var readings = new Dictionary<Guid, LastReadings>
        {
            { Device1Id, new LastReadings { { "temperature", 95.0 } } },
            { Device2Id, new LastReadings { { "temperature", 25.0 } } },
            { Device3Id, new LastReadings { { "pressure", 160.0 } } }
        };

        // Act
        var (result, _) = condition.Evaluate(readings);

        // Assert
        Assert.True(result);
    }

    [Fact]
    // Device1.temperature > 100 OR
    // (Device2.temperature < 30 AND
    //     (Device3.pressure > 150 OR Device1.humidity < 50 OR
    //         (Device2.pressure > 200 AND Device3.temperature < 20)
    //     )
    // )
    // (true)
    public void DeepNestedCondition_ComplexLogic_ReturnsExpectedResult()
    {
        // Arrange
        var condition = new CompositeTrigger
        {
            Operator = LogicalBinaryOperator.Or,
            Triggers =
            [
                new SimpleTrigger
                {
                    DeviceId = Device1Id,
                    DatapointName = "temperature",
                    Operator = ComparisonOperator.GreaterThan,
                    ConditionValue = 100
                },
                new CompositeTrigger
                {
                    Operator = LogicalBinaryOperator.And,
                    Triggers =
                    [
                        new SimpleTrigger
                        {
                            DeviceId = Device2Id,
                            DatapointName = "temperature",
                            Operator = ComparisonOperator.LessThan,
                            ConditionValue = 30
                        },
                        new CompositeTrigger
                        {
                            Operator = LogicalBinaryOperator.Or,
                            Triggers =
                            [
                                new SimpleTrigger
                                {
                                    DeviceId = Device3Id,
                                    DatapointName = "pressure",
                                    Operator = ComparisonOperator.GreaterThan,
                                    ConditionValue = 150
                                },
                                new SimpleTrigger
                                {
                                    DeviceId = Device1Id,
                                    DatapointName = "humidity",
                                    Operator = ComparisonOperator.LessThan,
                                    ConditionValue = 50
                                },
                                new CompositeTrigger
                                {
                                    Operator = LogicalBinaryOperator.And,
                                    Triggers =
                                    [
                                        new SimpleTrigger
                                        {
                                            DeviceId = Device2Id,
                                            DatapointName = "pressure",
                                            Operator = ComparisonOperator.GreaterThan,
                                            ConditionValue = 200
                                        },
                                        new SimpleTrigger
                                        {
                                            DeviceId = Device3Id,
                                            DatapointName = "temperature",
                                            Operator = ComparisonOperator.LessThan,
                                            ConditionValue = 20
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        var readings = new Dictionary<Guid, LastReadings>
        {
            { Device1Id, new LastReadings { { "temperature", 95.0 }, { "humidity", 45.0 } } },
            { Device2Id, new LastReadings { { "temperature", 25.0 }, { "pressure", 210.0 } } },
            { Device3Id, new LastReadings { { "pressure", 160.0 }, { "temperature", 15.0 } } }
        };

        // Act
        var (result, _) = condition.Evaluate(readings);

        // Assert
        Assert.True(result);
    }

    [Fact]    
    public void Deserialization_ValidJson_CreatesCorrectStructure()
    {
        // Arrange
        var json = @"{
            ""Type"": ""Composite"",
            ""Operator"": ""Or"",
            ""Triggers"": [
                {
                    ""Type"": ""Simple"",
                    ""DeviceId"": ""C30616B7-C70A-427F-84BB-6EC19FB4B67E"",
                    ""DatapointName"": ""temperature"",
                    ""Operator"": ""GreaterThan"",
                    ""ConditionValue"": 100
                },
                {
                    ""Type"": ""Composite"",
                    ""Operator"": ""And"",
                    ""Triggers"": [
                        {
                            ""Type"": ""Simple"",
                            ""DeviceId"": ""44C22283-0F08-4B00-948B-1A089E75C114"",
                            ""DatapointName"": ""temperature"",
                            ""Operator"": ""LessThan"",
                            ""ConditionValue"": 30
                        },
                        {
                            ""Type"": ""Simple"",
                            ""DeviceId"": ""B6B12AEB-DF55-404D-9ECF-F0BB6D582622"",
                            ""DatapointName"": ""pressure"",
                            ""Operator"": ""GreaterThan"",
                            ""ConditionValue"": 150
                        }
                    ]
                }
            ]
        }";

        // Act
        var automation = AutomationTriggerBase.LoadAutomationFromJson(json);

        // Assert
        Assert.NotNull(automation);
        Assert.IsType<CompositeTrigger>(automation);
        var composite = (CompositeTrigger)automation;
        Assert.Equal(LogicalBinaryOperator.Or, composite.Operator);
        Assert.Equal(2, composite.Triggers.Count);
    }
}
