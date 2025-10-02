<template>
  <header class="app-header">
    <div class="container">
      <h1>🔧 FluentMigrator REPL</h1>
      <p>Test FluentMigrator migrations with SQLite in your browser - No server required!</p>
    </div>
  </header>
  
  <div class="container-fluid mt-4">
    <div class="row">
      <div class="col-6">
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
      
      <div class="col-6">
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

