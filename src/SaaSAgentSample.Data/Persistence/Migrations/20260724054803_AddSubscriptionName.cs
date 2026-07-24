using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSAgentSample.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "Subscriptions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                table: "Subscriptions");
        }
    }
}
