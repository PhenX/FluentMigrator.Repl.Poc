<template>
  <div v-if="migrations && migrations.length > 0" class="migration-list mt-4">
    <div class="section-header mb-3">
      <h4>📋 Migrations Found in Code</h4>
      <span class="badge bg-info">{{ migrations.length }} migration(s)</span>
    </div>

    <div class="list-group">
      <div 
        v-for="migration in migrations" 
        :key="migration.version"
        class="list-group-item"
      >
        <div class="d-flex justify-content-between align-items-start">
          <div class="migration-info flex-grow-1">
            <h6 class="mb-1">
              <span class="badge bg-primary me-2">v{{ migration.version }}</span>
              {{ migration.name }}
            </h6>
            <p v-if="migration.description" class="mb-1 text-muted small">
              {{ migration.description }}
            </p>
            <div class="migration-methods">
              <span v-if="migration.hasUp" class="badge bg-success me-1">
                <i class="bi bi-arrow-up"></i> Up()
              </span>
              <span v-if="migration.hasDown" class="badge bg-warning">
                <i class="bi bi-arrow-down"></i> Down()
              </span>
            </div>
          </div>
          <div class="migration-actions">
            <button 
              class="btn btn-sm btn-outline-primary" 
              @click="previewMigration(migration.version)"
              :disabled="previewing === migration.version"
            >
              👁️ Preview
            </button>
          </div>
        </div>
        
        <!-- Preview Panel -->
        <div v-if="preview && preview.version === migration.version" class="migration-preview mt-3">
          <div class="preview-header">
            <strong>Preview</strong>
            <button class="btn btn-sm btn-link" @click="clearPreview">✖</button>
          </div>
          <pre class="preview-content">{{ preview.preview }}</pre>
        </div>
      </div>
    </div>
  </div>

  <div v-else-if="error" class="alert alert-warning mt-3">
    <strong>⚠️ Could not list migrations:</strong> {{ error }}
  </div>

  <div v-else-if="migrations && migrations.length === 0" class="alert alert-info mt-3">
    <strong>ℹ️ No migrations found</strong><br>
    Add a migration class with the <code>[Migration(version)]</code> attribute to your code.
  </div>
</template>

<script>
import { ref } from 'vue'

export default {
  name: 'MigrationList',
  props: {
    migrations: Array,
    error: String,
    editorInstance: Object
  },
  setup(props) {
    const preview = ref(null)
    const previewing = ref(null)

    const previewMigration = async (version) => {
      if (!window.migrationInterop) {
        console.error('Migration interop not available')
        return
      }

      if (!props.editorInstance) {
        console.error('Editor instance not available')
        return
      }

      previewing.value = version
      try {
        const code = props.editorInstance.getValue()
        const resultJson = await window.migrationInterop.invokeMethodAsync('PreviewMigrationAsync', code, version)
        const result = JSON.parse(resultJson)
        
        if (result.success) {
          preview.value = {
            version: result.version,
            name: result.name,
            preview: result.preview
          }
        } else {
          alert('Error previewing migration: ' + result.error)
        }
      } catch (error) {
        console.error('Error previewing migration:', error)
        alert('Error previewing migration: ' + error.message)
      } finally {
        previewing.value = null
      }
    }

    const clearPreview = () => {
      preview.value = null
    }

    return {
      preview,
      previewing,
      previewMigration,
      clearPreview
    }
  }
}
</script>

<style scoped>
.migration-list {
  background: #f8f9fa;
  border-radius: 8px;
  padding: 1rem;
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.list-group-item {
  border-left: 4px solid #007bff;
  margin-bottom: 0.5rem;
  border-radius: 4px;
}

.migration-info h6 {
  font-weight: 600;
  color: #333;
}

.migration-methods {
  margin-top: 0.5rem;
}

.migration-actions {
  margin-left: 1rem;
}

.migration-preview {
  background: #fff;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  padding: 0.5rem;
}

.preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-bottom: 0.5rem;
  border-bottom: 1px solid #dee2e6;
  margin-bottom: 0.5rem;
}

.preview-header strong {
  color: #007bff;
}

.preview-content {
  margin: 0;
  padding: 0.5rem;
  background: #f8f9fa;
  border-radius: 4px;
  font-size: 0.875rem;
  max-height: 300px;
  overflow-y: auto;
}
</style>
