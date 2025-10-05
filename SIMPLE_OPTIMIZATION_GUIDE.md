# Simple Performance Optimization Guide

## Quick Wins Without Web Workers

This guide provides **low-complexity** alternatives to improve UI responsiveness without the complexity of Web Workers.

## Problem

When executing migrations, the UI freezes because Blazor's compilation and execution blocks the main thread. Users cannot:
- Edit code while a migration runs
- See real-time progress
- Cancel long-running operations

## Solution 1: Progressive UI Updates (Complexity: 1/10)

### Modify `frontend/src/App.vue`

```typescript
async function runMigration(runType: RunType) {
  if (!window.migrationInterop || executing.value) return;

  if (alwaysReset.value || !dbName.value) {
    resetDatabase();
  }

  executing.value = true;
  output.value = "🔄 Initializing migration executor...";
  
  // Allow Vue to update the UI
  await nextTick();
  await new Promise(resolve => setTimeout(resolve, 50));

  try {
    const code = editor.getValue();
    
    // Update UI before starting
    output.value = "⚙️ Compiling migration code...";
    await nextTick();
    await new Promise(resolve => setTimeout(resolve, 50));
    
    // Execute migration
    output.value = await window.migrationInterop.invokeMethodAsync<string>(
      "ExecuteMigrationAsync",
      dbName.value,
      code,
      runType,
    );

    // Load the database schema
    output.value += "\n\n📊 Loading database schema...";
    await nextTick();
    
    const schemaJson = await window.migrationInterop.invokeMethodAsync<string>(
      "GetDatabaseSchemaAsync",
    );
    dbSchema.value = JSON.parse(schemaJson);

    // Refresh table data in the viewer
    if (dbViewer.value) {
      output.value += "\n♻️ Refreshing database viewer...";
      await nextTick();
      dbViewer.value.refreshAllData();
    }
    
    output.value += "\n\n✅ Migration completed successfully!";
  } catch (error) {
    output.value = `❌ Error: ${error.message}`;
  } finally {
    executing.value = false;
  }
}
```

**Benefits:**
- ✅ UI updates between steps
- ✅ User sees progress
- ✅ Takes ~5 minutes to implement
- ✅ No architectural changes

**Limitations:**
- ❌ UI still freezes during Blazor compilation
- ❌ Cannot cancel mid-execution

---

## Solution 2: Add Cancellation Support (Complexity: 3/10)

### 1. Modify `MigrationExecutor.cs` to support cancellation

```csharp
private CancellationTokenSource? _currentCancellation;

public async Task ExecuteMigrationCodeAsync(
    string dbName, 
    string userCode, 
    MigrationRunType runType = MigrationRunType.Up,
    CancellationToken cancellationToken = default)
{
    try
    {
        _currentCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        
        // Validate user code is provided
        if (string.IsNullOrWhiteSpace(userCode))
        {
            _logger.LogWarning("No code provided. Please enter migration code in the editor.");
            return;
        }
        
        _logger.LogInformation("🔨 Compiling user code...");
        
        // Check for cancellation
        _currentCancellation.Token.ThrowIfCancellationRequested();
        
        // Compile the user's code
        var assembly = await CompileUserCodeAsync(userCode, _currentCancellation.Token);
        if (assembly == null)
        {
            return;
        }
        
        _logger.LogInformation("✅ Code compiled successfully");
        
        // Check for cancellation
        _currentCancellation.Token.ThrowIfCancellationRequested();
        
        // Create an in-memory SQLite database
        _lastConnectionString = $"Data Source={dbName}.db;";
        _logger.LogInformation("🔗 Connection: {LastConnectionString}", _lastConnectionString);
        
        // Execute migrations from the compiled assembly
        await ExecuteMigrationsAsync(_lastConnectionString, assembly, runType, _currentCancellation.Token);
        
        _logger.LogInformation("✅ Migration executed successfully!");
    }
    catch (OperationCanceledException)
    {
        _logger.LogWarning("⚠️ Migration cancelled by user");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "An error occurred during execution");
    }
    finally
    {
        _currentCancellation?.Dispose();
        _currentCancellation = null;
    }
}

public void CancelCurrentMigration()
{
    _currentCancellation?.Cancel();
}
```

### 2. Update `MigrationInterop.cs`

```csharp
[JSInvokable]
public void CancelMigration()
{
    _executor.CancelCurrentMigration();
}
```

### 3. Add Cancel Button in `App.vue`

```vue
<template>
  <button
    class="btn btn-danger"
    @click="cancelMigration"
    :disabled="!executing"
    v-if="executing"
  >
    ⛔ Cancel Migration
  </button>
</template>

<script setup lang="ts">
async function cancelMigration() {
  if (window.migrationInterop) {
    await window.migrationInterop.invokeMethodAsync("CancelMigration");
    output.value = "⚠️ Cancellation requested...";
  }
}
</script>
```

**Benefits:**
- ✅ Users can cancel long operations
- ✅ Better user control
- ✅ Moderate implementation effort

**Limitations:**
- ❌ Cancellation is cooperative (Blazor must check the token)
- ❌ UI still freezes during compilation

---

## Solution 3: Streaming Progress Updates (Complexity: 5/10)

### 1. Add Progress Callback to C#

```csharp
public class MigrationExecutor
{
    public event EventHandler<string>? ProgressUpdated;

    private void ReportProgress(string message)
    {
        ProgressUpdated?.Invoke(this, message);
        _logger.LogInformation(message);
    }

    public async Task ExecuteMigrationCodeAsync(...)
    {
        ReportProgress("🔨 Starting compilation...");
        
        // Compilation step
        var assembly = await CompileUserCodeAsync(userCode);
        
        ReportProgress("✅ Compilation complete");
        ReportProgress("🔗 Connecting to database...");
        
        // Database connection
        _lastConnectionString = $"Data Source={dbName}.db;";
        
        ReportProgress("▶️ Executing migration...");
        
        // Execute migration
        await ExecuteMigrationsAsync(_lastConnectionString, assembly, runType);
        
        ReportProgress("✅ Migration complete!");
    }
}
```

### 2. Stream Updates to JavaScript

```csharp
public class MigrationInterop
{
    private readonly IJSRuntime _jsRuntime;
    private readonly MigrationExecutor _executor;

    public MigrationInterop(MigrationExecutor executor, IJSRuntime jsRuntime)
    {
        _executor = executor;
        _jsRuntime = jsRuntime;
        
        // Subscribe to progress updates
        _executor.ProgressUpdated += OnProgressUpdated;
    }

    private async void OnProgressUpdated(object? sender, string message)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("window.onMigrationProgress", message);
        }
        catch
        {
            // Ignore JS interop errors
        }
    }
}
```

### 3. Handle Progress in Vue

```typescript
// Add to window in index.html
window.onMigrationProgress = function(message: string) {
  window.dispatchEvent(new CustomEvent('migration-progress', { 
    detail: { message } 
  }));
};
```

```vue
<script setup lang="ts">
onMounted(() => {
  window.addEventListener('migration-progress', (event: CustomEvent) => {
    output.value += '\n' + event.detail.message;
  });
});
</script>
```

**Benefits:**
- ✅ Real-time progress updates
- ✅ Better user feedback
- ✅ No architecture changes

**Limitations:**
- ❌ Still doesn't prevent UI freeze
- ❌ Adds some complexity

---

## Solution 4: Optimize Compilation (Complexity: 4/10)

### Cache Compiled Assemblies

```csharp
public class MigrationExecutor
{
    private static readonly Dictionary<string, Assembly> _compilationCache 
        = new Dictionary<string, Assembly>();

    private async Task<Assembly?> CompileUserCodeAsync(string userCode)
    {
        // Create a hash of the code
        var codeHash = ComputeHash(userCode);
        
        // Check cache
        if (_compilationCache.TryGetValue(codeHash, out var cachedAssembly))
        {
            _logger.LogInformation("♻️ Using cached compilation");
            return cachedAssembly;
        }
        
        _logger.LogInformation("🔨 Compiling user code...");
        
        // ... existing compilation code ...
        
        // Cache the result
        if (assembly != null)
        {
            _compilationCache[codeHash] = assembly;
        }
        
        return assembly;
    }

    private string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }
}
```

**Benefits:**
- ✅ Faster subsequent executions
- ✅ Reduces compilation overhead
- ✅ Simple to implement

**Limitations:**
- ❌ First execution still slow
- ❌ Doesn't help with new code

---

## Solution 5: Visual Feedback During Freeze (Complexity: 2/10)

### Add Loading Spinner

```vue
<template>
  <div class="editor-section">
    <!-- Loading overlay -->
    <div v-if="executing" class="loading-overlay">
      <div class="spinner-border text-primary" role="status">
        <span class="visually-hidden">Loading...</span>
      </div>
      <p class="mt-3">Compiling and executing migration...</p>
      <p class="text-muted">This may take a few seconds</p>
    </div>
    
    <!-- Existing editor content -->
    <div ref="editorContainer" class="editor-container" 
         :class="{ 'opacity-50': executing }">
    </div>
  </div>
</template>

<style scoped>
.loading-overlay {
  position: absolute;
  top: 50%;
  left: 50%;
  transform: translate(-50%, -50%);
  z-index: 1000;
  text-align: center;
  background: rgba(255, 255, 255, 0.95);
  padding: 2rem;
  border-radius: 8px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
}
</style>
```

**Benefits:**
- ✅ Clear visual feedback
- ✅ Sets user expectations
- ✅ Very easy to implement

**Limitations:**
- ❌ Doesn't solve the freeze
- ❌ Only improves perception

---

## Recommended Implementation Order

1. **Start with Solution 5** (Visual Feedback) - 30 minutes
   - Immediate improvement to user experience
   - No risk

2. **Add Solution 1** (Progressive Updates) - 30 minutes
   - Better progress indication
   - Low risk

3. **Implement Solution 4** (Caching) - 2 hours
   - Improves performance for repeated runs
   - Moderate complexity

4. **Add Solution 2** (Cancellation) - 4 hours
   - Better user control
   - Moderate complexity

5. **Consider Solution 3** (Streaming) - 4 hours
   - If users want real-time feedback
   - Higher complexity

## Comparison Table

| Solution | Effort | Impact | Risk | User Value |
|----------|--------|--------|------|------------|
| Progressive Updates | 30 min | Low | None | Medium |
| Cancellation | 4 hours | Medium | Low | High |
| Streaming Progress | 4 hours | Medium | Low | Medium |
| Compilation Cache | 2 hours | High* | Low | High* |
| Visual Feedback | 30 min | Low | None | Medium |

\* For repeated code execution

## When to Consider Web Workers?

Only consider Web Workers if:
- ✅ All simple optimizations have been implemented
- ✅ Users still experience significant freezing (>5 seconds)
- ✅ The app is mission-critical
- ✅ You have 80+ hours for development and testing
- ✅ You need true parallelism (not just responsiveness)

## Conclusion

Start with the simple solutions above. They provide **80% of the benefit with 10% of the effort** compared to Web Workers. Most users won't notice the difference, and you'll save weeks of development time.
