﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hotel_Backend_API.Migrations
{
    /// <inheritdoc />
    public partial class addGuestimgprofile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "imgUser",
                table: "Guests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "imgUser",
                table: "Guests");
        }
    }
}