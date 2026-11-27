using Pinveria.Domain.ValueObjects;
using Pinveria.Domain.Maps;
using Pinveria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Pinveria.IntegrationTests;

public class MapPersistenceTests
{
	[Fact]
	public async Task FreshSchemaPersistsMapsAndRollbackPreservesAllParentAndMarkerData()
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		var game = await UpdateGameHandlerTests.SeedAsync(factory, "legacy-id");

		await MediaTestData.EnsureMarkerAssetsAsync(factory);
		var group = await CategoryGroupTestData.SeedAsync(factory, game.Id, "Group", 1);
		var category = await CategoryTestData.SeedAsync(factory, group.Id);
		using var client = await factory.CreateHttpsClientAsync();

		var marker = await MarkerTestData.CreateAsync(client, game.Id, category.Id,
			media: System.Text.Json.JsonDocument.Parse(MarkerTestData.MixedMedia).RootElement.Clone());

		var markerId = marker.GetProperty("id").GetGuid();
		var map = await MapTestData.SeedAsync(factory, game.Id);

		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

			var stored = await context.Maps.Include(map => map.Styles)
				.SingleAsync();

			Assert.Equal(map.Id, stored.Id);
			Assert.Equal(map.Styles.Single().Id, stored.Styles.Single().Id);
			Assert.Equal(MapProcessingStatus.Queued, stored.Styles.Single().Status);
			Assert.False(context.Database.HasPendingModelChanges());
			var duplicate = Map.Create(game.Id, map.Name, Slug.Create("map-" + Guid.NewGuid().ToString("N")).Value!).Value!;

			context.Maps.Add(duplicate);
			var conflict = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

			Assert.True(new PostgresPersistenceFailureClassifier().IsMapNameConflict(conflict));
		}

		await using (var scope = factory.Services.CreateAsyncScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

			DeleteGameHandlerTests.AssertSameGame(game, await context.Games.SingleAsync());
			Assert.Equal(group.Id, (await context.CategoryGroups.SingleAsync()).Id);
			Assert.Equal(category.Id, (await context.Categories.SingleAsync()).Id);
			var storedMarker = await context.Markers.Include(marker => marker.Media).SingleAsync();

			Assert.Equal(markerId, storedMarker.Id);
			Assert.Equal(3, storedMarker.Media.Count);
			Assert.Equal(map.Id, (await context.Maps.SingleAsync()).Id);
		}
	}

	[Fact]
	public async Task RemoveMapLogicalDimensionsMigrationRescalesMarkersAndDropsDimensions()
	{
		await using var factory = new GameApiFactory(migrateBeforeStartup: false);

		await using (var connection = new NpgsqlConnection(factory.ConnectionString))
		{
			await connection.OpenAsync();
			await using var command = connection.CreateCommand();
			command.CommandText = $"CREATE SCHEMA IF NOT EXISTS {factory.Schema}";
			await command.ExecuteNonQueryAsync();
		}

		var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(factory.ConnectionString).Options;
		await using var context = new ApplicationDbContext(options);
		var migrator = context.Database.GetService<IMigrator>();

		await migrator.MigrateAsync("20261125175433_Initial");

		var now = DateTimeOffset.UtcNow;
		var coverId = Guid.CreateVersion7();
		var gameId = Guid.CreateVersion7();
		var squareMapId = Guid.CreateVersion7();
		var rectMapId = Guid.CreateVersion7();
		var blankMapId = Guid.CreateVersion7();
		var squareGroupId = Guid.CreateVersion7();
		var rectGroupId = Guid.CreateVersion7();
		var blankGroupId = Guid.CreateVersion7();
		var squareCategoryId = Guid.CreateVersion7();
		var rectCategoryId = Guid.CreateVersion7();
		var blankCategoryId = Guid.CreateVersion7();
		var squareMarkerId = Guid.CreateVersion7();
		var rectMarkerId = Guid.CreateVersion7();
		var blankMarkerId = Guid.CreateVersion7();

		await context.Database.ExecuteSqlAsync($"""
			INSERT INTO "MediaAssets" ("Id", "Name", "ContentType", "ByteLength", "CreatedAt", "UpdatedAt", "Type", "Width", "Height", "Alt")
			VALUES ({coverId}, 'Cover', 'image/png', 100, {now}, {now}, 'Image', 64, 64, '');

			INSERT INTO "Games" ("Id", "Name", "Slug", "CoverId", "Status", "CreatedAt", "UpdatedAt")
			VALUES ({gameId}, 'Test Game', 'test-game', {coverId}, 'Published', {now}, {now});

			INSERT INTO "Maps" ("Id", "GameId", "Name", "Slug", "NormalizedName", "Width", "Height", "LastCreationSequence", "CreatedAt", "UpdatedAt", "Status")
			VALUES
				({squareMapId}, {gameId}, 'Square', 'square', 'SQUARE', 4096, 4096, 1, {now}, {now}, 'Published'),
				({rectMapId}, {gameId}, 'Rect', 'rect', 'RECT', 4000, 2000, 1, {now}, {now}, 'Published'),
				({blankMapId}, {gameId}, 'Blank', 'blank', 'BLANK', NULL, NULL, 0, {now}, {now}, 'Draft');

			INSERT INTO "CategoryGroups" ("Id", "MapId", "Name", "NormalizedName", "Color", "Status", "Order", "CreatedAt", "UpdatedAt")
			VALUES
				({squareGroupId}, {squareMapId}, 'G1', 'G1', '#F59E0B', 'Published', 0, {now}, {now}),
				({rectGroupId}, {rectMapId}, 'G2', 'G2', '#F59E0B', 'Published', 0, {now}, {now}),
				({blankGroupId}, {blankMapId}, 'G3', 'G3', '#F59E0B', 'Draft', 0, {now}, {now});

			INSERT INTO "Categories" ("Id", "CategoryGroupId", "Name", "NormalizedName", "Status", "Order", "CreatedAt", "UpdatedAt")
			VALUES
				({squareCategoryId}, {squareGroupId}, 'C1', 'C1', 'Published', 0, {now}, {now}),
				({rectCategoryId}, {rectGroupId}, 'C2', 'C2', 'Published', 0, {now}, {now}),
				({blankCategoryId}, {blankGroupId}, 'C3', 'C3', 'Draft', 0, {now}, {now});

			INSERT INTO "Markers" ("Id", "CategoryId", "Name", "Status", "CreatedAt", "UpdatedAt", "PositionX", "PositionY")
			VALUES
				({squareMarkerId}, {squareCategoryId}, 'M1', 'Published', {now}, {now}, 1024.0, -512.0),
				({rectMarkerId}, {rectCategoryId}, 'M2', 'Published', {now}, {now}, 1000.0, 500.0),
				({blankMarkerId}, {blankCategoryId}, 'M3', 'Draft', {now}, {now}, 12.5, -7.25);
			""");

		await migrator.MigrateAsync();

		var markers = await context.Markers.AsNoTracking().ToDictionaryAsync(item => item.Id);

		Assert.Equal(50d, markers[squareMarkerId].Position.X, 10);
		Assert.Equal(-25d, markers[squareMarkerId].Position.Y, 10);
		Assert.Equal(50d, markers[rectMarkerId].Position.X, 10);
		Assert.Equal(25d, markers[rectMarkerId].Position.Y, 10);
		Assert.Equal(12.5d, markers[blankMarkerId].Position.X, 10);
		Assert.Equal(-7.25d, markers[blankMarkerId].Position.Y, 10);

		var remainingDimensionColumns = await context.Database
			.SqlQuery<string>($"""
				SELECT column_name AS "Value"
				FROM information_schema.columns
				WHERE table_schema = {factory.Schema}
				  AND table_name = 'Maps'
				  AND column_name IN ('Width', 'Height')
				""")
			.ToListAsync();

		Assert.Empty(remainingDimensionColumns);
	}
}
