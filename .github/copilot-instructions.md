# TDD and Clean Code - Copilot Instructions

This codebase follows strict Test-Driven Development (TDD) practices and clean code principles. Focus on these essential patterns and workflows:

## TDD Workflow (RED-GREEN-REFACTOR-COMMIT)

1. **RED**: Write ONE failing test that describes desired behavior - test should fail for the right reason
2. **GREEN**: Write minimal production code to make the test pass - don't over-engineer  
3. **COMMIT**: Commit both test and production code together with clear message
4. **REFACTOR**: Improve code quality while keeping all tests green (if needed)
5. **COMMIT**: Commit refactoring changes separately with descriptive message
6. **One Test at a Time**: Always write only ONE test method per RED-GREEN-REFACTOR cycle
7. **Focus on Edge Cases First**: Start with edge cases and error conditions first (invalid input, missing data, etc.)
8. **ALWAYS**: When prompted to work on any code changes, first repeat these TDD instructions to confirm understanding of the workflow before proceeding.

**Critical**: Never write production code without a failing test first. Refactoring must never change behavior.

### **One Test at a Time Discipline**: 
- Write only ONE test method per RED-GREEN-REFACTOR cycle
- Start with edge cases and error conditions first (invalid input, missing data, etc.)
- Then write tests for main happy path logic
- Each test should verify one specific behavior or scenario
- Never write multiple test methods before implementing the production code


## Test Architecture Patterns

- **Testing Framework**: xUnit with Shouldly assertions, Moq for mocking, Coverlet for coverage
- **Test Structure**: AAA pattern (Arrange-Act-Assert) with clear separation
- **Naming**: `WhenCondition_ShouldExpectedOutcome` for test methods
- **Test Doubles**: Dedicated `TestDoubles/` folder with `TestLogger`, `FakeFileSystemService`, `PassThroughProcessLauncher`, `FakeTimeService`
- **System Under Test**: Use `SystemUnderTest` property pattern for clean test setup

### Test Class Organization Patterns

- **Nested Test Classes**: Group related tests using nested classes that inherit from the parent test class
  ```csharp
  public class ServiceTests : IDisposable, ISystemUnderTest<Service>
  {
      public class RegistrationTests : ServiceTests { }
      public class ValidationTests : ServiceTests { }
      public class DeletionTests : ServiceTests { }
  }
  ```

- **Database Test Setup**: Use in-memory database with unique names for test isolation
  ```csharp
  var options = new DbContextOptionsBuilder<DbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
  ```

- **Test Data Setup**: Use `using` scopes for database operations in Arrange sections
  ```csharp
  using (var scope = _scopeService.CreateWriteScope())
  {
      scope.Entities.Add(new Entity { /* test data */ });
      await scope.SaveChangesAsync();
  }
  ```

- **Time-based Testing**: Use `FakeTimeService` for predictable time-dependent behavior
  ```csharp
  _timeService.AdvanceTime(TimeSpan.FromMinutes(1));
  entity.Timestamp.ShouldBe(_timeService.UtcNow);
  ```

- **Direct Database Verification**: Assert against database state directly rather than service APIs
  ```csharp
  // Assert - Check database directly instead of using service API
  var entityInDb = await _dbContext.Entities.FindAsync(entityId);
  entityInDb.ShouldNotBeNull();
  entityInDb.Property.ShouldBe(expectedValue);
  ```

- **Mock Verification**: Verify external service interactions using Moq
  ```csharp
  _mockService.Verify(s => s.MethodAsync(expectedParameter), Times.Once);
  ```

- **Comprehensive State Verification**: Test all relevant entity properties after operations
  ```csharp
  entity.Id.ShouldBe(expectedId);
  entity.Status.ShouldBe(ExpectedStatus.Value);
  entity.CreatedAt.ShouldBe(_timeService.UtcNow);
  entity.UpdatedAt.ShouldBe(_timeService.UtcNow);
  ```

- **Assertion Comments**: Add clarifying comments to explain complex assertion logic
  ```csharp
  // Assert - Check database directly instead of using service API
  var entityInDb = await _dbContext.Entities.FindAsync(entityId);
  
  // Task 1 was InProgress for this agent - should be Failed
  updatedTask1!.Status.ShouldBe(TaskStatus.Failed);
  
  // Task 2 was only Pending for this agent - should remain Pending
  updatedTask2!.Status.ShouldBe(TaskStatus.Pending);
  ```

- **Edge Case Documentation**: Include descriptive test method names that explain edge cases
  ```csharp
  WhenRegisteringAgentWithMinimalFields_ShouldSetRequiredPropertiesOnly()
  WhenAgentDoesNotExist_ShouldDoNothing()
  WhenKillingAgentWithInProgressTasks_ShouldFailDanglingTasks()
  ```

- **Test Isolation Verification**: Ensure tests don't affect each other's data
  ```csharp
  // Verify database contains exactly 2 agents
  var totalAgents = await _dbContext.Agents.CountAsync();
  totalAgents.ShouldBe(2);
  ```

## Composition and Dependency Injection

- **Constructor Injection**: Services injected via constructor for testability
- **Interface Segregation**: Small, focused interfaces (`IContextService`, `IFileSystemService`, `IGeminiService`)
- **Composition Root**: DI container setup in `Program.cs` via `services.AddAgentLauncherServices()`
- **Test Isolation**: Mock external dependencies, if the mocks are getting complicated, use a fake instead to simplify test setup.

## Entity Framework and Database Testing Patterns

- **In-Memory Database Isolation**: Each test class gets unique database instance
  ```csharp
  var options = new DbContextOptionsBuilder<CoordinationDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString())
      .Options;
  ```

- **Database Scope Pattern**: Use scoped operations for data setup and teardown
  ```csharp
  using (var scope = _scopeService.CreateWriteScope())
  {
      scope.Entities.Add(testEntity);
      await scope.SaveChangesAsync();
  }
  ```

- **Direct Database Assertions**: Verify state changes directly in database, not through service layer
  ```csharp
  // Assert - Check database directly instead of using service API
  var entityInDb = await _dbContext.Entities.FindAsync(entityId);
  entityInDb.ShouldNotBeNull();
  entityInDb.Status.ShouldBe(ExpectedStatus.Active);
  ```

- **Entity Builder Pattern**: Create complete test entities with all required properties
  ```csharp
  var testEntity = new Entity
  {
      Id = Guid.NewGuid().ToString(),
      RequiredProperty = "test-value",
      CreatedAt = _timeService.UtcNow,
      Status = EntityStatus.Active
  };
  ```

- **Time-Dependent Testing**: Use `FakeTimeService` for predictable temporal behavior
  ```csharp
  // Arrange with initial time
  entity.CreatedAt = _timeService.UtcNow.AddMinutes(-5);
  
  // Act with time advancement
  _timeService.AdvanceTime(TimeSpan.FromMinutes(1));
  
  // Assert with current time
  entity.UpdatedAt.ShouldBe(_timeService.UtcNow);
  ```

- **Multi-Entity Testing**: Test complex scenarios with multiple related entities
  ```csharp
  // Create related entities to test cascade behaviors
  var agent = new Agent { Id = agentId, Status = AgentStatus.Running };
  var task1 = new WorkItem { Id = "task-1", AgentId = agentId, Status = TaskStatus.InProgress };
  var task2 = new WorkItem { Id = "task-2", AgentId = agentId, Status = TaskStatus.Pending };
  ```

## Clean Code Principles (Kent Beck & Kevlin Henney)

- **Single Responsibility**: Each class has one reason to change
- **Intention-Revealing Names**: Method and variable names explain purpose clearly
- **Small Functions**: Functions do one thing well, readable at single level of abstraction
- **No Surprises**: Code behaves as its name suggests, no hidden side effects
- **Composition Over Inheritance**: Favor object composition and interfaces over class inheritance

## Code Formatting Standards

- **Multi-line Constructor Parameters**: Always place each parameter on its own line for better readability
  ```csharp
  public SomeClass(
      IDependency dependency,
      IService service)
  ```

- **Multi-line Method Signatures**: Use vertical formatting for methods with multiple parameters
  ```csharp
  public async Task<Result> MethodAsync(
      string parameter1,
      string parameter2,
      string parameter3)
  ```

- **Multi-line Return Statements**: Break long return statements with method chaining
  ```csharp
  return SomeResult
      .Failure($"Error message: {details}");
  ```

- **Multi-line String Concatenation**: Format complex error messages clearly
  ```csharp
  return Result
      .Failure($"First part: {value1}. " +
          $"Second part: {value2}");
  ```

**Formatting Rule**: Apply consistent multi-line formatting for constructors, methods, and complex return statements to improve readability and maintainability.

## Code Review Focus Areas

- **Design Patterns**: Look for SOLID principles, appropriate abstraction levels
- **Test Coverage**: Ensure behavior is tested, not just implementation details
- **Error Handling**: Validate error paths with explicit tests
- **Readability**: Code should read like well-written prose
- **Duplication**: Remove code duplication through extraction and composition

## Development Commands

- **Build**: `dotnet build` for compilation check
- **Test**: `dotnet test` for full test suite (required before any commit)
- **Test Watch**: `dotnet test --watch` for continuous testing during development
- **Coverage**: Use Coverlet integration for test coverage analysis

## Service Layer Patterns

- **Command Handler Pattern**: Separate handlers for each CLI command (`LaunchAgentCommandHandler`)
- **Service Abstractions**: Mock external processes (`IProcessLauncher`) and file system (`IFileSystemService`)
- **Logging**: Structured logging via `IAppLogger` interface for testability
- **Configuration**: Environment-based configuration with clear interfaces

**Essential Rule**: All new code follows TDD cycle. Tests pass after every change. Refactoring preserves existing behavior while improving structure.
