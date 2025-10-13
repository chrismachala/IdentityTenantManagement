using System.Net;
using System.Text;
using System.Text.Json;
using IdentityTenantManagement.Exceptions;
using IdentityTenantManagement.Helpers;
using IdentityTenantManagement.Helpers.ContentBuilders;
using IdentityTenantManagement.Models.Keycloak;
using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Services.KeycloakServices;
using IO.Swagger.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace IdentityTenantManagement.Tests.Services.KeycloakServices;

[TestFixture]
public class KCOrganisationServiceTests
{
    private Mock<IOptions<KeycloakConfig>> _mockOptions;
    private Mock<IKCRequestHelper> _mockRequestHelper;
    private KCOrganisationService _service;
    private KeycloakConfig _config;
    private Mock<ILogger<KCOrganisationService>> _kcOrganisationServiceLogger;

    [SetUp]
    public void Setup()
    {
        _config = new KeycloakConfig
        {
            BaseUrl = "http://localhost:8080",
            Realm = "test-realm"
        };

        _mockOptions = new Mock<IOptions<KeycloakConfig>>();
        _mockOptions.Setup(x => x.Value).Returns(_config);
        
        _kcOrganisationServiceLogger =  new Mock<ILogger<KCOrganisationService>>();

        _mockRequestHelper = new Mock<IKCRequestHelper>();
_service = new KCOrganisationService(_mockOptions.Object, _mockRequestHelper.Object, _kcOrganisationServiceLogger.Object); 
    }

    #region AddUserToOrganisationAsync Tests

    [Test]
    public async Task AddUserToOrganisationAsync_AddsUserSuccessfully_WhenRequestIsValid()
    {
        // Arrange
        var model = new UserTenantModel { TenantId = "tenant-123", UserId = "user-456" };
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{model.TenantId}/members/invite-existing-user";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<FormUrlEncodedContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        await _service.AddUserToOrganisationAsync(model);

        // Assert
        _mockRequestHelper.Verify(h =>
            h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.Is<object>(o => o != null), It.IsAny<FormUrlEncodedContentBuilder>()),
            Times.Once);
        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void AddUserToOrganisationAsync_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var model = new UserTenantModel { TenantId = "tenant-123", UserId = "user-456" };
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{model.TenantId}/members/invite-existing-user";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<FormUrlEncodedContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Request failed", HttpStatusCode.BadRequest, "Bad request"));

        // Act & Assert
        Assert.That(async () => await _service.AddUserToOrganisationAsync(model),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void AddUserToOrganisationAsync_ThrowsKeycloakException_WhenUserNotFound()
    {
        // Arrange
        var model = new UserTenantModel { TenantId = "tenant-123", UserId = "nonexistent-user" };
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{model.TenantId}/members/invite-existing-user";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<FormUrlEncodedContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("User not found", HttpStatusCode.NotFound, "Not found"));

        // Act & Assert
        Assert.That(async () => await _service.AddUserToOrganisationAsync(model),
            Throws.TypeOf<KeycloakException>());
    }

    #endregion

    #region GetOrganisationByDomain Tests

    [Test]
    public async Task GetOrganisationByDomain_ReturnsOrganisation_WhenDomainExists()
    {
        // Arrange
        var domain = "example.com";
        var expectedOrg = new OrganizationRepresentation
        {
            Id = "org-123",
            Name = "ExampleOrg",
            Enabled = true,
            Domains = new List<OrganizationDomainRepresentation>
            {
                new OrganizationDomainRepresentation { Name = domain }
            }
        };
        var json = JsonSerializer.Serialize(new List<OrganizationRepresentation> { expectedOrg });

        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations?search={domain}";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.GetOrganisationByDomain(domain);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Name, Is.EqualTo("ExampleOrg"));
            Assert.That(result.Id, Is.EqualTo("org-123"));
            Assert.That(result.Enabled, Is.True);
        });
    }

    [Test]
    public void GetOrganisationByDomain_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var domain = "bad-domain.com";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, "fake");

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Internal server error", HttpStatusCode.InternalServerError, "Error"));

        // Act & Assert
        Assert.That(async () => await _service.GetOrganisationByDomain(domain),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void GetOrganisationByDomain_ThrowsNotFoundException_WhenDomainNotFound()
    {
        // Arrange
        var domain = "nonexistent.com";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, "fake");
        var json = JsonSerializer.Serialize(new List<OrganizationRepresentation>());
        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act & Assert
        Assert.That(async () => await _service.GetOrganisationByDomain(domain),
            Throws.TypeOf<NotFoundException>()
                .With.Message.Contains("Organization"));
    }

    #endregion

    #region CreateOrgAsync Tests

    [Test]
    public async Task CreateOrgAsync_CreatesOrganisation_WithCorrectData()
    {
        // Arrange
        var model = new CreateTenantModel { Name = "NewOrg", Domain = "neworg.com" };
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.Created);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        await _service.CreateOrgAsync(model);

        // Assert
        _mockRequestHelper.Verify(h =>
            h.CreateHttpRequestMessage(
                HttpMethod.Post,
                endpoint,
                It.Is<OrganizationRepresentation>(o =>
                    o.Name == "NewOrg" &&
                    o.Enabled == true &&
                    o.Domains.Count == 1 &&
                    o.Domains.Any(d => d.Name == "neworg.com")
                ),
                It.IsAny<JsonContentBuilder>()),
            Times.Once);

        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void CreateOrgAsync_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var model = new CreateTenantModel { Name = "FailOrg", Domain = "fail.com" };
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Bad request", HttpStatusCode.BadRequest, "Error"));

        // Act & Assert
        Assert.That(async () => await _service.CreateOrgAsync(model),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void CreateOrgAsync_ThrowsKeycloakException_WhenOrganisationAlreadyExists()
    {
        // Arrange
        var model = new CreateTenantModel { Name = "ExistingOrg", Domain = "existing.com" };
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Conflict", HttpStatusCode.Conflict, "Already exists"));

        // Act & Assert
        Assert.That(async () => await _service.CreateOrgAsync(model),
            Throws.TypeOf<KeycloakException>());
    }

    #endregion

    #region DeleteOrganisationAsync Tests

    [Test]
    public async Task DeleteOrganisationAsync_DeletesOrganisationSuccessfully_WhenOrganisationExists()
    {
        // Arrange
        var orgId = "org-123";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NoContent);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        await _service.DeleteOrganisationAsync(orgId);

        // Assert
        _mockRequestHelper.Verify(h =>
            h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()),
            Times.Once);
        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void DeleteOrganisationAsync_ThrowsKeycloakException_WhenOrganisationNotFound()
    {
        // Arrange
        var orgId = "nonexistent-org";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Organisation not found", HttpStatusCode.NotFound, "Not found"));

        // Act & Assert
        Assert.That(async () => await _service.DeleteOrganisationAsync(orgId),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void DeleteOrganisationAsync_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var orgId = "org-123";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Internal server error", HttpStatusCode.InternalServerError, "Error"));

        // Act & Assert
        Assert.That(async () => await _service.DeleteOrganisationAsync(orgId),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void DeleteOrganisationAsync_ThrowsKeycloakException_WhenOrganisationHasMembers()
    {
        // Arrange
        var orgId = "org-with-members";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Cannot delete organisation with members", HttpStatusCode.Conflict, "Conflict"));

        // Act & Assert
        Assert.That(async () => await _service.DeleteOrganisationAsync(orgId),
            Throws.TypeOf<KeycloakException>());
    }

    #endregion

    #region RemoveUserFromOrganisationAsync Tests

    [Test]
    public async Task RemoveUserFromOrganisationAsync_RemovesUserSuccessfully_WhenValid()
    {
        // Arrange
        var userId = "user-123";
        var orgId = "org-456";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}/members/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NoContent);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        await _service.RemoveUserFromOrganisationAsync(userId, orgId);

        // Assert
        _mockRequestHelper.Verify(h =>
            h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()),
            Times.Once);
        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void RemoveUserFromOrganisationAsync_ThrowsKeycloakException_WhenUserNotInOrganisation()
    {
        // Arrange
        var userId = "user-123";
        var orgId = "org-456";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}/members/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("User not a member of organisation", HttpStatusCode.NotFound, "Not found"));

        // Act & Assert
        Assert.That(async () => await _service.RemoveUserFromOrganisationAsync(userId, orgId),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void RemoveUserFromOrganisationAsync_ThrowsKeycloakException_WhenOrganisationNotFound()
    {
        // Arrange
        var userId = "user-123";
        var orgId = "nonexistent-org";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}/members/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Organisation not found", HttpStatusCode.NotFound, "Not found"));

        // Act & Assert
        Assert.That(async () => await _service.RemoveUserFromOrganisationAsync(userId, orgId),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    public void RemoveUserFromOrganisationAsync_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var userId = "user-123";
        var orgId = "org-456";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}/members/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Internal server error", HttpStatusCode.InternalServerError, "Error"));

        // Act & Assert
        Assert.That(async () => await _service.RemoveUserFromOrganisationAsync(userId, orgId),
            Throws.TypeOf<KeycloakException>());
    }

    [Test]
    [TestCase("", "org-123")] 
    [TestCase("user-123", "")] 
    public void RemoveUserFromOrganisationAsync_ThrowsException_WhenParametersAreInvalid(string userId, string orgId)
    {
        // Arrange
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/organizations/{orgId}/members/{userId}";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Bad request", HttpStatusCode.BadRequest, "Invalid parameters"));

        // Act & Assert
        Assert.That(async () => await _service.RemoveUserFromOrganisationAsync(userId, orgId),
            Throws.TypeOf<KeycloakException>());
    }

    #endregion
}
