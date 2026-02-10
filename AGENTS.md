# AGENTS.md - AI Agent Guide for Diffy

This document provides comprehensive context for AI coding assistants to effectively work on the Diffy project.


## Project Overview

**Diffy** is a cross-platform Git repository watcher with real-time diff visualization built using .NET 8 and Avalonia UI.

### Core Purpose
- Monitor Git repositories for file changes in real-time
- Display diffs with syntax highlighting and multiple viewing modes
- Provide Git commit history browsing
- Support multi-repository management via tabs

### Target Platforms
- Windows (Primary - Windows 11 tested)
- macOS (Intel x64 & Apple Silicon ARM64)
- Linux (x64 & ARM64)

### Current Version
- **Framework**: .NET 8.0
- **UI Framework**: Avalonia 11.3.11

## Quick Start for Agents

### Essential Commands
```bash
# Build the project
dotnet build

# Run the application
dotnet run --project src/Diffy.App/Diffy.App.csproj

# Run tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Format code
dotnet format

# Clean artifacts
dotnet clean
```

### Project Files You Should Read First
1. `README.md` - User-facing documentation
2. `src/Diffy.App/Program.cs` - Application entry point
3. `src/Diffy.App/App.axaml.cs` - DI configuration
4. `src/Diffy.Core/Interfaces/` - Service contracts

---

## Architecture & Design Patterns

### Architectural Style
**Clean Architecture with MVVM**

```
┌─────────────────────────────────────────────┐
│         Presentation Layer                  │
│  (Views, ViewModels, Controls, Converters)  │
│         Avalonia UI + ReactiveUI            │
└──────────────────┬──────────────────────────┘
                   │ depends on
┌──────────────────▼──────────────────────────┐
│         Application Services Layer          │
│  (Service Implementations in Diffy.App)     │
│  DiffService, GitService, FileWatcher, etc. │
└──────────────────┬──────────────────────────┘
                   │ implements
┌──────────────────▼──────────────────────────┐
│         Core Domain Layer                   │
│  (Interfaces, Models in Diffy.Core)         │
│  No external dependencies                   │
└─────────────────────────────────────────────┘
```

### Design Patterns Used

| Pattern | Usage | Location | Purpose |
|---------|-------|----------|---------|
| **MVVM** | Everywhere | Views + ViewModels | Separation of UI and logic |
| **Dependency Injection** | Constructor injection | App.axaml.cs | Service lifetime management |
| **Repository** | Git abstraction | IGitRepositoryFactory | Abstract LibGit2Sharp |
| **Factory** | Repository creation | GitRepositoryWrapper | Create Git repo instances |
| **Observer** | Reactive streams | ReactiveUI | Property change propagation |
| **Singleton** | Single instance | SingleInstanceService | Prevent multiple app instances |
| **Strategy** | Diff modes | DiffViewModel | Switch between side-by-side/inline |
| **Cache** | LRU caching | StringLRUCache | Performance optimization |
| **Service Locator** | App.Services | ViewModels | Access to service provider |

### MVVM Implementation

**View (XAML)**
- Pure UI markup with data binding
- No business logic
- Located in: `src/Diffy.App/Views/`

**ViewModel (C#)**
- Business logic and state
- Inherits from `ViewModelBase` (ReactiveUI.ReactiveObject)
- Uses `[Reactive]` attribute for property weaving via Fody
- Uses `ReactiveCommand` for async operations
- Located in: `src/Diffy.App/ViewModels/`

**Model (C#)**
- Pure data structures
- No business logic
- Located in: `src/Diffy.Core/Models/`

### Reactive Programming

**ReactiveUI Integration**
```csharp
// Property declaration with Fody weaving
[Reactive] public string SearchText { get; set; } = string.Empty;

// Reactive command
public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

RefreshCommand = ReactiveCommand.CreateFromTask(async () =>
{
    await LoadDataAsync();
});

// Observable subscriptions with throttling
this.WhenAnyValue(x => x.SearchText)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(async text => await FilterFilesAsync(text));
```

---

## Project Structure

### Solution Organization

```
Diffy/
├── Diffy.sln                      # Visual Studio solution
├── README.md                      # User documentation
├── CONTRIBUTING.md                # Development guidelines
├── AGENTS.md                      # This file - AI agent guide
├── LICENSE                        # MIT License
├── .editorconfig                  # Code style settings
├── .gitignore                     # Git ignore rules
├── build-win.ps1                  # Windows build script
├── github-release.ps1             # GitHub release automation
├── 
├── src/                           # Source code
│   ├── Diffy.App/                 # Main application (Avalonia UI)
│   │   ├── Program.cs             # Entry point + single instance
│   │   ├── App.axaml              # Application styles & resources
│   │   ├── App.axaml.cs           # DI configuration
│   │   ├── Diffy.App.csproj       # Project file
│   │   ├── FodyWeavers.xml        # ReactiveUI.Fody config
│   │   ├── 
│   │   ├── Assets/                # Icons and resources
│   │   │   └── Icons/             # SVG and bitmap icons
│   │   ├── 
│   │   ├── Caching/               # Performance optimization
│   │   │   └── StringLRUCache.cs  # LRU cache for diff content
│   │   ├── 
│   │   ├── Controls/              # Custom UI controls
│   │   │   ├── DiffMinimapControl.cs        # Minimap visualization
│   │   │   └── HighlightedTextBlock.cs      # Syntax-highlighted text
│   │   ├── 
│   │   ├── Converters/            # XAML value converters
│   │   │   └── StatusConverters.cs
│   │   ├── 
│   │   ├── Services/              # Service implementations
│   │   │   ├── DiffService.cs              # Diff generation
│   │   │   ├── GitService.cs               # Git operations
│   │   │   ├── FileWatcherService.cs       # File system monitoring
│   │   │   ├── SettingsService.cs          # User preferences
│   │   │   ├── SyntaxHighlightingService.cs # Code highlighting
│   │   │   ├── FileOperationService.cs     # File operations
│   │   │   ├── ShellIntegrationService.cs  # OS context menu
│   │   │   ├── SingleInstanceService.cs    # Single instance enforcement
│   │   │   ├── GitRepositoryWrapper.cs     # LibGit2Sharp abstraction
│   │   │   └── IntervalTree.cs             # Data structure for highlighting
│   │   ├── 
│   │   ├── Utilities/             # Helper classes
│   │   │   ├── PathUtilities.cs            # Cross-platform path handling
│   │   │   └── StringComparisonHelper.cs   # Platform-aware comparisons
│   │   ├── 
│   │   ├── ViewModels/            # MVVM ViewModels
│   │   │   ├── ViewModelBase.cs            # Base class
│   │   │   ├── MainWindowViewModel.cs      # Main window orchestration
│   │   │   ├── RepositoryTabViewModel.cs   # Repository tab logic
│   │   │   ├── DiffViewModel.cs            # Diff display controller
│   │   │   └── CommitHistoryViewModel.cs   # Git history viewer
│   │   └── 
│   │       └── Views/             # XAML views
│   │           ├── MainWindow.axaml        # Main window
│   │           ├── RepositoryTabView.axaml # Repository tab
│   │           ├── HistoryView.axaml       # Commit history
│   │           └── AboutWindow.axaml       # About dialog
│   └── 
│       └── Diffy.Core/            # Core business logic
│           ├── Diffy.Core.csproj  # Project file (no dependencies)
│           ├── 
│           ├── Interfaces/        # Service abstractions
│           │   ├── IDiffService.cs
│           │   ├── IGitService.cs
│           │   ├── IGitRepositoryFactory.cs
│           │   ├── IFileWatcherService.cs
│           │   ├── IFileOperationService.cs
│           │   ├── ISettingsService.cs
│           │   ├── IShellIntegrationService.cs
│           │   ├── ISingleInstanceService.cs
│           │   └── ISyntaxHighlightingService.cs
│           └── 
│               └── Models/        # Domain models
│                   ├── DiffModels.cs       # FileDiff, DiffLine, DiffBlock, etc.
│                   ├── FileStatus.cs       # Git file status
│                   ├── CommitInfo.cs       # Git commit data
│                   ├── RepositoryInfo.cs   # Repository metadata
│                   └── FilterSettings.cs   # User filter preferences
└── 
    └── tests/                     # Unit tests
        └── Diffy.Tests.Unit/      # xUnit tests
            ├── Diffy.Tests.Unit.csproj
            ├── Controls/          # Control tests
            ├── Converters/        # Converter tests
            ├── Models/            # Model tests
            ├── Services/          # Service tests (DiffService, GitService, etc.)
            ├── Utilities/         # Utility tests
            └── ViewModels/        # ViewModel tests
```

## Key Components

### 1. Service Layer (Diffy.App/Services/)

#### DiffService.cs
**Purpose**: Generate and parse file diffs using DiffPlex

**Responsibilities**:
- Generate unified diffs
- Parse diffs into structured objects
- Align diffs for side-by-side view
- Handle whitespace ignoring
- Detect binary files

---

#### GitService.cs
**Purpose**: Git repository operations via LibGit2Sharp

**Responsibilities**:
- Fetch changed files
- Retrieve commit history
- Get file content (HEAD vs working)
- Revert files to HEAD
- Handle Git safe.directory configuration

---

#### FileWatcherService.cs
**Purpose**: Monitor file system changes in real-time

**Features**:
- Uses `FileSystemWatcher`
- Debouncing/throttling for rapid changes
- Cross-platform file system events

---

#### SettingsService.cs
**Purpose**: Persist and retrieve user preferences

---

#### SyntaxHighlightingService.cs
**Purpose**: Apply TextMate-based syntax highlighting to code

**Features**:
- TextMate grammar support
- 100+ languages supported
- Incremental highlighting for performance

---

#### FileOperationService.cs
**Purpose**: File system operations (open, delete, move to trash)

---

#### SingleInstanceService.cs
**Purpose**: Ensure only one app instance runs at a time

**Implementation**:
- File lock mechanism (cross-platform)
- Named pipe communication for passing arguments
- Stale lock detection and cleanup

---

#### ShellIntegrationService.cs
**Purpose**: Integrate with OS shell (Windows Explorer context menu)

---

### 2. ViewModel Layer (Diffy.App/ViewModels/)

#### MainWindowViewModel.cs
**Purpose**: Main window orchestration and tab management

**Responsibilities**:
- Manage repository tabs
- Handle repository opening
- Theme switching
- Recent repositories menu
- Status bar updates

---

#### RepositoryTabViewModel.cs
**Purpose**: Repository-specific logic and file list management

**Responsibilities**:
- Load changed files
- Filter files by search
- Start/stop file watching
- Handle file operations
- Display diff for selected file

---

#### DiffViewModel.cs
**Purpose**: Control diff display and navigation

- Generate and display diffs
- Switch diff viewing modes
- Apply syntax highlighting
- Navigate between changes
- Handle whitespace ignoring

---

#### CommitHistoryViewModel.cs
**Purpose**: Display and navigate Git commit history

**Responsibilities**:
- Load paginated commit history
- Search commits by message/author/hash
- Display files changed in selected commit
- Show commit metadata (author, date, message)

---

### 3. View Layer (Diffy.App/Views/)

#### MainWindow.axaml
**Purpose**: Main application window layout

**Components**:
- Title bar with menu button
- Tab control for repositories
- Status bar
- Menu with recent repositories

**Data Binding**:
- Bound to `MainWindowViewModel`
- Two-way binding for `SelectedTab`

---

#### RepositoryTabView.axaml
**Purpose**: Repository tab content with file list and diff viewer

**Layout**:
```
┌─────────────────────────────────────┐
│ Search Box | Refresh | History      │
├──────────────┬──────────────────────┤
│              │                      │
│  File List   │    Diff Viewer       │
│  (ListView)  │  (AvaloniaEdit +     │
│              │   Minimap)           │
│              │                      │
└──────────────┴──────────────────────┘
```

**Data Binding**:
- Bound to `RepositoryTabViewModel`
- Two-way binding for `SelectedFile`

---

#### HistoryView.axaml
**Purpose**: Commit history dialog

**Layout**:
```
┌─────────────────────────────────────┐
│ Search Box                          │
├──────────────┬──────────────────────┤
│              │                      │
│  Commits     │  Changed Files       │
│  (DataGrid)  │  (ListView)          │
│              │                      │
└──────────────┴──────────────────────┘
```

**Data Binding**:
- Bound to `CommitHistoryViewModel`
- Two-way binding for `SelectedCommit`

---

### 4. Custom Controls (Diffy.App/Controls/)

#### DiffMinimapControl.cs
**Purpose**: Visual minimap showing all changes at a glance

**Rendering**:
- Green bars for additions
- Red bars for deletions
- Gray bars for unchanged lines
- Click to navigate to specific line

---

#### HighlightedTextBlock.cs
**Purpose**: Display text with syntax highlighting

**Features**:
- Renders `HighlightedSegment` list
- Supports foreground/background colors
- Performance-optimized for large files

---

### 5. Utilities

#### PathUtilities.cs
**Purpose**: Cross-platform path manipulation

---

#### StringComparisonHelper.cs
**Purpose**: Platform-appropriate string comparison

---

#### StringLRUCache.cs
**Purpose**: LRU (Least Recently Used) cache for diff content

---

## Coding Standards & Conventions

### General Guidelines

1. **No warnings allowed** - Build with `-warnaserror` flag
2. **Nullable reference types** - `<Nullable>enable</Nullable>` is on
3. **Use implicit usings** - `<ImplicitUsings>enable</ImplicitUsings>` is on

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| **Interfaces** | PascalCase with `I` prefix | `IGitService` |
| **Classes** | PascalCase | `GitService` |
| **Methods** | PascalCase | `GetChangedFilesAsync()` |
| **Properties** | PascalCase | `CurrentBranch` |
| **Fields (private)** | camelCase with `_` prefix | `_gitService` |
| **Parameters** | camelCase | `repoPath` |
| **Local variables** | camelCase | `changedFiles` |
| **Constants** | PascalCase | `MaxCommitsPerPage` |
| **Enums** | PascalCase (type and values) | `DiffMode.SideBySide` |


### Async/Await Guidelines

1. **Always use async/await** for I/O operations
2. **Suffix async methods** with `Async`
3. **ConfigureAwait** - Not needed in UI apps (Avalonia handles context)
4. **Avoid async void** (except event handlers)

```csharp
// Good
public async Task<List<FileStatus>> GetChangedFilesAsync(string repoPath)
{
    return await Task.Run(() =>
    {
        // LibGit2Sharp operations
    });
}

// Bad
public List<FileStatus> GetChangedFiles(string repoPath)
{
    // Blocking I/O
}
```

### Dependency Injection

**Registration** (in `App.axaml.cs`):
```csharp
services.AddSingleton<IGitService, GitService>();
services.AddTransient<MainWindowViewModel>();
```

**Constructor Injection**:
```csharp
public class RepositoryTabViewModel : ViewModelBase
{
    private readonly IGitService _gitService;
    private readonly IDiffService _diffService;
    
    public RepositoryTabViewModel(
        IGitService gitService,
        IDiffService diffService)
    {
        _gitService = gitService;
        _diffService = diffService;
    }
}
```

### ReactiveUI Conventions

**Property Declaration**:
```csharp
// Use [Reactive] attribute for auto-implemented properties
[Reactive] public string SearchText { get; set; } = string.Empty;

// Fody will weave this to:
// - Backing field
// - Property change notification
// - ReactiveUI integration
```

**Command Declaration**:
```csharp
public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

public RepositoryTabViewModel()
{
    RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);
}

private async Task RefreshAsync()
{
    // Implementation
}
```

**Observable Subscriptions**:
```csharp
// In constructor
this.WhenAnyValue(x => x.SearchText)
    .Throttle(TimeSpan.FromMilliseconds(300))
    .ObserveOn(RxApp.MainThreadScheduler)
    .Subscribe(async text => await FilterFilesAsync(text));
```


## Development Workflow

### Local Development Tips

**Hot Reload**:
```bash
# Use dotnet watch for hot reload during development
dotnet watch run --project src/Diffy.App/Diffy.App.csproj
```

**Debug Configuration** (launch.json):
```json
{
    "name": "Diffy Debug",
    "type": "coreclr",
    "request": "launch",
    "program": "${workspaceFolder}/src/Diffy.App/bin/Debug/net8.0/Diffy.dll",
    "args": [],
    "cwd": "${workspaceFolder}",
    "stopAtEntry": false,
    "console": "internalConsole"
}
```

**Avalonia DevTools** (Debug only):
- Press `F12` to open DevTools
- Inspect visual tree
- View property values
- Test data binding

---

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~DiffServiceTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~DiffServiceTests.GenerateDiff_WithIdenticalContent_ReturnsEmptyDiff"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run in watch mode
dotnet watch test --project tests/Diffy.Tests.Unit
```


## Build & Deployment

### Build Process

#### Debug Build
```bash
# Standard debug build
dotnet build

# Build specific project
dotnet build src/Diffy.App/Diffy.App.csproj
```

#### Release Build
```bash
# Build with optimizations
dotnet build -c Release

# Or use build script (Windows)
.\build-win.ps1
```

**Build Script** (`build-win.ps1`):
1. Cleans previous artifacts
2. Builds in Release configuration with `-warnaserror`
3. Copies output to `dist/` folder
4. Verifies assets are included

### Publishing

**Single-File Executable**:
```bash
# Windows x64
dotnet publish src/Diffy.App/Diffy.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true \
  -o dist/win-x64

# macOS ARM64 (Apple Silicon)
dotnet publish src/Diffy.App/Diffy.App.csproj \
  -c Release \
  -r osx-arm64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o dist/osx-arm64

# Linux x64
dotnet publish src/Diffy.App/Diffy.App.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o dist/linux-x64
```

**Runtime Identifiers (RIDs)**:
- Windows: `win-x64`, `win-arm64`
- macOS: `osx-x64`, `osx-arm64`
- Linux: `linux-x64`, `linux-arm64`

## Common Tasks

### Adding a New Feature

**Example: Add file sorting by last modified date**

1. **Update Model** (if needed):
```csharp
// Diffy.Core/Models/FileStatus.cs
public class FileStatus
{
    // Add new property
    public DateTime? LastModified { get; set; }
}
```

2. **Update Service**:
```csharp
// Diffy.App/Services/GitService.cs
public async Task<List<FileStatus>> GetChangedFilesAsync(string repoPath)
{
    // ... existing code ...
    
    // Add last modified info
    status.LastModified = File.GetLastWriteTime(fullPath);
}
```

3. **Update ViewModel**:
```csharp
// Diffy.App/ViewModels/RepositoryTabViewModel.cs
private void SortFilesByDate()
{
    var sorted = Files.OrderByDescending(f => f.LastModified).ToList();
    Files.Clear();
    foreach (var file in sorted)
    {
        Files.Add(file);
    }
}
```

4. **Update View** (if UI needed):
```xaml
<!-- Diffy.App/Views/RepositoryTabView.axaml -->
<Button Content="Sort by Date" Command="{Binding SortByDateCommand}" />
```

5. **Add Tests**:
```csharp
// tests/Diffy.Tests.Unit/Services/GitServiceTests.cs
[Fact]
public async Task GetChangedFilesAsync_ShouldIncludeLastModified()
{
    // Arrange & Act
    var files = await _sut.GetChangedFilesAsync(TestRepoPath);
    
    // Assert
    files.Should().AllSatisfy(f => f.LastModified.Should().HaveValue());
}
```


### Adding Platform-Specific Code

**Example: Windows Registry access**

1. **Use platform detection**:
```csharp
if (OperatingSystem.IsWindows())
{
    // Windows-specific code
    RegisterWindowsContextMenu();
}
else if (OperatingSystem.IsMacOS())
{
    // macOS-specific code
}
else if (OperatingSystem.IsLinux())
{
    // Linux-specific code
}
```

2. **Use conditional compilation** (if needed):
```csharp
#if WINDOWS
    // Windows-only code
    using Microsoft.Win32;
#endif

public void RegisterContextMenu()
{
#if WINDOWS
    var key = Registry.CurrentUser.CreateSubKey(@"Software\Classes\Directory\shell\Diffy");
    key.SetValue("", "Watch with Diffy");
#endif
}
```

---

## Troubleshooting Guide

### Common Build Errors

#### Error: "Nullable reference types not supported"
**Cause**: Using C# 8.0 features without enabling nullable
**Solution**: Ensure `<Nullable>enable</Nullable>` in .csproj

#### Error: "Fody weaving failed"
**Cause**: ReactiveUI.Fody not configured correctly
**Solution**: Verify `FodyWeavers.xml` exists and contains ReactiveUI configuration

#### Error: "LibGit2Sharp native binaries not found"
**Cause**: Native binaries not copied to output
**Solution**: Clean and rebuild (`dotnet clean && dotnet build`)


## Resources & References

### Official Documentation

- **Avalonia UI**: https://docs.avaloniaui.net/
- **ReactiveUI**: https://www.reactiveui.net/docs/
- **LibGit2Sharp**: https://github.com/libgit2/libgit2sharp/wiki
- **DiffPlex**: https://github.com/mmanela/diffplex
- **.NET**: https://docs.microsoft.com/dotnet/

### Project Docs

- **Diff.md**: Diff algorithm details (if exists)

