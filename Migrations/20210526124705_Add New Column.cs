using Microsoft.EntityFrameworkCore.Migrations;

namespace JWTAuthentication.WebApi.Migrations
{
    public partial class AddNewColumn : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Deviceinfo",
                table: "RefreshToken",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Deviceinfo",
                table: "RefreshToken");
        }
    }
}
