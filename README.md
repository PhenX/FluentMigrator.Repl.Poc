# FluentMigrator REPL - Proof of Concept

A Vue-inspired Blazor WebAssembly application that allows you to edit and test FluentMigrator C# code offline without any AJAX requests, using WebAssembly technology.

## 🚀 Features

- **In-Browser Code Editing**: Write FluentMigrator migration code directly in the browser
- **Real-Time Analysis**: Instantly analyze migration code to see what tables, columns, and indexes would be created
- **No Server Required**: Runs entirely in the browser using WebAssembly
- **Example Migrations**: Quick-load example migrations to get started
- **Modern UI**: Clean, responsive interface with split-pane layout

## 📸 Screenshots

### Main Interface
![FluentMigrator REPL Interface](https://github.com/user-attachments/assets/22373623-6a39-4e21-9791-e4f42b4efcfa)

### Analyzing Migrations with Indexes
![Migration Analysis](https://github.com/user-attachments/assets/1d45f918-3f69-48a1-af26-0fe28881fd0c)

## 🏗️ Architecture

- **Frontend**: Blazor WebAssembly (C#)
- **Database**: SQLite (configured for WebAssembly)
- **Migration Framework**: FluentMigrator
- **Styling**: Custom CSS with responsive design

## 🛠️ Getting Started

### Prerequisites

- .NET 9.0 SDK or later

### Running Locally

1. Clone the repository:
   ```bash
   git clone https://github.com/PhenX/FluentMigrator.Repl.Poc.git
   cd FluentMigrator.Repl.Poc
   ```

2. Navigate to the project directory:
   ```bash
   cd src/FluentMigratorRepl
   ```

3. Run the application:
   ```bash
   dotnet run
   ```

4. Open your browser and navigate to `http://localhost:5122`

### Building for Production

```bash
cd src/FluentMigratorRepl
dotnet publish -c Release
```

The output will be in `bin/Release/net9.0/publish/wwwroot` and can be deployed to any static web host.

## 📝 How It Works

1. **Code Editor**: Write your FluentMigrator migration code in the editor pane
2. **Run Migration**: Click the "Run Migration" button to analyze your code
3. **View Results**: The output pane shows what your migration would do:
   - Tables that would be created
   - Columns with their types and constraints
   - Indexes that would be added
   - Down migration actions

## 🎯 Example Migrations

The application includes three example migrations:

1. **Simple Table**: Basic table creation with common column types
2. **With Foreign Keys**: Demonstrates relationships between tables
3. **With Indexes**: Shows how to create various types of indexes

## 🔧 Technical Details

### Current Limitations

- **Static Analysis Only**: Due to WebAssembly limitations without additional workload installation, the application performs static code analysis rather than actual database execution
- **No Dynamic Compilation**: User code is analyzed using regex patterns rather than Roslyn compilation

### Future Enhancements

To enable full database execution in the browser:

1. Install the `wasm-tools` workload:
   ```bash
   dotnet workload install wasm-tools
   ```

2. Enable native build in the project file:
   ```xml
   <WasmBuildNative>true</WasmBuildNative>
   ```

This would allow actual SQLite operations to run in the browser using WebAssembly.

## 📦 Technologies Used

- **Blazor WebAssembly**: For running C# in the browser
- **FluentMigrator**: Database migration framework
- **Microsoft.Data.Sqlite**: SQLite database provider
- **SQLitePCLRaw**: SQLite native bindings for WebAssembly

## 🤝 Contributing

This is a proof of concept. Feel free to fork and enhance it!

## 📄 License

This project is open source and available under the MIT License.

## 🙏 Acknowledgments

- [FluentMigrator](https://github.com/fluentmigrator/fluentmigrator) - The excellent database migration framework
- [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) - Microsoft's web framework for .NET
