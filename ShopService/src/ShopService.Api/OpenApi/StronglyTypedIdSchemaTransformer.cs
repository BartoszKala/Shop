using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using ShopService.Domain.Common;

namespace ShopService.Api.OpenApi;

internal sealed class StronglyTypedIdSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
        if (StronglyTypedIds.IsStronglyTypedId(type))
        {
            schema.Type = JsonSchemaType.String;
            schema.Format = "uuid";
            schema.Properties?.Clear();
            schema.Required?.Clear();
        }

        return Task.CompletedTask;
    }
}
