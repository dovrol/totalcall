using System.Text.Json;

namespace TotalCall.Client.Infrastructure.Json;

public static class JsonDataOptions
{
    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}
