# SwimStats Test Suite

This directory contains all unit tests for the SwimStats application.

## Quick Start

```powershell
# Run all tests
dotnet test

# Run with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~ResultServiceTests"
```

## Test Projects

### SwimStats.Tests
Main test project containing:
- **DatabaseTests**: EF Core model and relationship tests (6 tests)
- **ResultServiceTests**: Data access layer tests (7 tests)
- **MainViewModelTests**: Business logic and calculation tests (10 tests)

**Total: 23 tests**

## Test Philosophy

We follow **Test-Driven Development (TDD)**:
1. Write tests first (or alongside features)
2. Run tests after every major change
3. Keep tests isolated and fast (use in-memory databases)
4. Aim for clear, descriptive test names

## Test Structure

All tests follow the **Arrange-Act-Assert** pattern:

```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange: Set up test data
    var swimmer = new Swimmer { Name = "Test" };
    
    // Act: Execute the code under test
    var result = await _service.GetBestTimeAsync(swimmer.Id, Stroke.Freestyle, 50);
    
    // Assert: Verify expectations
    Assert.NotNull(result);
    Assert.Equal(24.5, result.Value, 1);
}
```

## Testing Utilities

### In-Memory Database

All database tests use SQLite in-memory mode:

```csharp
var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
    .UseSqlite("DataSource=:memory:")
    .Options;

_db = new SwimStatsDbContext(options);
_db.Database.OpenConnection();  // Keep connection alive
_db.Database.EnsureCreated();   // Create schema
```

### Test Data Setup

Each test class seeds its own test data in the constructor or setup method:

```csharp
private void SeedTestData()
{
    var swimmer = new Swimmer { Id = 1, Name = "Test Swimmer" };
    var evt = new Event { Id = 1, Stroke = Stroke.Freestyle, DistanceMeters = 50 };
    _db.Swimmers.Add(swimmer);
    _db.Events.Add(evt);
    _db.SaveChanges();
}
```

## Adding New Tests

### 1. Create Test Class

```csharp
using Xunit;

namespace SwimStats.Tests;

public class MyNewServiceTests : IDisposable
{
    private readonly SwimStatsDbContext _db;
    private readonly MyNewService _service;

    public MyNewServiceTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new SwimStatsDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new MyNewService(_db);
    }

    [Fact]
    public async Task MyMethod_WhenCalled_ReturnsExpectedResult()
    {
        // Test implementation
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
```

### 2. Run Tests

```powershell
dotnet test
```

### 3. Verify Coverage

Ensure your new feature has:
- ✅ Happy path test
- ✅ Edge cases (null, empty, invalid input)
- ✅ Error handling scenarios

## CI/CD Integration

Tests run automatically on:
- Every push to `main`
- Every pull request

GitHub Actions workflow: `.github/workflows/ci.yml`

## Common Test Patterns

### Testing Async Methods
```csharp
[Fact]
public async Task GetResultsAsync_ReturnsResults()
{
    var results = await _service.GetResultsAsync(1, Stroke.Freestyle, 50);
    Assert.NotEmpty(results);
}
```

### Testing Null/Empty Cases
```csharp
[Fact]
public async Task GetBestTime_NoResults_ReturnsNull()
{
    var result = await _service.GetBestTimeAsync(999, Stroke.Freestyle, 50);
    Assert.Null(result);
}
```

### Testing Calculations
```csharp
[Fact]
public void CalculateMedian_OddCount_ReturnsMiddleValue()
{
    var values = new List<double> { 10.0, 20.0, 30.0 };
    var median = MainViewModel.CalculateMedian(values);
    Assert.Equal(20.0, median);
}
```

### Testing Filtering
```csharp
[Fact]
public async Task GetResults_FiltersByStroke()
{
    var freestyle = await _service.GetResultsAsync(1, Stroke.Freestyle, null);
    var backstroke = await _service.GetResultsAsync(1, Stroke.Backstroke, null);
    
    Assert.All(freestyle, r => Assert.Equal(Stroke.Freestyle, r.Event.Stroke));
    Assert.All(backstroke, r => Assert.Equal(Stroke.Backstroke, r.Event.Stroke));
}
```

## Debugging Tests

### Run Single Test
```powershell
dotnet test --filter "FullyQualifiedName=SwimStats.Tests.ResultServiceTests.GetBestTimeAsync_ReturnsLowestTime"
```

### View Test Output
```powershell
dotnet test --logger "console;verbosity=detailed"
```

## Best Practices

1. **Keep tests fast**: Use in-memory databases, avoid external dependencies
2. **Keep tests isolated**: Each test should be independent
3. **Use descriptive names**: Test name should describe what it tests
4. **One assertion concept per test**: Test one thing at a time
5. **Follow AAA pattern**: Arrange, Act, Assert
6. **Clean up resources**: Use `IDisposable` to dispose database connections

## Resources

- [xUnit Documentation](https://xunit.net/)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
- [Microsoft Testing Best Practices](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
