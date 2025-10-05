# FluentMigrator REPL - Proof of Concept

A Vue.js + Blazor WebAssembly application that allows you to edit and test FluentMigrator C# code offline without any AJAX requests, using WebAssembly technology.

## 🚀 Features

- **Monaco Editor**: Professional code editor (same as VS Code) with C# syntax highlighting
- **Real Migration Execution**: Executes actual FluentMigrator migrations using FluentMigrator.Runner
- **Vue.js UI**: Modern frontend built with Vue 3, Vite, and Bootstrap 5
- **No Server Required**: Runs entirely in the browser using WebAssembly
- **Vite Asset Management**: Full control over Bootstrap, CSS, and all frontend assets
- **Static Deployment**: Deploy to any static hosting service (GitHub Pages, Netlify, etc.)

## 📸 Screenshots

### Vue.js with Monaco Editor
![Vue + Monaco Editor Interface](https://github.com/user-attachments/assets/68acfb28-904f-4fbf-876e-17f3d00f0b33)

## 🏗️ Solution Structure

This repository contains a Visual Studio solution with two projects:

```
FluentMigrator.Repl.sln          # Visual Studio solution file
├── Solution Items/               # Documentation files
│   ├── README.md
│   ├── QUICKSTART.md
│   ├── IMPLEMENTATION.md
│   └── .gitignore
├── src/FluentMigratorRepl/      # Blazor WebAssembly C# project
│   ├── JSInterop/               # JavaScript interop for Vue communication
│   ├── Services/                # FluentMigrator execution service
│   └── wwwroot/                 # Static assets (includes Vite build output)
└── frontend/                    # Vue.js + Vite project (solution folder)
    ├── src/                     # Vue components and app logic
    ├── package.json             # NPM dependencies
    └── vite.config.js           # Vite configuration
```

## 🛠️ Getting Started

### Prerequisites

- .NET 9.0 SDK or later
- Node.js 18+ (for the Vue.js frontend)

### Using Visual Studio or Rider

1. Open `FluentMigrator.Repl.sln` in Visual Studio or JetBrains Rider
2. Build the solution to restore NuGet packages
3. Run the Blazor project from the IDE (serves on http://localhost:5122)
4. In a separate terminal, navigate to `frontend/` and run:
   ```bash
   npm install
   npm run dev
   ```
5. Visit http://localhost:5173 to use the application

### Using Command Line

### Using Command Line

```bash
# Terminal 1: Run Blazor WASM backend
cd src/FluentMigratorRepl
dotnet run
# Serves on http://localhost:5122

# Terminal 2: Run Vue dev server
cd frontend
npm install
npm run dev
# Serves on http://localhost:5173 with hot reload
```

Visit http://localhost:5173 to use the application.

### Building the Solution

```bash
# Build the entire solution
dotnet build FluentMigrator.Repl.sln --configuration Release
```

### Building for Production

### Building for Production

```bash
# Build Vue frontend (outputs to Blazor wwwroot)
cd frontend
npm run build

# Build Blazor WASM
cd ../src/FluentMigratorRepl
dotnet publish -c Release
```

The output will be in `bin/Release/net9.0/publish/wwwroot` and can be deployed to any static web host.

## 📝 How It Works

1. **Vue Frontend**: Provides the UI with Monaco Editor for professional code editing
2. **Blazor WASM Backend**: Loads in background and exposes `window.migrationInterop` to JavaScript
3. **JavaScript Interop Bridge**: Vue calls `migrationInterop.invokeMethodAsync('ExecuteMigrationAsync', code)`
4. **FluentMigrator Execution**: C# migration code runs in browser via WASM using FluentMigrator.Runner
5. **Results Display**: Database schema created by the migration is displayed in the output panel

## 🎯 Example Migrations

The application includes pre-defined example migrations in the C# assembly:

1. **CreateUsersTable**: Basic table creation with identity primary key and common column types
2. **CreatePostsTable**: Demonstrates foreign keys and relationships

Users can also write custom migration code in the Monaco editor.

## 🔧 Technical Details

### Architecture

The application uses a hybrid approach:
- **Frontend**: Vue.js 3 + Vite for the UI and asset management
- **Editor**: Monaco Editor for professional code editing experience
- **Backend**: Blazor WebAssembly (headless) for C# execution
- **Database**: In-memory SQLite configured for WebAssembly
- **Migration Framework**: FluentMigrator 7.1.0 with FluentMigrator.Runner

### Current Limitations

**SQLite Native Operations in WebAssembly**: Full SQLite support in the browser requires the `wasm-tools` workload:

```bash
dotnet workload install wasm-tools
```

Then enable in the `.csproj`:
```xml
<WasmBuildNative>true</WasmBuildNative>
```

Without this setup, SQLite may throw TypeInitializationException in the browser. The migrations execute correctly on regular .NET (dotnet run).

## 📦 Technologies Used

- **Vue.js 3**: Frontend framework with Composition API
- **Vite 7**: Build tool and development server
- **Monaco Editor**: Code editor with web workers for syntax highlighting
- **Blazor WebAssembly**: For running C# in the browser
- **FluentMigrator 7.1.0**: Database migration framework
- **FluentMigrator.Runner**: Migration execution engine
- **Microsoft.Data.Sqlite 9.0.9**: SQLite database provider
- **Bootstrap 5**: Responsive UI framework

## 📚 Documentation

- **QUICKSTART.md**: Get started in 5 minutes with step-by-step instructions
- **IMPLEMENTATION.md**: Technical architecture deep-dive and extension guide

## 🤝 Contributing

This is a proof of concept. Feel free to fork and enhance it!

## 📄 License

This project is open source and available under the MIT License.

## 🙏 Acknowledgments

- [FluentMigrator](https://github.com/fluentmigrator/fluentmigrator) - The excellent database migration framework
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - Microsoft's web framework for .NET
- [Vue.js](https://vuejs.org/) - Progressive JavaScript framework
- [Monaco Editor](https://microsoft.github.io/monaco-editor/) - The code editor that powers VS Code
- [Vite](https://vitejs.dev/) - Next generation frontend tooling
