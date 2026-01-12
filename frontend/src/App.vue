<template>
  <header class="app-header" v-if="fullPage">
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

  <div class="container-fluid mt-1" :class="{embedded: !fullPage}">
    <div class="row">
      <div class="col-12 col-lg-6 g-0 g-lg-1 px-1">
        <div class="editor-section">
          <div class="section-header mb-2">
            <button class="btn btn-sm btn-outline-light" @click="copyUrl" v-if="fullPage">
              📋 Copy URL
            </button>
            <button class="btn btn-sm btn-outline-light" @click="openInNewWindow" v-else>
              ↗ Open in new window
            </button>
            <div>
              <BForm class="d-flex flex-row align-items-center flex-wrap">
                <BFormCheckbox
                  id="alwaysReset"
                  v-model="alwaysReset"
                  switch
                  class="ms-2"
                  :disabled="!preloaded"
                >
                  <span class="d-xl-inline">Always reset DB</span>
                </BFormCheckbox>
  
                <button
                  class="btn btn-outline-danger btn-sm ms-2"
                  @click="resetDatabase"
                  type="button"
                  :disabled="!preloaded"
                >
                  💣 Reset DB
                </button>
                <button
                  class="btn btn-outline-info btn-sm ms-2"
                  @click="runMigration(RunType.List)"
                  type="button"
                  :disabled="!preloaded"
                >
                  📋 List
                </button>
                <button
                  class="btn btn-outline-warning btn-sm ms-2"
                  @click="runMigration(RunType.Preview)"
                  type="button"
                  :disabled="!preloaded"
                >
                  👁️ Preview
                </button>
                <button
                  class="btn btn-primary btn-sm ms-2"
                  @click="runMigration(RunType.Run)"
                  type="button"
                  :disabled="!preloaded"
                >
                  ▶️ Run
                </button>
              </BForm>
            </div>
          </div>
          
          <Suspense>
            <template #default>
              <MonacoEditor ref="monacoEditor" :initial-value="initialCode" />
            </template>
            <template #fallback>
              <div class="editor-container d-flex align-items-center justify-content-center">
                <span>Loading editor...</span>
              </div>
            </template>
          </Suspense>
          
          <div class="examples mt-3 mb-3" v-if="fullPage">
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
  
      <div class="col-12 col-lg-6 mt-1 mt-lg-0 g-0 g-lg-1 px-1">
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
import { ref, onMounted, onUnmounted, useTemplateRef, computed, defineAsyncComponent } from "vue";
import DatabaseViewer from "./components/DatabaseViewer.vue";
import ThemeToggle from "./components/ThemeToggle.vue";
import { BBadge, BTab, BTabs, BFormCheckbox, BForm } from "bootstrap-vue-next";
import samples from "./samples/index.js";
import { Schema } from "./types";
import { useTheme } from "./composables/useTheme";

const MonacoEditor = defineAsyncComponent(() => import("./components/MonacoEditor.vue"));

const { initTheme, cleanupTheme } = useTheme();

const monacoEditor = useTemplateRef<{ getValue: () => string; setValue: (value: string) => void }>("monacoEditor");
const output = ref("Ready to run migrations. Click 'Run Migration' to execute your code.");
const executing = ref(false);
const dbName = ref("sample");
const dbSchema = ref<Schema>(null);
const alwaysReset = ref(false);
const dbViewer = useTemplateRef("dbViewer");
const preloaded = ref(false);

// Fullpage when "?embed" is not present in URL
const fullPage = computed(() => !window.location.search.includes("embed"));

// Get code from URL hash if available
const initialCode = computed(() => {
  const hash = window.location.hash;
  if (hash.startsWith("#code=")) {
    try {
      const encodedCode = hash.substring(6);
      return atob(decodeURIComponent(encodedCode));
    } catch (e) {
      console.error("Failed to decode code from URL:", e);
    }
  }
  return samples[0].code;
});

enum RunType {
  Run = 0,
  List = 1,
  Preview = 2,
}

async function copyUrl() {
  const code = monacoEditor.value?.getValue() ?? "";
  const encodedCode = encodeURIComponent(btoa(code));
  const url = `${window.location.origin}${window.location.pathname}#code=${encodedCode}`;

  await navigator.clipboard.writeText(url);

  output.value = "URL copied to clipboard!";
}

async function openInNewWindow() {
  const code = monacoEditor.value?.getValue() ?? "";
  const encodedCode = encodeURIComponent(btoa(code));
  const url = `${window.location.origin}${window.location.pathname}#code=${encodedCode}`;

  window.open(url, "_blank");
}

async function runMigration(runType: RunType) {
  if (!window.migrationInterop || executing.value) return;

  if (alwaysReset.value || !dbName.value) {
    resetDatabase();
  }

  executing.value = true;
  output.value = "Executing migration...";

  try {
    const code = monacoEditor.value?.getValue() ?? "";
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
  if (monacoEditor.value && code) {
    monacoEditor.value.setValue(code);
  }
}

async function preload() {
  if (preloaded.value) return;
  
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

  // Wait for Blazor WASM to be ready
  window.addEventListener("blazor-ready", preload);

  // Check if already ready
  if (window.migrationInterop) {
    await preload();
  }
});

onUnmounted(() => {
  cleanupTheme();
});
</script>
