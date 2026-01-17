using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SwimStats.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwimmersFromConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "Swimmers",
                keyColumn: "Id",
                keyValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Swimmers",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Ingemar Voskamp" },
                    { 2, "Cindy Franken-Hendriks" },
                    { 3, "Tom Brouwers" },
                    { 4, "Annemarie Jakobs" },
                    { 5, "Esther Sprick" },
                    { 6, "Danee Sandifort" },
                    { 7, "Inge Ruisch" },
                    { 8, "Yvette den Daas" },
                    { 9, "Yvette Voskamp-Adriaensen" },
                    { 10, "Luuk Leenders" },
                    { 11, "Mila Otten" },
                    { 12, "Jesse Saris" },
                    { 13, "Anouk Neijenhuis" },
                    { 14, "Tessa Vermeulen" },
                    { 15, "Joël Swart" },
                    { 16, "Laura Piras" },
                    { 17, "Maartje Vriesen" },
                    { 18, "Phileine Ossendrijver" },
                    { 19, "Myrna Roth" },
                    { 20, "Zara Gazibegovic" },
                    { 21, "Sofie Martens" },
                    { 22, "Boxuan Sheng" },
                    { 23, "Wenxuan Sheng" },
                    { 24, "Ilana Gazibegovic" },
                    { 25, "Florian Van der Meiden" },
                    { 26, "Timo Hogenkamp" },
                    { 27, "Timo Schoonderwaldt" },
                    { 28, "Guido Vos" },
                    { 29, "Lucas Hissink" },
                    { 30, "Zhifeng Sheng" }
                });
        }
    }
}
