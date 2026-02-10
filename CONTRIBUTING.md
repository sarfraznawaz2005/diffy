# Contributing to Diffy

Thank you for considering contributing to Diffy! We welcome contributions from everyone.

## Code of Conduct

This project adheres to a code of conduct. By participating, you are expected to uphold this code. Please report unacceptable behavior to [your-email@example.com].

## How to Contribute

### Reporting Bugs

Before creating bug reports, please check the existing issues as you might find that the problem has already been reported or resolved.

When creating a bug report, please include as many details as possible:

- **Use a clear and descriptive title** for the issue
- **Describe the exact steps to reproduce** the problem
- **Provide specific examples** to demonstrate the steps
- **Describe the behavior** you observed and what you expected to see
- **Include screenshots** if applicable
- **Mention your operating system** (Windows, macOS, Linux) and version
- **Specify the Diffy version** you're using

### Suggesting Enhancements

Enhancement suggestions are tracked as [GitHub issues](https://github.com/yourusername/diffy/issues).

When suggesting an enhancement, please:

- **Use a clear and descriptive title**
- **Provide a detailed description** of the suggested enhancement
- **Explain why** this enhancement would be useful
- **List some examples** of how this feature would be used

### Pull Requests

We welcome pull requests! Here's a quick guide to get you started:

#### Setup Development Environment

```bash
# Clone the repository
git clone https://github.com/yourusername/diffy.git
cd diffy

# Create a feature branch
git checkout -b feature/your-feature-name

# Install dependencies
dotnet restore

# Build the project
dotnet build

# Run tests
dotnet test
```

#### Development Workflow

1. **Fork and Branch**: Fork the repository and create a new branch for your changes
2. **Write Code**: Follow the coding standards and conventions
3. **Write Tests**: Ensure your changes are covered by tests
4. **Build and Test**: Run `dotnet build` and `dotnet test`
5. **Format Code**: Run `dotnet-format` to ensure consistent formatting
6. **Commit**: Write clear, descriptive commit messages
7. **Push**: Push your branch to your fork
8. **Create PR**: Create a pull request with a clear description

#### Coding Standards

- Follow existing code conventions in the project
- Use meaningful variable and method names
- Keep methods short and focused
- Add XML comments for public APIs
- Follow C# naming conventions (PascalCase for public members, camelCase for locals)

#### Commit Message Format

We follow a conventional commit format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

Types:
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation changes
- `style`: Code style changes (formatting, etc.)
- `refactor`: Code refactoring
- `test`: Adding or updating tests
- `chore`: Maintenance tasks

Example:
```
feat(diff): add support for ignoring whitespace in diffs

Closes #123
```

#### Code Review Process

All pull requests must pass:
- **CI checks**: Build and test on all platforms
- **Code review**: At least one maintainer approval
- **Formatting**: No whitespace or style issues

The maintainer may request changes, clarifications, or additional tests before merging.

## Testing

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Diffy.Tests.Unit

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run in watch mode
dotnet watch test --project tests/Diffy.Tests.Unit
```

### Writing Tests

- Write unit tests for new functionality
- Aim for high test coverage (target: 85%+)
- Use `xUnit` framework with `Moq` for mocking
- Follow the Arrange-Act-Assert pattern

Example:
```csharp
[Fact]
public void MethodName_StateUnderTest_ExpectedBehavior()
{
    // Arrange
    var service = new MyService();
    var input = "test";

    // Act
    var result = service.Process(input);

    // Assert
    result.Should().NotBeNull();
    result.Value.Should().Be("expected");
}
```

## Documentation

- Update the README.md for user-facing changes
- Update inline code comments for complex logic
- Add XML documentation comments for public APIs
- Update AGENTS.md if you change architecture or patterns

## Architecture Guidelines

### MVVM Pattern

- Maintain strict separation between Views (XAML) and ViewModels (C#)
- Use data binding for communication
- Keep business logic in ViewModels or Services, not Views

### Dependency Injection

- Register services in `App.axaml.cs`
- Use constructor injection for dependencies
- Prefer interfaces over concrete implementations

### ReactiveUI

- Use `ReactiveCommand` for command implementations
- Use `WhenAnyValue` for reactive property subscriptions
- Throttle rapid updates (e.g., search input)

### Cross-Platform Considerations

- Test on Windows, macOS, and Linux when possible
- Use `OperatingSystem.IsWindows()`, `IsMacOS()`, `IsLinux()` for platform detection
- Avoid platform-specific APIs in shared code
- Use `Avalonia.Application.Current.ActualThemeVariant` for theme detection

## Project Structure

```
Diffy/
├── src/
│   ├── Diffy.App/
│   │   ├── Views/           # XAML views
│   │   ├── ViewModels/      # MVVM view models
│   │   ├── Services/        # Application services
│   │   ├── Controls/        # Custom controls
│   │   ├── Converters/      # Value converters
│   │   ├── Utilities/       # Helper classes
│   │   └── Caching/         # Caching implementations
│   └── Diffy.Core/
│       ├── Interfaces/      # Service interfaces
│       └── Models/          # Data models
└── tests/
    └── Diffy.Tests.Unit/   # Unit tests
```

## Getting Help

- **Documentation**: See [AGENTS.md](AGENTS.md) and [diffs.md](diffs.md)
- **Discussions**: Join the [GitHub Discussions](https://github.com/yourusername/diffy/discussions)
- **Issues**: Check existing [GitHub Issues](https://github.com/yourusername/diffy/issues)

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
