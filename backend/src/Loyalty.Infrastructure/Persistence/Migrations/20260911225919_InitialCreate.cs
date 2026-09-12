using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Loyalty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: false),
                    PasswordSalt = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "coupons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CouponCode = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SourcePassId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceProvider = table.Column<int>(type: "integer", nullable: true),
                    RedeemedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedAtStoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    RedeemedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    QrHash = table.Column<string>(type: "text", nullable: false),
                    QrSecret = table.Column<string>(type: "text", nullable: false),
                    PhoneNumber = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    PointsBalance = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MembershipId = table.Column<string>(type: "text", nullable: true),
                    JoinedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reward_transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: true),
                    CouponId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PointsDelta = table.Column<int>(type: "integer", nullable: false),
                    StampsDelta = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reward_transactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    PrimaryColor = table.Column<string>(type: "text", nullable: true),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "wallet_passes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<int>(type: "integer", nullable: false),
                    PassIdentifier = table.Column<string>(type: "text", nullable: false),
                    SerialNumber = table.Column<string>(type: "text", nullable: true),
                    PassToken = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallet_passes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "loyalty_programs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    PointsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    StampsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PointsPerMonetaryUnit = table.Column<int>(type: "integer", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_programs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loyalty_programs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "redemption_rewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PointsCost = table.Column<int>(type: "integer", nullable: false),
                    StampCost = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_redemption_rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_redemption_rewards_loyalty_programs_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "loyalty_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stamp_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductSku = table.Column<string>(type: "text", nullable: false),
                    ProductName = table.Column<string>(type: "text", nullable: false),
                    StampsRequired = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stamp_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_stamp_rules_loyalty_programs_LoyaltyProgramId",
                        column: x => x.LoyaltyProgramId,
                        principalTable: "loyalty_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_users_Email",
                table: "app_users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_coupons_Status",
                table: "coupons",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_coupons_TenantId_CouponCode",
                table: "coupons",
                columns: new[] { "TenantId", "CouponCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_programs_TenantId",
                table: "loyalty_programs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_members_TenantId_PhoneNumber",
                table: "members",
                columns: new[] { "TenantId", "PhoneNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_members_TenantId_QrHash",
                table: "members",
                columns: new[] { "TenantId", "QrHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_redemption_rewards_LoyaltyProgramId",
                table: "redemption_rewards",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_reward_transactions_TenantId_CreatedUtc",
                table: "reward_transactions",
                columns: new[] { "TenantId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_reward_transactions_TenantId_MemberId_CreatedUtc",
                table: "reward_transactions",
                columns: new[] { "TenantId", "MemberId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_stamp_rules_LoyaltyProgramId",
                table: "stamp_rules",
                column: "LoyaltyProgramId");

            migrationBuilder.CreateIndex(
                name: "IX_stores_TenantId_Name",
                table: "stores",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_wallet_passes_TenantId_MemberId_Provider",
                table: "wallet_passes",
                columns: new[] { "TenantId", "MemberId", "Provider" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_users");

            migrationBuilder.DropTable(
                name: "coupons");

            migrationBuilder.DropTable(
                name: "members");

            migrationBuilder.DropTable(
                name: "redemption_rewards");

            migrationBuilder.DropTable(
                name: "reward_transactions");

            migrationBuilder.DropTable(
                name: "stamp_rules");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropTable(
                name: "wallet_passes");

            migrationBuilder.DropTable(
                name: "loyalty_programs");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
