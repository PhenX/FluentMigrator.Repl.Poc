<template>
  <header class="app-header">
    <div class="container">
      <h1>🔧 FluentMigrator REPL</h1>
      <p>
        Try FluentMigrator migrations with SQLite in your browser - No server
        required!
      </p>
    </div>
  </header>

  <div class="container-fluid mt-2">
    <div class="row">
      <div class="col-6">
        <div class="editor-section">
          <div class="section-header mb-2">
            <h3>C# Migration Code</h3>
            <div>
              <div class="form-check form-check-inline me-3">
                <input
                  class="form-check-input"
                  type="checkbox"
                  id="alwaysReset"
                  v-model="alwaysReset"
                  :disabled="!blazorReady || executing || listing"
                />
                <label class="form-check-label" for="alwaysReset">
                  Always reset database
                </label>
              </div>
              <button
                class="btn btn-outline-danger btn-sm me-2"
                @click="resetDatabase()"
                :disabled="!blazorReady || executing || listing"
              >
                💣 Reset database
              </button>
              <button
                class="btn btn-outline-info btn-sm me-2"
                @click="runMigration(RunType.List)"
                :disabled="!blazorReady || executing || listing"
              >
                📋 List
              </button>
              <button
                class="btn btn-outline-warning btn-sm me-2"
                @click="runMigration(RunType.Preview)"
                :disabled="!blazorReady || executing || previewing"
              >
                👁️ Preview
              </button>
              <button
                class="btn btn-primary"
                @click="runMigration(RunType.Run)"
                :disabled="!blazorReady || executing"
              >
                ▶️ Run Migration
              </button>
            </div>
          </div>
          <div ref="editorContainer" class="editor-container"></div>
          <div class="examples mt-3">
            <h4>Quick Examples:</h4>
            <button
              class="btn btn-secondary btn-sm me-2"
              v-for="migration of samples"
              @click="loadExample(migration.code)"
            >
              {{ migration.title }}
            </button>
          </div>
        </div>
      </div>

      <div class="col-6">
        <BTabs small>
          <BTab active>
            <template #title>🖥️ Output</template>
            <div class="output-section">
              <pre class="output-container m-0 p-2" v-html="output"></pre>
            </div>
          </BTab>
          <BTab>
            <template #title
              >📊 Database viewer
              <BBadge variant="info" v-if="dbSchema">{{
                dbSchema.tables.length + dbSchema.views.length
              }}</BBadge>
            </template>
            <DatabaseViewer ref="dbViewer" :schema="dbSchema" />
          </BTab>
          <BTab>
            <template #title>ℹ️ Important notes</template>
            <div class="alert alert-info my-3">
              ℹ️ Because this application runs entirely in your browser using
              WebAssembly and using the SQLite provider, there are some
              important limitations to be aware of.
            </div>
            <ul>
              <li>
                <strong>Database Persistence:</strong> The SQLite database is
                stored in the browser's memory and will be lost when you refresh
                or close the page. Use the "Always reset database" option to
                start fresh with each migration run.
              </li>
              <li>
                <strong>Limited Database Features:</strong> SQLite does not
                support all features of more robust databases like SQL Server or
                PostgreSQL. Some FluentMigrator features may not work as
                expected.
              </li>
            </ul>
          </BTab>
        </BTabs>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, useTemplateRef } from "vue";
import monaco from "./monaco-config";
import DatabaseViewer from "./components/DatabaseViewer.vue";

const editorContainer = ref(null);
const output = ref(
  "Ready to run migrations. Click 'Run Migration' to execute your code.",
);
const blazorReady = ref(false);
const executing = ref(false);
const listing = ref(false);
const previewing = ref(false);
const dbName = ref("sample");
const dbSchema = ref<Schema>(null);
const alwaysReset = ref(false);
const dbViewer = useTemplateRef("dbViewer");
let editor = null;

enum RunType {
  Run = 0,
  List = 1,
  Preview = 2,
}

import samples from "./samples/index.js";
import { BBadge, BTab, BTabs } from "bootstrap-vue-next";
import { Schema } from "./types";

const initEditor = () => {
  if (editorContainer.value) {
    editor = monaco.editor.create(editorContainer.value, {
      value: samples[0].code,
      language: "csharp",
      theme: "vs-light",
      automaticLayout: true,
      minimap: { enabled: true },
      fontSize: 13,
      scrollBeyondLastLine: false,
    });
  }
};

async function runMigration(runType: RunType) {
  if (!window.migrationInterop || executing.value) return;

  if (alwaysReset.value || !dbName.value) {
    resetDatabase();
  }

  executing.value = true;
  output.value = "Executing migration...";

  try {
    const code = editor.getValue();
    output.value = await window.migrationInterop.invokeMethodAsync<string>(
      "ExecuteMigrationAsync",
      dbName.value,
      code,
      runType,
    );

    // Load the database schema
    const schemaJson = await window.migrationInterop.invokeMethodAsync<string>(
      "GetDatabaseSchemaAsync",
    );
    dbSchema.value = JSON.parse(schemaJson);

    // Refresh table data in the viewer
    if (dbViewer.value) {
      dbViewer.value.refreshAllData();
    }
  } catch (error) {
    output.value = `Error: ${error.message}`;
  } finally {
    executing.value = false;
  }
}

function resetDatabase() {
  dbName.value = `sample_${Date.now()}`;
}

function loadExample(code: string) {
  if (editor && code) {
    editor.setValue(code);
  }
}

onMounted(() => {
  initEditor();

  // Wait for Blazor WASM to be ready
  window.addEventListener("blazor-ready", () => {
    blazorReady.value = true;
    output.value = "Blazor WASM loaded! Ready to execute migrations.";
  });

  // Check if already ready
  if (window.migrationInterop) {
    blazorReady.value = true;
    output.value = "Blazor WASM loaded! Ready to execute migrations.";
  }
});
</script>
