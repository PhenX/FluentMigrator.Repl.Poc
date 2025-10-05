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
            <div>
              <div class="form-check form-check-inline me-3">
                <input class="form-check-input" type="checkbox" id="alwaysReset" v-model="alwaysReset" :disabled="!blazorReady || executing || listing">
                <label class="form-check-label" for="alwaysReset">
                  Always reset database
                </label>
              </div>
              
              <button class="btn btn-outline-danger btn-sm me-2" @click="resetDatabase()" :disabled="!blazorReady || executing || listing">
                💣 Reset database
              </button>
              
              <button class="btn btn-outline-info btn-sm me-2" @click="runMigration(RunType.List)" :disabled="!blazorReady || executing || listing">
                📋 List
              </button>
              <button class="btn btn-outline-warning btn-sm me-2" @click="runMigration(RunType.Preview)" :disabled="!blazorReady || executing || previewing">
                👁️ Preview
              </button>
              <button class="btn btn-primary" @click="runMigration(RunType.Run)" :disabled="!blazorReady || executing">
                ▶️ Run Migration
              </button>
            </div>
          </div>
          <div ref="editorContainer" class="editor-container"></div>
          <div class="examples mt-3">
            <h4>Quick Examples:</h4>
            <button class="btn btn-secondary btn-sm me-2" v-for="migration of samples" @click="loadExample(migration.code)">{{migration.title}}</button>
          </div>
        </div>
      </div>
      
      <div class="col-6">
        <div class="output-section">
          <div class="section-header">
            <h3>Output</h3>
            <button class="btn btn-secondary btn-sm" @click="clearOutput">Clear</button>
          </div>
          <pre class="output-container" v-html="output"></pre>
          
          <!-- Database Viewer Component -->
          <DatabaseViewer ref="dbViewer" :schema="dbSchema" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, useTemplateRef } from 'vue'
import monaco from './monaco-config'
import DatabaseViewer from './components/DatabaseViewer.vue'

const editorContainer = ref(null)
const output = ref('Ready to run migrations. Click \'Run Migration\' to execute your code.')
const blazorReady = ref(false)
const executing = ref(false)
const listing = ref(false)
const previewing = ref(false)
const dbName = ref('sample')
const dbSchema = ref(null)
const alwaysReset = ref(false)
const dbViewer = useTemplateRef("dbViewer")
let editor = null

enum RunType {
  Run = 0,
  List = 1,
  Preview = 2
}

import samples from './samples/index.js';

const initEditor = () => {
  if (editorContainer.value) {
    editor = monaco.editor.create(editorContainer.value, {
      value: samples[0].code,
      language: 'csharp',
      theme: 'vs-dark',
      automaticLayout: true,
      minimap: { enabled: false },
      fontSize: 13,
      scrollBeyondLastLine: false,
    })
  }
}

async function runMigration(runType: RunType) {
  if (!window.migrationInterop || executing.value) return

  if (alwaysReset.value || !dbName.value) {
    resetDatabase()
  }

  executing.value = true
  output.value = 'Executing migration...'

  try {
    const code = editor.getValue()
    output.value = await window.migrationInterop.invokeMethodAsync<string>('ExecuteMigrationAsync', dbName.value, code, runType)

    // Load the database schema
    const schemaJson = await window.migrationInterop.invokeMethodAsync<string>('GetDatabaseSchemaAsync')
    dbSchema.value = JSON.parse(schemaJson)

    // Refresh table data in the viewer
    if (dbViewer.value) {
      dbViewer.value.refreshAllData()
    }
  } catch (error) {
    output.value = `Error: ${error.message}`
  } finally {
    executing.value = false
  }
}

function resetDatabase() {
  dbName.value = `sample_${Date.now()}`
}

function clearOutput() {
  output.value = 'Ready to run migrations. Click \'Run Migration\' to execute your code.'
  dbSchema.value = null
}

function loadExample(code: string) {
  if (editor && code) {
    editor.setValue(code)
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
</script>

