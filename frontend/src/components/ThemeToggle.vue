<template>
  <BDropdown variant="link" no-caret toggle-class="theme-toggle-btn">
    <template #button-content>
      <span class="theme-icon">{{ currentIcon }}</span>
    </template>
    <BDropdownItem
      v-for="option in themeOptions"
      :key="option.value"
      :active="preference === option.value"
      @click="setPreference(option.value)"
    >
      {{ option.icon }} {{ option.label }}
    </BDropdownItem>
  </BDropdown>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { BDropdown, BDropdownItem } from "bootstrap-vue-next";
import { useTheme, type ThemePreference } from "../composables/useTheme";

const { preference, effectiveTheme, setPreference } = useTheme();

const themeOptions: { value: ThemePreference; label: string; icon: string }[] =
  [
    { value: "auto", label: "Auto", icon: "💻" },
    { value: "light", label: "Light", icon: "☀️" },
    { value: "dark", label: "Dark", icon: "🌙" },
  ];

const currentIcon = computed(() => {
  if (preference.value === "auto") {
    return effectiveTheme.value === "dark" ? "🌙" : "☀️";
  }
  return preference.value === "dark" ? "🌙" : "☀️";
});
</script>

<style scoped lang="scss">
:deep(.theme-toggle-btn) {
  padding: 0.25rem 0.5rem;
  color: white !important;
  text-decoration: none;
  border: 1px solid rgba(255, 255, 255, 0.3);
  border-radius: 0.375rem;

  &:hover {
    background-color: rgba(255, 255, 255, 0.1);
  }
}

.theme-icon {
  font-size: 1.1rem;
}
</style>
