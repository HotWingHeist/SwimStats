Thanks for your interest in contributing to SwimStats!

How to contribute

- Fork the repository and create a feature branch from `main`.
  - Naming: `feature/<short-description>` or `fix/<short-description>`.
- Make small, focused changes with clear commit messages.
- Run the test suite locally before creating a pull request.

Build & test locally (PowerShell on Windows):

```powershell
# From repository root
dotnet restore
dotnet build
dotnet test
```

Branch and PR policy

- Work on a topic branch, open a PR against `main`.
- PRs should include a short description, testing notes, and screenshots if UI changes.
- CI will run build and tests automatically. Address any failing tests before merging.

Coding standards

- Follow existing repository structure and naming conventions.
- Keep UI changes accessible (color-blind friendly palettes already used).
- **Add unit tests for new logic where applicable.**

## Test-Driven Development (TDD) Approach

We follow a test-driven approach to ensure code quality:

### When to write tests

1. **New Features**: Write tests alongside or before implementing new features
2. **Bug Fixes**: Add a failing test that reproduces the bug, then fix it
3. **Refactoring**: Ensure existing tests pass before and after refactoring

### Test guidelines

- **Test Coverage**: Aim to test business logic, data access, and edge cases
- **Test Isolation**: Each test should be independent and use in-memory databases
- **Test Naming**: Use descriptive names like `MethodName_Scenario_ExpectedResult`
- **Run Tests**: Always run `dotnet test` after making changes

### Test structure

```csharp
[Fact]
public async Task MethodName_WhenCondition_ShouldExpectedBehavior()
{
    // Arrange: Set up test data and dependencies
    var service = new MyService(_mockDb);
    
    // Act: Call the method being tested
    var result = await service.MethodName(input);
    
    // Assert: Verify the expected outcome
    Assert.Equal(expected, result);
}
```

### Running tests locally

```powershell
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests in a specific project
dotnet test tests/SwimStats.Tests/SwimStats.Tests.csproj
```

### Current test coverage

- ✅ `MainViewModelTests`: Business logic (median calculation)
- ✅ `ResultServiceTests`: Data access layer
- ✅ `DatabaseTests`: EF Core models and relationships

### Adding new tests

When adding a new feature:

1. Create or update test file in `tests/SwimStats.Tests/`
2. Write test cases covering happy path and edge cases
3. Implement the feature
4. Run tests: `dotnet test`
5. Ensure all tests pass before committing

Maintainers

- If a PR is not acted on within a few days, mention a maintainer or open an issue for discussion.

Thanks — contributions are welcome!