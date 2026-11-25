using System.ComponentModel;
using Pinveria.Application.Common.Sorting;
using Pinveria.Application.Games;
using Microsoft.AspNetCore.Mvc;

namespace Pinveria.Api.Games;

public sealed class ListGamesRequest
{
	[FromQuery, DefaultValue(0)]
	public int? Offset { get; init; }

	[FromQuery, DefaultValue(20)]
	public int? Limit { get; init; }

	[FromQuery, Description("Trimmed literal name substring; ignores ASCII case. Empty means no filter.")]
	public string? Search { get; init; }

	[FromQuery, Description("name, createdAt, or updatedAt (case-insensitive). Omit for createdAt ascending. Id ascending breaks ties.")]
	public GameSortBy? SortBy { get; init; }

	[FromQuery, Description("asc or desc (case-insensitive). Requires sortBy; defaults to asc with sortBy.")]
	public SortDirection? SortDirection { get; init; }
}
