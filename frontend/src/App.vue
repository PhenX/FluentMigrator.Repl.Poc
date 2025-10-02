<template>
  <div class="app">
    <header class="app-header">
      <div class="container">
        <h1>🔧 FluentMigrator REPL</h1>
        <p>Test FluentMigrator migrations with SQLite in your browser - No server required!</p>
      </div>
    </header>
    
    <div class="container-fluid mt-4">
      <div class="row">
        <div class="col-md-6">
          <div class="editor-section">
            <div class="section-header">
              <h3>C# Migration Code</h3>
              <button class="btn btn-primary" @click="runMigration" :disabled="!blazorReady || executing">
                ▶️ Run Migration
              </button>
            </div>
            <div ref="editorContainer" class="editor-container"></div>
            <div class="examples mt-3">
              <h4>Quick Examples:</h4>
              <button class="btn btn-secondary btn-sm me-2" @click="loadExample('simple')">Simple Table</button>
              <button class="btn btn-secondary btn-sm me-2" @click="loadExample('foreignKeys')">With Foreign Keys</button>
              <button class="btn btn-secondary btn-sm" @click="loadExample('indexes')">With Indexes</button>
            </div>
          </div>
        </div>
        
        <div class="col-md-6">
          <div class="output-section">
            <div class="section-header">
              <h3>Output</h3>
              <button class="btn btn-secondary btn-sm" @click="clearOutput">Clear</button>
            </div>
            <pre class="output-container">{{ output }}</pre>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
import { ref, onMounted } from 'vue'
import monaco from './monaco-config'

export default {
  name: 'App',
  setup() {
    const editorContainer = ref(null)
    const output = ref('Ready to run migrations. Click \'Run Migration\' to execute your code.')
    const blazorReady = ref(false)
    const executing = ref(false)
    let editor = null

    const defaultCode = `using FluentMigrator;

[Migration(202501010001)]
public class CreateUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Username").AsString(50).NotNullable()
            .WithColumn("Email").AsString(100).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("Users");
    }
}`

    const examples = {
      simple: defaultCode,
      foreignKeys: `using FluentMigrator;

[Migration(202501010002)]
public class CreatePostsTable : Migration
{
    public override void Up()
    {
        Create.Table("Posts")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
                .ForeignKey("Users", "Id")
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Content").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }

    public override void Down()
    {
        Delete.Table("Posts");
    }
}`,
      indexes: `using FluentMigrator;

[Migration(202501010003)]
public class CreateOrdersWithIndexes : Migration
{
    public override void Up()
    {
        Create.Table("Orders")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CustomerId").AsInt32().NotNullable()
            .WithColumn("OrderNumber").AsString(50).NotNullable()
            .WithColumn("OrderDate").AsDateTime().NotNullable()
            .WithColumn("Status").AsString(20).NotNullable();
            
        Create.Index("IX_Orders_CustomerId")
            .OnTable("Orders")
            .OnColumn("CustomerId");
            
        Create.Index("IX_Orders_OrderDate")
            .OnTable("Orders")
            .OnColumn("OrderDate")
            .Descending();
            
        Create.Index("IX_Orders_OrderNumber")
            .OnTable("Orders")
            .OnColumn("OrderNumber")
            .Unique();
    }

    public override void Down()
    {
        Delete.Index("IX_Orders_OrderNumber");
        Delete.Index("IX_Orders_OrderDate");
        Delete.Index("IX_Orders_CustomerId");
        Delete.Table("Orders");
    }
}`
    }

    const initEditor = () => {
      if (editorContainer.value) {
        editor = monaco.editor.create(editorContainer.value, {
          value: defaultCode,
          language: 'csharp',
          theme: 'vs-dark',
          automaticLayout: true,
          minimap: { enabled: false },
          fontSize: 14,
          scrollBeyondLastLine: false,
        })
      }
    }

    const runMigration = async () => {
      if (!window.migrationInterop || executing.value) return
      
      executing.value = true
      output.value = 'Executing migration...'
      
      try {
        const code = editor.getValue()
        const result = await window.migrationInterop.invokeMethodAsync('ExecuteMigrationAsync', code)
        output.value = result
      } catch (error) {
        output.value = `Error: ${error.message}`
      } finally {
        executing.value = false
      }
    }

    const clearOutput = () => {
      output.value = 'Ready to run migrations. Click \'Run Migration\' to execute your code.'
    }

    const loadExample = (exampleName) => {
      if (editor && examples[exampleName]) {
        editor.setValue(examples[exampleName])
      }
    }

    onMounted(() => {
      initEditor()
      
      // Wait for Blazor WASM to be ready
      window.addEventListener('blazor-ready', () => {
        blazorReady.value = true
        output.value = 'Blazor WASM loaded! Ready to execute migrations.'
      })
      
      // Check if already ready
      if (window.migrationInterop) {
        blazorReady.value = true
        output.value = 'Blazor WASM loaded! Ready to execute migrations.'
      }
    })

    return {
      editorContainer,
      output,
      blazorReady,
      executing,
      runMigration,
      clearOutput,
      loadExample
    }
  }
}
</script>

<style scoped>
.app {
  min-height: 100vh;
  background: #f5f5f5;
}

.app-header {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  padding: 2rem 0;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}

.app-header h1 {
  margin: 0;
  font-size: 2rem;
}

.app-header p {
  margin: 0.5rem 0 0 0;
  opacity: 0.9;
}

.editor-section, .output-section {
  background: white;
  border-radius: 8px;
  padding: 1.5rem;
  box-shadow: 0 2px 8px rgba(0,0,0,0.1);
  height: calc(100vh - 250px);
  display: flex;
  flex-direction: column;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1rem;
}

.section-header h3 {
  margin: 0;
  font-size: 1.25rem;
}

.editor-container {
  flex: 1;
  border: 1px solid #ddd;
  border-radius: 4px;
  overflow: hidden;
}

.output-container {
  flex: 1;
  background: #f8f9fa;
  border: 1px solid #ddd;
  border-radius: 4px;
  padding: 1rem;
  overflow-y: auto;
  font-family: 'Courier New', monospace;
  font-size: 0.875rem;
  white-space: pre-wrap;
  margin: 0;
}

.examples {
  padding-top: 1rem;
  border-top: 1px solid #e0e0e0;
}

.examples h4 {
  font-size: 1rem;
  margin-bottom: 0.5rem;
}
</style>
