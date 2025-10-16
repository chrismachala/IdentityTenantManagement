namespace KeycloakAdapter.Exceptions;

public class NotFoundException : Exception
{
    public string ResourceType { get; }
    public string ResourceIdentifier { get; }

    public NotFoundException(string resourceType, string resourceIdentifier)
        : base($"{resourceType} with identifier '{resourceIdentifier}' was not found.")
    {
        ResourceType = resourceType;
        ResourceIdentifier = resourceIdentifier;
    }

    public NotFoundException(string resourceType, string resourceIdentifier, Exception innerException)
        : base($"{resourceType} with identifier '{resourceIdentifier}' was not found.", innerException)
    {
        ResourceType = resourceType;
        ResourceIdentifier = resourceIdentifier;
    }
}