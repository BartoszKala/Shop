using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityService.Domain.Common;

namespace IdentityService.Api.Json;

internal sealed class StronglyTypedIdJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => StronglyTypedIds.IsStronglyTypedId(typeToConvert);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(StronglyTypedIdJsonConverter<>).MakeGenericType(typeToConvert))!;

    private sealed class StronglyTypedIdJsonConverter<TId> : JsonConverter<TId>
        where TId : struct, IStronglyTypedId<TId>
    {
        public override TId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            TId.From(reader.GetGuid());

        public override void Write(Utf8JsonWriter writer, TId value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}