import { ref, computed, watch, type WatchStopHandle } from "vue";

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
  // At this point, preference is "dark" | "light"
  return preference.value as "dark" | "light";
});

function setPreference(newPreference: ThemePreference) {
  preference.value = newPreference;
  localStorage.setItem(STORAGE_KEY, newPreference);
}

function applyTheme() {
  document.documentElement.setAttribute("data-bs-theme", effectiveTheme.value);
}

let mediaQueryListener: ((e: MediaQueryListEvent) => void) | null = null;
let watchStopHandle: WatchStopHandle | null = null;

function initTheme() {
  // Listen for system preference changes
  const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)");
  mediaQueryListener = (e: MediaQueryListEvent) => {
    systemDark.value = e.matches;
  };
  mediaQuery.addEventListener("change", mediaQueryListener);

  // Apply theme whenever effective theme changes
  watchStopHandle = watch(effectiveTheme, applyTheme, { immediate: true });
}

function cleanupTheme() {
  if (mediaQueryListener) {
    window
      .matchMedia("(prefers-color-scheme: dark)")
      .removeEventListener("change", mediaQueryListener);
    mediaQueryListener = null;
  }
  if (watchStopHandle) {
    watchStopHandle();
    watchStopHandle = null;
  }
}

export function useTheme() {
  return {
    preference,
    effectiveTheme,
    setPreference,
    initTheme,
    cleanupTheme,
  };
}
