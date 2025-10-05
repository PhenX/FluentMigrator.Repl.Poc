const migrationCode = `using FluentMigrator;

[Migration(202501010003)]
public class CreateOrdersWithIndexes : Migration
{
    public override void Up()
    {
        Create.Table("Orders")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CustomerId").AsInt32().NotNullable()
            .WithColumn("OrderNumber").AsString(50).NotNullable()
            .WithColumn("OrderDate").AsDateTime().NotNullable()
            .WithColumn("Status").AsString(20).NotNullable();
            
        Create.Index("IX_Orders_CustomerId")
            .OnTable("Orders")
            .OnColumn("CustomerId");
            
        Create.Index("IX_Orders_OrderDate")
            .OnTable("Orders")
            .OnColumn("OrderDate")
            .Descending();
            
        Create.Index("IX_Orders_OrderNumber")
            .OnTable("Orders")
            .OnColumn("OrderNumber")
            .Unique();
    }
    
    public override void Down()
    {
        Delete.Index("IX_Orders_OrderNumber");
        Delete.Index("IX_Orders_OrderDate");
        Delete.Index("IX_Orders_CustomerId");
        Delete.Table("Orders");
    }
}`;

export default {
  title: "With indexes",
  code: migrationCode,
};
