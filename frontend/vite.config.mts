import {defineConfig} from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  build: {
    outDir: '../src/FluentMigratorRepl/wwwroot',
    emptyOutDir: false, // Don't delete _framework folder
    rollupOptions: {
      output: {
        // Keep assets organized
        assetFileNames: 'assets/[name]-[hash][extname]',
        chunkFileNames: 'assets/[name]-[hash].js',
        entryFileNames: 'assets/[name]-[hash].js',
      }
    }
  },
  server: {
    port: 5173,
    proxy: {
      // Proxy _framework requests to the Blazor dev server
      '/_framework': {
        target: 'http://localhost:5122',
        changeOrigin: true
      }
    }
  }
})
