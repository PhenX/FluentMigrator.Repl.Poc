import { ref, computed, watch } from "vue";

export type ThemePreference = "auto" | "dark" | "light";

const STORAGE_KEY = "theme-preference";

const preference = ref<ThemePreference>(getStoredPreference());
const systemDark = ref(getSystemDarkPreference());

function getStoredPreference(): ThemePreference {
  const stored = localStorage.getItem(STORAGE_KEY);
  if (stored === "auto" || stored === "dark" || stored === "light") {
    return stored;
  }
  return "auto";
}

function getSystemDarkPreference(): boolean {
  return window.matchMedia("(prefers-color-scheme: dark)").matches;
}

const effectiveTheme = computed<"dark" | "light">(() => {
  if (preference.value === "auto") {
    return systemDark.value ? "dark" : "light";
  }
  return preference.value;
});

function setPreference(newPreference: ThemePreference) {
  preference.value = newPreference;
  localStorage.setItem(STORAGE_KEY, newPreference);
}

function applyTheme() {
  document.documentElement.setAttribute("data-bs-theme", effectiveTheme.value);
}

function initTheme() {
  // Listen for system preference changes
  window
    .matchMedia("(prefers-color-scheme: dark)")
    .addEventListener("change", (e) => {
      systemDark.value = e.matches;
    });

  // Apply theme whenever effective theme changes
  watch(effectiveTheme, applyTheme, { immediate: true });
}

export function useTheme() {
  return {
    preference,
    effectiveTheme,
    setPreference,
    initTheme,
  };
}
