using System.Text.Json.Serialization.Metadata;
using Pinveria.Domain;

namespace Pinveria.Api.Serialization.Optional;

internal static class OptionalJsonTypeInfoModifier
{
	internal static void Modify(JsonTypeInfo typeInfo)
	{
		if (typeInfo.Kind != JsonTypeInfoKind.Object)
			return;

		foreach (var property in typeInfo.Properties)
		{
			if (!typeof(IOptional).IsAssignableFrom(property.PropertyType))
				continue;

			property.ShouldSerialize = static (_, value) => value is IOptional { IsSpecified: true };
		}
	}
}
