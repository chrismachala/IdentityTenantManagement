using System.Text;
using Newtonsoft.Json;

namespace KeycloakAdapter.Helpers.ContentBuilders;

public class JsonContentBuilder : IHttpContentBuilder
{
    public string ContentType => "application/json";

    public HttpContent Build(object body)
    {
        if (body == null) return null;
        var json = JsonConvert.SerializeObject(body, Formatting.None,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        return new StringContent(json, Encoding.UTF8, ContentType);
    }
}