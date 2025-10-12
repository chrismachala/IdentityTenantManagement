namespace IdentityTenantManagement.Models.Helpers;

//HttpMethod method, string endpoint, object body
 
public class RequestMessageModel
{
    public HttpMethod Method  { get; set; }
    public object Body { get; set; }
    public string Endpoint { get; set; }
    public ByteArrayContent Content { get; set; }
    
}