export type Schema = {
    tables: Array<{
        name: string
        columns: Array<{
            name: string
            type: string
            constraints: string[]
        }>
    }>
    views?: Array<{
        name: string
        sql: string
    }>
    indexes?: Array<{
        name: string
        tableName: string
        columns: string[]
        unique: boolean
    }>
};
