namespace Pinveria.Infrastructure.Maps.Storage;

public sealed class MapStorageOptions
{
	public string StorageRoot { get; init; } = "storage/maps";
	public bool WorkerEnabled { get; init; } = true;
}
