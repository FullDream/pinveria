namespace Pinveria.Api.OpenApi.Validation;

public interface IOpenApiValidationSource
{
	Type ContractType { get; }
	Type ValidationType { get; }
}
