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

## Composition and Dependency Injection

- **Constructor Injection**: Services injected via constructor for testability
- **Interface Segregation**: Small, focused interfaces (`IContextService`, `IFileSystemService`, `IGeminiService`)
- **Composition Root**: DI container setup in `Program.cs` via `services.AddAgentLauncherServices()`
- **Test Isolation**: Mock external dependencies, if the mocks are getting complicated, use a fake instead to simplify test setup.

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
