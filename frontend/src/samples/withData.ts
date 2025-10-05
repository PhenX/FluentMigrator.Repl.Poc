const migrationCode = `using FluentMigrator;

[Migration(202501010004)]
public class CreateProductsTable : Migration
{
    public override void Up()
    {
        Create.Table("Products")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(100).NotNullable()
            .WithColumn("Description").AsString(500).Nullable()
            .WithColumn("Price").AsDecimal().NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
                .WithDefault(SystemMethods.CurrentDateTime);
                
        // Insert initial data
        Insert.IntoTable("Products").Row(new { Name = "Sample Product 1", Description = "This is a sample product.", Price = 19.99m });
        Insert.IntoTable("Products").Row(new { Name = "Sample Product 2", Description = "This is another sample product.", Price = 29.99m });
    }
    
    public override void Down()
    {
        Delete.Table("Products");
    }
}
`;

export default {
  title: "With data",
  code: migrationCode,
};
