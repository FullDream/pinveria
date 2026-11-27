using Pinveria.Domain;
using Pinveria.Domain.ValueObjects;
using Pinveria.Domain.Maps;

namespace Pinveria.Domain.Tests;

public class MapRenameTests
{
	[Fact]
	public void RenameTrimsAndPreservesCreation()
	{
		var map = Map.Create(Guid.CreateVersion7(), "World", Slug.Create("map-" + Guid.NewGuid().ToString("N")).Value!).Value!;
		var createdAt = map.CreatedAt;
		var result = map.Rename(" New World ");

		Assert.True(result.IsSuccess);
		Assert.True(result.Value);
		Assert.Equal("New World", map.Name);
		Assert.Equal("NEW WORLD", map.NormalizedName);
		Assert.Equal(createdAt, map.CreatedAt);
		Assert.True(map.UpdatedAt >= createdAt);
	}

	[Fact]
	public void NoOpPreservesTimestampWhileCaseChangeUpdatesSpelling()
	{
		var map = Map.Create(Guid.CreateVersion7(), "World", Slug.Create("map-" + Guid.NewGuid().ToString("N")).Value!).Value!;
		var updatedAt = map.UpdatedAt;

		Assert.False(map.Rename(" World ").Value);
		Assert.Equal(updatedAt, map.UpdatedAt);
		Assert.True(map.Rename("world").Value);
		Assert.Equal("world", map.Name);
		Assert.Equal("WORLD", map.NormalizedName);
	}

	[Fact]
	public void CreateAndUpdateNormalizeOptionalDescription()
	{
		var slug = Slug.Create("map-" + Guid.NewGuid().ToString("N")).Value!;
		var empty = Map.Create(Guid.CreateVersion7(), "World", slug, description: "").Value!;

		Assert.Null(empty.Description);
		var map = Map.Create(Guid.CreateVersion7(), "World", slug, description: "Main overworld").Value!;

		Assert.Equal("Main overworld", map.Description);
		var updatedAt = map.UpdatedAt;

		Assert.False(map.Update(description: "Main overworld").Value);
		Assert.Equal(updatedAt, map.UpdatedAt);

		Assert.True(map.Update(description: "").Value);
		Assert.Null(map.Description);
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("  ")]
	public void InvalidRenamePreservesMap(string? name)
	{
		var map = Map.Create(Guid.CreateVersion7(), "World", Slug.Create("map-" + Guid.NewGuid().ToString("N")).Value!).Value!;
		var updatedAt = map.UpdatedAt;

		Assert.True(map.Rename(name).IsFailure);
		Assert.True(map.Rename(new string('n', 201)).IsFailure);
		Assert.Equal("World", map.Name);
		Assert.Equal(updatedAt, map.UpdatedAt);
		Assert.True(map.Rename(" " + new string('n', 200) + " ").IsSuccess);
	}
}