namespace Pinveria.Application.Media;

public sealed record MediaFolderDetailsDto(
	MediaFolderDto Folder,
	IReadOnlyCollection<MediaFolderBreadcrumbDto> Ancestors);
