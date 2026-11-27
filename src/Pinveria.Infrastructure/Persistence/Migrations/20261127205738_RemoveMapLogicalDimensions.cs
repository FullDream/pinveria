using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pinveria.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127205738_RemoveMapLogicalDimensions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            UPDATE "Markers" AS mk
            SET "PositionX" = mk."PositionX" * (CAST(200.0 AS double precision) / CAST(GREATEST(m."Width", m."Height") AS double precision)),
                "PositionY" = mk."PositionY" * (CAST(200.0 AS double precision) / CAST(GREATEST(m."Width", m."Height") AS double precision))
            FROM "Categories" AS c
            INNER JOIN "CategoryGroups" AS cg ON cg."Id" = c."CategoryGroupId"
            INNER JOIN "Maps" AS m ON m."Id" = cg."MapId"
            WHERE mk."CategoryId" = c."Id"
              AND m."Width" IS NOT NULL
              AND m."Height" IS NOT NULL
              AND GREATEST(m."Width", m."Height") > 0;
            """);

        migrationBuilder.DropColumn(
            name: "Height",
            table: "Maps");

        migrationBuilder.DropColumn(
            name: "Width",
            table: "Maps");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "Height",
            table: "Maps",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "Width",
            table: "Maps",
            type: "integer",
            nullable: true);
    }
}
