using Microsoft.AspNetCore.OpenApi;

namespace Pinveria.Api.OpenApi.Optional;

public static class OptionalOpenApiExtensions
{
	public static OpenApiOptions AddOptionalSupport(this OpenApiOptions options)
	{
		var createReferenceId = options.CreateSchemaReferenceId;

		options.CreateSchemaReferenceId = jsonTypeInfo =>
		{
			if (OptionalSchemaTransformer.IsOptional(jsonTypeInfo.Type))
				return null;

			return createReferenceId(jsonTypeInfo);
		};

		options.AddSchemaTransformer<OptionalSchemaTransformer>();

		return options;
	}
}
