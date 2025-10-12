namespace IdentityTenantManagement.Helpers.ContentBuilders;

public interface IHttpContentBuilder
{
    string ContentType { get; }
    HttpContent Build(object body);
}