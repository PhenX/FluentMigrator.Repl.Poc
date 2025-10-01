# Quick Start Guide

Get started with the FluentMigrator REPL in just a few minutes!

## Prerequisites

- .NET 9.0 SDK or later ([Download](https://dotnet.microsoft.com/download))

## Installation & Running

```bash
# Clone the repository
git clone https://github.com/PhenX/FluentMigrator.Repl.Poc.git
cd FluentMigrator.Repl.Poc

# Navigate to the project
cd src/FluentMigratorRepl

# Run the application
dotnet run
```

The application will start and open at `http://localhost:5122`

## First Steps

### 1. Try the Default Migration

When you open the app, you'll see a default migration already loaded:
- Click the **"▶️ Run Migration"** button
- Watch the output panel show the analysis

### 2. Load an Example

Click one of the example buttons:
- **Simple Table**: Basic table with common types
- **With Foreign Keys**: Tables with relationships
- **With Indexes**: Various index types

### 3. Write Your Own

Modify the code in the editor:

```csharp
using FluentMigrator;

[Migration(202501010001)]
public class CreateProductsTable : Migration
{
    public override void Up()
    {
        Create.Table("Products")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Price").AsDecimal(10, 2).NotNullable();
    }

    public override void Down()
    {
        Delete.Table("Products");
    }
}
```

Click **Run Migration** to see the analysis!

## Understanding the Output

The output shows:
- 📌 **Migration Version**: The unique version number
- 📦 **Migration Class**: Name of your migration class
- 🔨 **UP Migration Actions**: What happens when migrating up
  - Tables created
  - Columns with types and constraints
  - Indexes added
- 🔄 **DOWN Migration Actions**: What happens when rolling back

## Common FluentMigrator Patterns

### Creating Tables

```csharp
Create.Table("Users")
    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
    .WithColumn("Username").AsString(50).NotNullable()
    .WithColumn("Email").AsString(100).NotNullable().Unique();
```

### Adding Foreign Keys

```csharp
Create.Table("Posts")
    .WithColumn("Id").AsInt32().PrimaryKey().Identity()
    .WithColumn("UserId").AsInt32().NotNullable()
        .ForeignKey("Users", "Id")
    .WithColumn("Title").AsString(200).NotNullable();
```

### Creating Indexes

```csharp
// Simple index
Create.Index("IX_Posts_UserId")
    .OnTable("Posts")
    .OnColumn("UserId");

// Unique index
Create.Index("IX_Users_Email")
    .OnTable("Users")
    .OnColumn("Email")
    .Unique();

// Descending index
Create.Index("IX_Posts_CreatedAt")
    .OnTable("Posts")
    .OnColumn("CreatedAt")
    .Descending();
```

### Adding Default Values

```csharp
.WithColumn("CreatedAt").AsDateTime()
    .NotNullable()
    .WithDefault(SystemMethods.CurrentDateTime)

.WithColumn("IsActive").AsBoolean()
    .WithDefaultValue(true)
```

## Tips & Tricks

### 💡 Code Organization

Use descriptive migration names:
```csharp
// Good
[Migration(202501010001)]
public class CreateUsersTable : Migration

// Also Good
[Migration(202501010002)]
public class AddEmailIndexToUsers : Migration
```

### 💡 Version Numbers

Use timestamps for version numbers:
- Format: `YYYYMMDDHHmm` or just `YYYYMMDDnnnn`
- Example: `202501010001` = January 1, 2025, sequence 0001

### 💡 Always Include Down()

Always provide a way to rollback:
```csharp
public override void Down()
{
    Delete.Table("TableName");
}
```

### 💡 Test Your Migrations

Use the REPL to:
1. Write your migration
2. Analyze the output
3. Verify column types and constraints
4. Check index definitions
5. Ensure proper rollback

## Keyboard Shortcuts

- **Tab**: Insert spaces (in code editor)
- **Ctrl+A**: Select all code
- **Ctrl+C/V**: Copy/paste code

## Troubleshooting

### Application won't start

```bash
# Check .NET version
dotnet --version

# Should be 9.0 or later
```

### Code not analyzing

- Check syntax - ensure using FluentMigrator namespace
- Verify `[Migration(version)]` attribute is present
- Ensure class inherits from `Migration`

### Output not showing

- Click "Clear" button and try again
- Check browser console for errors (F12)

## Next Steps

- Read the full [README.md](README.md) for deployment options
- Check [IMPLEMENTATION.md](IMPLEMENTATION.md) for technical details
- Explore FluentMigrator documentation: https://fluentmigrator.github.io/

## Getting Help

- FluentMigrator Docs: https://fluentmigrator.github.io/
- GitHub Issues: Report bugs or request features
- Examples: Check the built-in example migrations

## Building for Production

```bash
# Publish optimized build
dotnet publish -c Release

# Output will be in:
# bin/Release/net9.0/publish/wwwroot/

# Deploy this folder to any static web host!
```

Happy migrating! 🚀
