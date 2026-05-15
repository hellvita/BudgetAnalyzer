using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BudgetAnalyzer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRevokedTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "revoked_tokens",
                columns: table => new
                {
                    jti = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revoked_tokens", x => x.jti);
                });

            migrationBuilder.CreateIndex(
                name: "ix_revoked_tokens_expires_at",
                table: "revoked_tokens",
                column: "expires_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "revoked_tokens");
        }
    }
}
