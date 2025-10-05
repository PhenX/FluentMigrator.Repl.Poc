const migrationCode = `using FluentMigrator;

// This is a sample migration that creates a simple Users table.
// You can create one or migrations here and click "Run Migrations" button to apply them to the database.

[Migration(202501010001)]
public class CreateUsersTable : Migration
{
    public override void Up()
    {
        Create.Table("Users")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Username").AsString(50).NotNullable()
            .WithColumn("Email").AsString(100).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }
    
    public override void Down()
    {
        Delete.Table("Users");
    }
}`;

export default {
  title: "Simple Table Creation",
  code: migrationCode,
};
