namespace KeycloakAdapter.Helpers.ContentBuilders;

public class FormUrlEncodedContentBuilder : IHttpContentBuilder
{
    public string ContentType => "application/x-www-form-urlencoded";

    public HttpContent Build(object body)
    {
        if (body is IEnumerable<KeyValuePair<string, string>> kvp)
            return new FormUrlEncodedContent(kvp);

        var dict = body.GetType()
            .GetProperties()
            .ToDictionary(p => p.Name, p => p.GetValue(body)?.ToString() ?? string.Empty);

        return new FormUrlEncodedContent(dict);
    }
}