using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Pinveria.Application.Media;
using Pinveria.Application.Media.Optimization;
using Pinveria.Application.Media.Optimization.Caching;
using Pinveria.Application.Media.Optimization.Processing;
using Microsoft.Extensions.DependencyInjection;
using NetVips;

namespace Pinveria.IntegrationTests;

public sealed class MediaImageOptimizationApiTests
{
	[Fact]
	public async Task BareUrlServesUnmodifiedOriginalWithoutVaryHeader()
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var uploaded = await MediaTestData.UploadImageAsync(client, "png", width: 800, height: 600);
		using var request = new HttpRequestMessage(HttpMethod.Get, uploaded.Url);

		request.Headers.Accept.ParseAdd("image/avif,image/webp,*/*");
		using var response = await client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
		Assert.DoesNotContain("Accept", response.Headers.Vary);
		var bytes = await response.Content.ReadAsByteArrayAsync();
		using var image = Image.NewFromBuffer(bytes);

		Assert.Equal(800, image.Width);
		Assert.Equal(600, image.Height);
	}

	[Fact]
	public async Task WidthOnlyResizesInOriginalFormatAndOmitsVaryHeader()
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var uploaded = await MediaTestData.UploadImageAsync(client, "png", width: 1600, height: 900);
		using var response = await client.GetAsync($"{uploaded.Url}?width=640");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
		Assert.DoesNotContain("Accept", response.Headers.Vary);
		Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
		Assert.Contains("immutable", response.Headers.CacheControl?.ToString() ?? string.Empty);
		var bytes = await response.Content.ReadAsByteArrayAsync();
		using var image = Image.NewFromBuffer(bytes);

		Assert.Equal(640, image.Width);
		Assert.Equal(360, image.Height);
	}

	[Fact]
	public async Task FormatAutoNegotiatesAvifWebpAndFallbackWithVaryHeaderWhileExplicitFormatOmitsVary()
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var uploaded = await MediaTestData.UploadImageAsync(client, "png", width: 640, height: 480);

		using (var avifRequest = new HttpRequestMessage(HttpMethod.Get, $"{uploaded.Url}?width=320&format=auto"))
		{
			avifRequest.Headers.Accept.ParseAdd("image/avif,image/webp,image/*,*/*");
			using var avifResponse = await client.SendAsync(avifRequest);

			Assert.Equal(HttpStatusCode.OK, avifResponse.StatusCode);
			Assert.Equal("image/avif", avifResponse.Content.Headers.ContentType?.MediaType);
			Assert.Contains("Accept", avifResponse.Headers.Vary);
			using var avifImage = Image.NewFromBuffer(await avifResponse.Content.ReadAsByteArrayAsync());

			Assert.Equal(320, avifImage.Width);
			Assert.Equal(240, avifImage.Height);
		}

		using (var webpRequest = new HttpRequestMessage(HttpMethod.Get, $"{uploaded.Url}?width=320&format=auto"))
		{
			webpRequest.Headers.Accept.ParseAdd("image/webp,image/png,*/*");
			using var webpResponse = await client.SendAsync(webpRequest);

			Assert.Equal(HttpStatusCode.OK, webpResponse.StatusCode);
			Assert.Equal("image/webp", webpResponse.Content.Headers.ContentType?.MediaType);
			Assert.Contains("Accept", webpResponse.Headers.Vary);
		}

		using (var fallbackRequest = new HttpRequestMessage(HttpMethod.Get, $"{uploaded.Url}?width=320&format=auto"))
		{
			fallbackRequest.Headers.Accept.ParseAdd("image/png,*/*");
			using var fallbackResponse = await client.SendAsync(fallbackRequest);

			Assert.Equal(HttpStatusCode.OK, fallbackResponse.StatusCode);
			Assert.Equal("image/png", fallbackResponse.Content.Headers.ContentType?.MediaType);
			Assert.Contains("Accept", fallbackResponse.Headers.Vary);
		}

		using (var explicitRequest = new HttpRequestMessage(HttpMethod.Get, $"{uploaded.Url}?width=320&format=webp"))
		{
			explicitRequest.Headers.Accept.ParseAdd("image/avif,image/webp,*/*");
			using var explicitResponse = await client.SendAsync(explicitRequest);

			Assert.Equal(HttpStatusCode.OK, explicitResponse.StatusCode);
			Assert.Equal("image/webp", explicitResponse.Content.Headers.ContentType?.MediaType);
			Assert.DoesNotContain("Accept", explicitResponse.Headers.Vary);
		}
	}

	[Theory]
	[InlineData("?width=500")]
	[InlineData("?width=0")]
	[InlineData("?width=-320")]
	[InlineData("?width=abc")]
	[InlineData("?width=320&width=640")]
	[InlineData("?format=gif")]
	[InlineData("?format=")]
	[InlineData("?format=webp&format=avif")]
	public async Task RejectsInvalidOrDuplicateQueryParametersWithBadRequest(string query)
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var uploaded = await MediaTestData.UploadImageAsync(client, "png", width: 320, height: 240);
		using var response = await client.GetAsync($"{uploaded.Url}{query}");

		Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
	}

	[Fact]
	public async Task SupportsHeadAndIfNoneMatch304AndInvalidatesCacheOnAssetDeletion()
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var uploaded = await MediaTestData.UploadImageAsync(client, "png", width: 640, height: 480);
		var url = $"{uploaded.Url}?width=320&format=webp";

		using var getResponse = await client.GetAsync(url);

		Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
		var etag = getResponse.Headers.ETag;
		var body = await getResponse.Content.ReadAsByteArrayAsync();

		Assert.NotNull(etag);
		Assert.NotEmpty(body);

		using var headRequest = new HttpRequestMessage(HttpMethod.Head, url);
		using var headResponse = await client.SendAsync(headRequest);

		Assert.Equal(HttpStatusCode.OK, headResponse.StatusCode);
		Assert.Equal(etag, headResponse.Headers.ETag);
		Assert.Equal(body.Length, headResponse.Content.Headers.ContentLength);
		Assert.Empty(await headResponse.Content.ReadAsByteArrayAsync());

		using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, url);

		conditionalRequest.Headers.IfNoneMatch.Add(etag);
		using var conditionalResponse = await client.SendAsync(conditionalRequest);

		Assert.Equal(HttpStatusCode.NotModified, conditionalResponse.StatusCode);
		Assert.Equal(etag, conditionalResponse.Headers.ETag);
		Assert.Empty(await conditionalResponse.Content.ReadAsByteArrayAsync());

		var assetCacheDirectory = Path.Combine(factory.MediaCachePath, uploaded.Id.ToString("N"));

		Assert.True(Directory.Exists(assetCacheDirectory));

		using var deleteResponse = await client.DeleteAsync($"/media/assets/{uploaded.Id}");

		Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
		Assert.False(Directory.Exists(assetCacheDirectory));
	}

	[Fact]
	public async Task PassesThroughSvgWithSandboxCspAndVideoWithByteRanges()
	{
		await using var factory = new GameApiFactory();

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var svg = await MediaTestData.UploadImageAsync(client, "svg");
		using var svgResponse = await client.GetAsync($"{svg.Url}?width=320&format=auto");

		Assert.Equal(HttpStatusCode.OK, svgResponse.StatusCode);
		Assert.Equal("image/svg+xml", svgResponse.Content.Headers.ContentType?.MediaType);
		Assert.Contains("sandbox", svgResponse.Headers.GetValues("Content-Security-Policy").Single());

		var videoId = Guid.CreateVersion7();
		var videoPath = Path.Combine(factory.MediaStoragePath, "assets", videoId.ToString("N"));
		var mp4Bytes = CreateMinimalMp4Header();

		Directory.CreateDirectory(Path.GetDirectoryName(videoPath)!);
		await File.WriteAllBytesAsync(videoPath, mp4Bytes);
		using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, $"/media/files/{videoId}?width=640&format=auto");

		rangeRequest.Headers.Range = new RangeHeaderValue(0, 7);
		using var rangeResponse = await client.SendAsync(rangeRequest);

		Assert.Equal(HttpStatusCode.PartialContent, rangeResponse.StatusCode);
		Assert.Equal("video/mp4", rangeResponse.Content.Headers.ContentType?.MediaType);
		Assert.Equal(8, (await rangeResponse.Content.ReadAsByteArrayAsync()).Length);
	}

	[Fact]
	public async Task BusyFallbackServesOriginalWithNoStoreCacheControl()
	{
		await using var factory = new GameApiFactory(configureServices: services =>
		{
			services.AddSingleton<IImageProcessor, AlwaysBusyImageProcessor>();
		});

		await factory.MigrateAsync();
		using var client = await factory.CreateHttpsClientAsync();
		var uploaded = await MediaTestData.UploadImageAsync(client, "png", width: 640, height: 480);
		using var request = new HttpRequestMessage(HttpMethod.Get, $"{uploaded.Url}?width=320&format=auto");

		request.Headers.Accept.ParseAdd("image/avif,image/webp,*/*");
		using var response = await client.SendAsync(request);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
		Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
		Assert.Contains("Accept", response.Headers.Vary);
	}

	private static byte[] CreateMinimalMp4Header() =>
	[
		0x00, 0x00, 0x00, 0x18,
		(byte)'f', (byte)'t', (byte)'y', (byte)'p',
		(byte)'i', (byte)'s', (byte)'o', (byte)'m',
		0x00, 0x00, 0x02, 0x00,
		(byte)'i', (byte)'s', (byte)'o', (byte)'m',
		(byte)'i', (byte)'s', (byte)'o', (byte)'2'
	];

	private sealed class AlwaysBusyImageProcessor : IImageProcessor
	{
		public int? ReadIntrinsicWidth(string sourcePath) => 640;

		public Task<ImageProcessResult> ProcessAsync(ImageProcessRequest request,
			CancellationToken cancellationToken) =>
			Task.FromResult(ImageProcessResult.BusyTimeout());
	}
}
