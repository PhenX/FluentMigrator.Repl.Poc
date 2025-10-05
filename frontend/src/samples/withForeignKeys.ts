const migrationCode = `using FluentMigrator;

[Migration(202501010002)]
public class CreatePostsTable : Migration
{
    public override void Up()
    {
        Create.Table("Posts")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
                .ForeignKey("Users", "Id")
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Content").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
    }
    
    public override void Down()
    {
        Delete.Table("Posts");
    }
}`;

export default {
  title: "With foreign keys",
  code: migrationCode,
};
