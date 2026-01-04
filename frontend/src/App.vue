<template>
  <header class="app-header">
    <div class="container d-flex justify-content-between align-items-center">
      <div>
        <h1>🔧 FluentMigrator REPL</h1>
        <p>
          Try FluentMigrator migrations with SQLite in your browser - No server
          required!
        </p>
      </div>
      <ThemeToggle />
    </div>
  </header>

  <div class="container-fluid mt-2">
      <BOverlay
        :show="loading"
        spinner-variant="primary"
        spinner-type="grow"
        :opacity="0.5"
        class="row"
      >
      <div class="col-12 col-lg-6">
        <div class="editor-section">
          <div class="section-header mb-2">
            <button class="btn btn-sm btn-outline-dark" @click="copyUrl">
              📋 Copy URL
            </button>
            <div>
              <BForm class="d-flex flex-row align-items-center flex-wrap">
                <BFormCheckbox
                  id="alwaysReset"
                  v-model="alwaysReset"
                  switch
                  class="ms-2"
                >
                  <span class="d-none d-xl-inline">Always reset DB</span>
                </BFormCheckbox>

                <button
                  class="btn btn-outline-danger btn-sm ms-2"
                  @click="resetDatabase"
                  type="button"
                >
                  💣 Reset DB
                </button>
                <button
                  class="btn btn-outline-info btn-sm ms-2"
                  @click="runMigration(RunType.List)"
                  type="button"
                >
                  📋 List
                </button>
                <button
                  class="btn btn-outline-warning btn-sm ms-2"
                  @click="runMigration(RunType.Preview)"
                  type="button"
                >
                  👁️ Preview
                </button>
                <button
                  class="btn btn-primary btn-sm ms-2"
                  @click="runMigration(RunType.Run)"
                  type="button"
                >
                  ▶️ Run
                </button>
              </BForm>
            </div>
          </div>
          <div ref="editorContainer" class="editor-container"></div>
          <div class="examples mt-3 mb-3">
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

      <div class="col-12 col-lg-6">
        <BTabs>
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
    </BOverlay>
  </div>
</template>

<script setup lang="ts">
import {ref, onMounted, onUnmounted, useTemplateRef, computed, watch} from "vue";
import monaco from "./monaco-config";
import DatabaseViewer from "./components/DatabaseViewer.vue";
import ThemeToggle from "./components/ThemeToggle.vue";
import {BBadge, BTab, BTabs, BFormCheckbox, BForm, BOverlay} from "bootstrap-vue-next";
import samples from "./samples/index.js";
import { Schema } from "./types";
import { useTheme } from "./composables/useTheme";

const { effectiveTheme, initTheme, cleanupTheme } = useTheme();

const editorContainer = ref(null);
const output = ref(
  "Ready to run migrations. Click 'Run Migration' to execute your code.",
);
const blazorReady = ref(false);
const executing = ref(false);
const dbName = ref("sample");
const dbSchema = ref<Schema>(null);
const alwaysReset = ref(false);
const dbViewer = useTemplateRef("dbViewer");
const preloaded = ref(false);
const loading = computed(() => !blazorReady.value || executing.value);
let editor = null;

enum RunType {
  Run = 0,
  List = 1,
  Preview = 2,
}

function initEditor() {
  if (!editorContainer.value) {
    return;
  }

  // Get code from URL hash if available
  const hash = window.location.hash;
  let decodedCode: string = null;
  if (hash.startsWith("#code=")) {
    try {
      const encodedCode = hash.substring(6);
      decodedCode = atob(decodeURIComponent(encodedCode));
    } catch (e) {
      console.error("Failed to decode code from URL:", e);
    }
  }

  const monacoTheme = effectiveTheme.value === "dark" ? "vs-dark" : "vs-light";
  editor = monaco.editor.create(editorContainer.value, {
    value: decodedCode ?? samples[0].code,
    language: "csharp",
    theme: monacoTheme,
    automaticLayout: true,
    minimap: { enabled: false },
    fontSize: 12,
    scrollBeyondLastLine: false,
  });
}

// Watch for theme changes and update Monaco editor theme
watch(effectiveTheme, (newTheme) => {
  if (editor) {
    monaco.editor.setTheme(newTheme === "dark" ? "vs-dark" : "vs-light");
  }
});

async function copyUrl() {
  const code = editor.getValue();
  const encodedCode = encodeURIComponent(btoa(code));
  const url = `${window.location.origin}${window.location.pathname}#code=${encodedCode}`;

  await navigator.clipboard.writeText(url);

  output.value = "URL copied to clipboard!";
}

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

async function preload() {
  if (preloaded.value) return;
  
  blazorReady.value = true;
  executing.value = true;
  
  try {
    output.value = await window.migrationInterop.invokeMethodAsync<string>("PreloadAsync");
  } catch (error) {
    output.value = `Error during preload: ${error.message}`;
  } finally {
    executing.value = false;
    preloaded.value = true;
  }
}

onMounted(async () => {
  initTheme();
  initEditor();

  // Wait for Blazor WASM to be ready
  window.addEventListener("blazor-ready", preload);

  // Check if already ready
  if (window.migrationInterop) {
    await preload()
  }
});

onUnmounted(() => {
  cleanupTheme();
});
</script>
