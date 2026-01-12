<template>
  <div ref="editorContainer" class="editor-container"></div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted, watch } from "vue";
import monaco from "../monaco-config";
import { useTheme } from "../composables/useTheme";

const props = defineProps<{
  initialValue?: string;
}>();

const { effectiveTheme } = useTheme();

const editorContainer = ref<HTMLElement | null>(null);
let editor: monaco.editor.IStandaloneCodeEditor | null = null;

function getMonacoTheme(theme: string) {
  return theme === "dark" ? "vs-dark" : "vs-light";
}

function getValue(): string {
  return editor?.getValue() ?? "";
}

function setValue(value: string) {
  editor?.setValue(value);
}

watch(effectiveTheme, (newTheme) => {
  if (editor) {
    monaco.editor.setTheme(getMonacoTheme(newTheme));
  }
});

onMounted(() => {
  if (!editorContainer.value) return;

  editor = monaco.editor.create(editorContainer.value, {
    value: props.initialValue ?? "",
    language: "csharp",
    theme: getMonacoTheme(effectiveTheme.value),
    automaticLayout: true,
    minimap: { enabled: false },
    fontSize: 12,
    scrollBeyondLastLine: false,
  });
});

onUnmounted(() => {
  editor?.dispose();
});

defineExpose({
  getValue,
  setValue,
});
</script>
