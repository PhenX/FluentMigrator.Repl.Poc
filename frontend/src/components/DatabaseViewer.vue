<template>
  <div class="database-viewer">
    <h4 class="mb-3">📊 Database Schema</h4>
    
    <div v-if="!schema || schema.tables.length === 0" class="text-muted">
      No tables created yet. Run a migration to see the database schema.
    </div>
    
    <div v-else>
      <!-- Tables Section -->
      <div v-for="table in schema.tables" :key="table.name" class="mb-4">
        <div class="schema-item-header" @click="toggleTable(table.name)">
          <h5>
            <span class="toggle-icon">{{ expandedTables.includes(table.name) ? '▼' : '▶' }}</span>
            📋 Table: {{ table.name }}
            <span class="badge bg-secondary ms-2">{{ table.columns.length }} columns</span>
          </h5>
        </div>
        
        <div v-show="expandedTables.includes(table.name)" class="schema-content">
          <!-- Columns -->
          <div class="table-structure mb-3">
            <h6>Columns:</h6>
            <table class="table table-sm table-bordered">
              <thead>
                <tr>
                  <th>Name</th>
                  <th>Type</th>
                  <th>Constraints</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="column in table.columns" :key="column.name">
                  <td><code>{{ column.name }}</code></td>
                  <td>{{ column.type }}</td>
                  <td>
                    <span v-for="constraint in column.constraints" :key="constraint" class="badge bg-info me-1">
                      {{ constraint }}
                    </span>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
          
          <!-- Indexes for this table -->
          <div v-if="getTableIndexes(table.name).length > 0" class="table-indexes mb-3">
            <h6>Indexes:</h6>
            <ul class="list-unstyled">
              <li v-for="index in getTableIndexes(table.name)" :key="index.name">
                <code>{{ index.name }}</code> on <code>{{ index.columns.join(', ') }}</code>
                <span v-if="index.unique" class="badge bg-warning text-dark ms-1">UNIQUE</span>
              </li>
            </ul>
          </div>
          
          <!-- Table Data -->
          <div class="table-data">
            <h6>
              Data:
              <button class="btn btn-sm btn-outline-primary ms-2" @click="loadTableData(table.name)">
                <span v-if="loadingTable === table.name">Loading...</span>
                <span v-else>Refresh</span>
              </button>
            </h6>
            
            <div v-if="tableData[table.name]">
              <div v-if="tableData[table.name].rows.length === 0" class="text-muted small">
                (No rows)
              </div>
              <div v-else class="table-responsive">
                <table class="table table-sm table-striped table-hover">
                  <thead>
                    <tr>
                      <th v-for="col in tableData[table.name].columns" :key="col">{{ col }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr v-for="(row, idx) in tableData[table.name].rows" :key="idx">
                      <td v-for="col in tableData[table.name].columns" :key="col">
                        {{ formatValue(row[col]) }}
                      </td>
                    </tr>
                  </tbody>
                </table>
                <div class="text-muted small">
                  {{ tableData[table.name].rows.length }} row(s)
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
      
      <!-- Views Section -->
      <div v-if="schema.views && schema.views.length > 0">
        <h5 class="mt-4">👁️ Views</h5>
        <div v-for="view in schema.views" :key="view.name" class="mb-3">
          <div class="schema-item-header" @click="toggleView(view.name)">
            <h6>
              <span class="toggle-icon">{{ expandedViews.includes(view.name) ? '▼' : '▶' }}</span>
              {{ view.name }}
            </h6>
          </div>
          <div v-show="expandedViews.includes(view.name)" class="schema-content">
            <pre class="sql-display">{{ view.sql }}</pre>
            
            <button class="btn btn-sm btn-outline-primary mb-2" @click="loadViewData(view.name)">
              <span v-if="loadingTable === view.name">Loading...</span>
              <span v-else>Query View</span>
            </button>
            
            <div v-if="viewData[view.name]" class="table-responsive">
              <div v-if="viewData[view.name].rows.length === 0" class="text-muted small">
                (No rows)
              </div>
              <table v-else class="table table-sm table-striped table-hover">
                <thead>
                  <tr>
                    <th v-for="col in viewData[view.name].columns" :key="col">{{ col }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="(row, idx) in viewData[view.name].rows" :key="idx">
                    <td v-for="col in viewData[view.name].columns" :key="col">
                      {{ formatValue(row[col]) }}
                    </td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, defineExpose, defineProps } from 'vue'


const props = defineProps({
  schema: {
    type: Object,
    required: false,
    default: null
  }
})

const expandedTables = ref([])
const expandedViews = ref([])
const tableData = ref({})
const viewData = ref({})
const loadingTable = ref(null)

const toggleTable = (tableName) => {
  const idx = expandedTables.value.indexOf(tableName)
  if (idx === -1) {
    expandedTables.value.push(tableName)
    // Auto-load data when expanding
    loadTableData(tableName)
  } else {
    expandedTables.value.splice(idx, 1)
  }
}

const toggleView = (viewName) => {
  const idx = expandedViews.value.indexOf(viewName)
  if (idx === -1) {
    expandedViews.value.push(viewName)
  } else {
    expandedViews.value.splice(idx, 1)
  }
}

const getTableIndexes = (tableName) => {
  if (!props.schema || !props.schema.indexes) return []
  return props.schema.indexes.filter(idx => idx.tableName === tableName)
}

const loadTableData = async (tableName) => {
  if (!window.migrationInterop) return
  
  loadingTable.value = tableName
  try {
    const result = await window.migrationInterop.invokeMethodAsync('GetTableDataAsync', tableName)
    tableData.value[tableName] = JSON.parse(result)
  } catch (error) {
    console.error('Error loading table data:', error)
    tableData.value[tableName] = { columns: [], rows: [] }
  } finally {
    loadingTable.value = null
  }
}

const loadViewData = async (viewName) => {
  if (!window.migrationInterop) return
  
  loadingTable.value = viewName
  try {
    const result = await window.migrationInterop.invokeMethodAsync('GetTableDataAsync', viewName)
    viewData.value[viewName] = JSON.parse(result)
  } catch (error) {
    console.error('Error loading view data:', error)
    viewData.value[viewName] = { columns: [], rows: [] }
  } finally {
    loadingTable.value = null
  }
}

const formatValue = (value) => {
  if (value === null || value === undefined) return '(null)'
  if (typeof value === 'string' && value.length > 100) return value.substring(0, 100) + '...'
  return value
}

const refreshAllData = () => {
  // Refresh all expanded tables
  expandedTables.value.forEach(tableName => {
    loadTableData(tableName)
  })
  // Refresh all expanded views
  expandedViews.value.forEach(viewName => {
    loadViewData(viewName)
  })
}

// Expose refresh method to parent
defineExpose({ refreshAllData })
</script>

<style scoped>
.database-viewer {
  margin-top: 2rem;
}

.schema-item-header {
  cursor: pointer;
  padding: 0.75rem;
  background-color: #f8f9fa;
  border-left: 4px solid #007bff;
  margin-bottom: 0.5rem;
  border-radius: 4px;
  transition: background-color 0.2s;
}

.schema-item-header:hover {
  background-color: #e9ecef;
}

.schema-item-header h5,
.schema-item-header h6 {
  margin: 0;
  font-size: 1rem;
}

.toggle-icon {
  display: inline-block;
  width: 20px;
  font-size: 0.8rem;
  color: #6c757d;
}

.schema-content {
  padding: 1rem;
  background-color: #ffffff;
  border: 1px solid #dee2e6;
  border-radius: 4px;
  margin-bottom: 1rem;
}

.table-structure h6,
.table-indexes h6,
.table-data h6 {
  font-size: 0.9rem;
  font-weight: 600;
  color: #495057;
  margin-bottom: 0.5rem;
}

.table {
  font-size: 0.85rem;
}

code {
  background-color: #f8f9fa;
  padding: 2px 6px;
  border-radius: 3px;
  font-size: 0.9em;
}

.sql-display {
  background-color: #f8f9fa;
  padding: 0.75rem;
  border-radius: 4px;
  font-size: 0.85rem;
  margin-bottom: 0.5rem;
}

.table-responsive {
  max-height: 400px;
  overflow-y: auto;
}

.badge {
  font-size: 0.75rem;
}
</style>
