# WWoW.RecordedTests.Shared.Tests

Comprehensive unit test suite for the WWoW.RecordedTests.Shared library.

## Test Coverage

This test project provides **85 unit tests** covering all new functionality added to the shared library:

### Configuration Tests (42 tests)

#### ConfigurationResolver Tests (18 tests)
- String value resolution with CLI → env → config → default precedence
- Integer value resolution with type conversion
- Boolean value resolution with parsing
- TimeSpan value resolution from seconds
- Edge cases (empty strings, null values, invalid formats)

#### ServerConfigurationHelper Tests (12 tests)
- Server definition parsing from CLI, environment, and config
- Semicolon-separated server definition parsing
- TrueNAS client vs Local Docker client resolution
- Whitespace trimming and empty string filtering
- Server availability checker creation

#### OrchestrationConfigurationHelper Tests (12 tests)
- Orchestration options resolution with precedence
- Command-line argument parsing
- Environment variable resolution
- Config file option handling
- Invalid value handling and defaults

### Factory Tests (14 tests)

#### BotRunnerFactoryHelpers Tests (8 tests)
- `FromDelegate()` factory creation
- `FromType<T>()` factory creation
- Multiple instance creation validation
- Null argument validation
- Factory invocation behavior

#### ObsScreenRecorderFactory Tests (13 tests)
- `CreateDefault()` recorder creation
- `Create()` with custom configuration
- `CreateFromEnvironment()` with env variables
- `CreateFactory()` delegate creation
- Environment variable parsing and defaults
- Invalid configuration handling

### Storage Tests (14 tests)

#### FileSystemRecordedTestStorage Tests (14 tests)
- File upload with timestamp organization
- File download from storage
- Artifact listing for tests
- Artifact deletion with directory cleanup
- Special character sanitization in test names
- Empty directory cleanup
- Error handling (null artifacts, missing files)

### Desired State Tests (15 tests)

#### GmCommandServerDesiredState Tests (7 tests)
- Setup command application
- Teardown command reversion
- Empty command arrays handling
- Null command handling
- Idempotent apply/revert operations

#### DelegateServerDesiredState Tests (10 tests)
- Apply delegate invocation
- Revert delegate invocation
- Null delegate handling (no-op)
- Parameter passing validation
- Async delegate support
- Cancellation propagation
- Multiple invocation testing

## Test Structure

```
Tests/WWoW.RecordedTests.Shared.Tests/
├── Configuration/
│   ├── ConfigurationResolverTests.cs
│   ├── ServerConfigurationHelperTests.cs
│   └── OrchestrationConfigurationHelperTests.cs
├── DesiredState/
│   ├── DelegateServerDesiredStateTests.cs
│   └── GmCommandServerDesiredStateTests.cs
├── Factories/
│   ├── BotRunnerFactoryHelpersTests.cs
│   └── ObsScreenRecorderFactoryTests.cs
├── Storage/
│   └── FileSystemRecordedTestStorageTests.cs
└── README.md
```

## Running Tests

### Run All Tests
```bash
dotnet test Tests\WWoW.RecordedTests.Shared.Tests\WWoW.RecordedTests.Shared.Tests.csproj
```

### Run with Verbose Output
```bash
dotnet test Tests\WWoW.RecordedTests.Shared.Tests\WWoW.RecordedTests.Shared.Tests.csproj --verbosity detailed
```

### Run Specific Test Class
```bash
dotnet test Tests\WWoW.RecordedTests.Shared.Tests\WWoW.RecordedTests.Shared.Tests.csproj --filter "FullyQualifiedName~ConfigurationResolverTests"
```

### Run Specific Test
```bash
dotnet test Tests\WWoW.RecordedTests.Shared.Tests\WWoW.RecordedTests.Shared.Tests.csproj --filter "FullyQualifiedName~ResolveString_WithCliValue_ShouldReturnCliValue"
```

### Generate Code Coverage
```bash
dotnet test Tests\WWoW.RecordedTests.Shared.Tests\WWoW.RecordedTests.Shared.Tests.csproj --collect:"XPlat Code Coverage"
```

## Test Dependencies

- **xUnit** - Test framework
- **FluentAssertions** - Fluent assertion library for readable tests
- **NSubstitute** - Mocking framework for interfaces

## Test Patterns

### Arrange-Act-Assert (AAA)
All tests follow the AAA pattern:
```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and dependencies
    var input = "test";

    // Act - Execute the method under test
    var result = MethodUnderTest(input);

    // Assert - Verify the outcome
    result.Should().Be("expected");
}
```

### Descriptive Test Names
Test names follow the pattern: `MethodName_Scenario_ExpectedBehavior`

Examples:
- `ResolveString_WithCliValue_ShouldReturnCliValue`
- `FromDelegate_WithNullDelegate_ShouldThrowArgumentNullException`
- `UploadArtifactAsync_WithSpecialCharactersInTestName_ShouldSanitize`

### Environment Variable Cleanup
Tests that modify environment variables use `IDisposable` pattern for cleanup:
```csharp
public class MyTests : IDisposable
{
    private readonly List<string> _envVarsToCleanup = new();

    public void Dispose()
    {
        foreach (var varName in _envVarsToCleanup)
        {
            Environment.SetEnvironmentVariable(varName, null);
        }
    }
}
```

### Temporary File Cleanup
Tests that create files use try-finally blocks for cleanup:
```csharp
var tempFile = Path.GetTempFileName();
try
{
    // Test code
}
finally
{
    if (File.Exists(tempFile))
        File.Delete(tempFile);
}
```

## Test Results

**Latest Test Run**: All 85 tests passing ✅

```
Passed!  - Failed:     0, Passed:    85, Skipped:     0, Total:    85
```

### Test Breakdown by Category
- ✅ Configuration: 42 tests (49%)
- ✅ Factories: 21 tests (25%)
- ✅ Storage: 14 tests (16%)
- ✅ Desired State: 15 tests (18%)

## CI/CD Integration

### GitHub Actions Example
```yaml
name: Run Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal
```

## Adding New Tests

When adding new functionality to WWoW.RecordedTests.Shared:

1. Create a corresponding test file in the appropriate category folder
2. Follow the AAA pattern
3. Use descriptive test names
4. Clean up resources (files, environment variables)
5. Aim for high code coverage (>80%)
6. Test happy path, edge cases, and error conditions

## Future Enhancements

- [ ] Add integration tests for end-to-end scenarios
- [ ] Add performance/benchmark tests
- [ ] Implement code coverage reporting
- [ ] Add mutation testing for test quality validation
- [ ] Create test data builders for complex scenarios

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
