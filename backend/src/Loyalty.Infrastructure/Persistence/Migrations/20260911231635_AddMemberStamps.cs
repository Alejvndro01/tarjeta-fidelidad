using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMemberStamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "member_stamps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_member_stamps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_member_stamps_TenantId_MemberId_ProductSku",
                table: "member_stamps",
                columns: new[] { "TenantId", "MemberId", "ProductSku" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "member_stamps");
        }
    }
}
