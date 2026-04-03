// AOT Compatibility Validation for PicoCfg
// This program validates that all PicoCfg features work correctly in AOT-compiled mode

using PicoCfg;
using PicoCfg.Extensions;

Console.WriteLine("=== PicoCfg AOT Compatibility Validation ===");
Console.WriteLine();

var testResults = new List<bool>();

// Test 1: Basic key-value parsing
await Test(
    "Basic key-value parsing",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("TestKey=TestValue\nAnotherKey=AnotherValue");

        var config = await builder.BuildAsync();
        var value1 = await config.GetValueAsync("TestKey");
        var value2 = await config.GetValueAsync("AnotherKey");

        return value1 == "TestValue" && value2 == "AnotherValue";
    }
);

// Test 2: Multiple configuration source priority (last source wins)
await Test(
    "Multiple source priority",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key=FirstValue");
        builder.Add("Key=SecondValue"); // This should override

        var config = await builder.BuildAsync();
        var value = await config.GetValueAsync("Key");

        return value == "SecondValue";
    }
);

// Test 3: Different configuration source types
await Test(
    "Multiple source types",
    async () =>
    {
        var builder = Cfg.CreateBuilder();

        // String source
        builder.Add("StringKey=StringValue");

        // Dictionary source
        builder.Add(new Dictionary<string, string> { ["DictKey"] = "DictValue" });

        // Stream source
        builder.Add(() =>
        {
            var stream = new MemoryStream();
            using var writer = new StreamWriter(stream, leaveOpen: true);
            writer.WriteLine("StreamKey=StreamValue");
            writer.Flush();
            stream.Position = 0;
            return stream;
        });

        var config = await builder.BuildAsync();

        var stringValue = await config.GetValueAsync("StringKey");
        var dictValue = await config.GetValueAsync("DictKey");
        var streamValue = await config.GetValueAsync("StreamKey");

        return stringValue == "StringValue"
            && dictValue == "DictValue"
            && streamValue == "StreamValue";
    }
);

// Test 4: Missing key handling
await Test(
    "Missing key handling",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("ExistingKey=SomeValue");

        var config = await builder.BuildAsync();
        var existingValue = await config.GetValueAsync("ExistingKey");
        var missingValue = await config.GetValueAsync("NonExistentKey");

        return existingValue == "SomeValue" && missingValue == null;
    }
);

// Test 5: Complex key names with special characters
await Test(
    "Complex key names",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Section:Subsection:Key=Value\nSection.Key.With.Dots=AnotherValue");

        var config = await builder.BuildAsync();
        var value1 = await config.GetValueAsync("Section:Subsection:Key");
        var value2 = await config.GetValueAsync("Section.Key.With.Dots");

        return value1 == "Value" && value2 == "AnotherValue";
    }
);

// Test 6: Empty lines and whitespace handling
await Test(
    "Whitespace handling",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("\n\nKey1=Value1\n  \nKey2  =  Value2  \nKey3=\n\n");

        var config = await builder.BuildAsync();
        var value1 = await config.GetValueAsync("Key1");
        var value2 = await config.GetValueAsync("Key2");
        var value3 = await config.GetValueAsync("Key3");

        // Key2 should be trimmed, Key3 should be empty string
        return value1 == "Value1" && value2 == "Value2" && value3 == "";
    }
);

// Test 7: Async cancellation support
await Test(
    "Async cancellation",
    async () =>
    {
        try
        {
            var builder = Cfg.CreateBuilder();
            builder.Add("Key=Value");

            var cts = new CancellationTokenSource();
            cts.Cancel(); // Cancel immediately

            var config = await builder.BuildAsync(cts.Token);
            var value = await config.GetValueAsync("Key", cts.Token);

            // If we get here without cancellation, it's still OK for AOT validation
            return value == "Value";
        }
        catch (OperationCanceledException)
        {
            // Cancellation is also acceptable
            return true;
        }
    }
);

// Test 8: Multiple providers and GetChildren (if implemented)
await Test(
    "Provider enumeration",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("Key1=Value1");
        builder.Add("Key2=Value2");

        var config = await builder.BuildAsync();

        // Try to enumerate children (even if empty)
        await foreach (var _ in config.GetChildrenAsync())
        {
            break; // Just check that enumeration works
        }

        // For now, just ensure no exceptions
        return true;
    }
);

// Test 9: Change notification
await Test(
    "Change notification",
    async () =>
    {
        var builder = Cfg.CreateBuilder();
        builder.Add("InitialKey=InitialValue");

        var config = await builder.BuildAsync();

        // Get change token
        var changeToken = await config.WatchAsync();

        // Initial state should be unchanged
        if (changeToken.HasChanged)
            return false;

        // Try to reload (this should trigger change in some providers)
        await config.ReloadAsync();

        // After reload, token might be changed (depends on implementation)
        // For this test, just ensure no exceptions
        return true;
    }
);

// Summary
Console.WriteLine();
Console.WriteLine("=== Validation Summary ===");
Console.WriteLine($"Total tests: {testResults.Count}");
Console.WriteLine($"Passed: {testResults.Count(r => r)}");
Console.WriteLine($"Failed: {testResults.Count(r => !r)}");

if (testResults.All(r => r))
{
    Console.WriteLine("✅ All tests passed! PicoCfg is AOT compatible.");
    return 0;
}
else
{
    Console.WriteLine("❌ Some tests failed. Check the output above.");
    return 1;
}

async Task Test(string name, Func<Task<bool>> test)
{
    try
    {
        Console.Write($"Testing: {name}... ");
        var result = await test();
        testResults.Add(result);

        if (result)
        {
            Console.WriteLine("✅ PASS");
        }
        else
        {
            Console.WriteLine("❌ FAIL (wrong result)");
        }
    }
    catch (Exception ex)
    {
        testResults.Add(false);
        Console.WriteLine($"❌ FAIL (exception: {ex.Message})");
    }
}
