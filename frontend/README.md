# FluentMigrator REPL - Vue Frontend

This is the Vue.js frontend for the FluentMigrator REPL application. It uses Vite for fast development and Monaco Editor for the code editor.

## Architecture

- **Vue 3** - Frontend framework
- **Vite** - Build tool and dev server
- **Monaco Editor** - Code editor (same as VS Code)
- **Bootstrap 5** - UI framework
- **Blazor WebAssembly** - C# execution engine (headless, no UI)

The Vue frontend communicates with Blazor WASM via JavaScript interop to execute C# migration code in the browser.

## Development

```bash
# Install dependencies
npm install

# Start dev server (with hot reload)
npm run dev

# Build for production
npm run build
```

## Development Workflow

1. Start the Blazor WASM dev server:
   ```bash
   cd ../src/FluentMigratorRepl
   dotnet run
   ```
   This runs on http://localhost:5122

2. In another terminal, start the Vue dev server:
   ```bash
   cd frontend
   npm run dev
   ```
   This runs on http://localhost:5173 and proxies `/_framework` requests to the Blazor server

3. Open http://localhost:5173 in your browser

## Production Build

```bash
# Build Vue frontend
npm run build

# Build Blazor WASM. Do not use publish to disable trimming
cd ../src/FluentMigratorRepl
dotnet build -c Release

# Deploy the entire bin/Release/net9.0/wwwroot folder
```

The build process outputs to `../src/FluentMigratorRepl/wwwroot/`, which is served by the Blazor app.

## Features

- **Monaco Editor** - Full featured code editor with C# syntax highlighting
- **Real-time Execution** - Execute FluentMigrator migrations via Blazor WASM
- **No Server Required** - Everything runs in the browser
- **Static Deployment** - Can be deployed to any static host (GitHub Pages, Netlify, etc.)
