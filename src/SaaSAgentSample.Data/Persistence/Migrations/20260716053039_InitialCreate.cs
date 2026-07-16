using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaaSAgentSample.Data.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MarketplaceSubscriptionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OfferId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PlanId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_MarketplaceSubscriptionId",
                table: "Subscriptions",
                column: "MarketplaceSubscriptionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Subscriptions");
        }
    }
}
