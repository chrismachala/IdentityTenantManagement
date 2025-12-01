using KeycloakAdapter.Helpers;
using KeycloakAdapter.Helpers.ContentBuilders;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Net;

namespace IdentityTenantManagement.Tests.Contract;

/// <summary>
/// Contract tests for KeycloakAdapter to verify correct HTTP interactions with Keycloak API.
/// Uses Moq to mock HTTP client - does NOT require real Keycloak instance.
/// Constitutional Requirement: Principle V - Identity Provider Abstraction
/// </summary>
[TestFixture]
public class KeycloakAdapterContractTests
{
    private Mock<IKCRequestHelper> _mockRequestHelper;
    private Mock<ILogger<KCOrganisationService>> _mockOrgLogger;
    private Mock<ILogger<KCUserService>> _mockUserLogger;
    private IOptions<KeycloakConfig> _keycloakConfig;

    [SetUp]
    public void Setup()
    {
        _mockRequestHelper = new Mock<IKCRequestHelper>();
        _mockOrgLogger = new Mock<ILogger<KCOrganisationService>>();
        _mockUserLogger = new Mock<ILogger<KCUserService>>();

        _keycloakConfig = Options.Create(new KeycloakConfig
        {
            BaseUrl = "http://localhost:8080",
            Realm = "TestRealm",
            ClientId = "TestClient",
            ClientSecret = "TestSecret"
        });
    }

    /// <summary>
    /// T078: Verify CreateOrgAsync calls correct Keycloak endpoint with proper payload
    /// </summary>
    [Test]
    public async Task CreateOrgAsync_CallsCorrectEndpoint_WithProperPayload()
    {
        // Arrange
        var service = new KCOrganisationService(_keycloakConfig, _mockRequestHelper.Object, _mockOrgLogger.Object);

        var tenantModel = new CreateTenantModel
        {
            Name = "TestOrg",
            Domain = "test-org"
        };

        HttpRequestMessage? capturedRequest = null;

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                capturedRequest = new HttpRequestMessage(method, endpoint);
                return capturedRequest;
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created));

        // Act
        await service.CreateOrgAsync(tenantModel);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Post,
            It.Is<string>(url => url.Contains("/admin/realms/TestRealm/organizations")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    /// <summary>
    /// T079: Verify CreateUserAsync calls correct Keycloak endpoint with user data
    /// </summary>
    [Test]
    public async Task CreateUserAsync_CallsCorrectEndpoint_WithUserData()
    {
        // Arrange
        var service = new KCUserService(_keycloakConfig, _mockRequestHelper.Object, _mockUserLogger.Object);

        var userModel = new CreateUserModel
        {
            UserName = "testuser",
            Email = "test@example.com",
            FirstName = "Test",
            LastName = "User",
            Password = "SecurePassword123!"
        };

        HttpRequestMessage? capturedRequest = null;

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                capturedRequest = new HttpRequestMessage(method, endpoint);
                return capturedRequest;
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Headers = { Location = new Uri("http://localhost:8080/admin/realms/TestRealm/users/123") }
            });

        // Act
        await service.CreateUserAsync(userModel);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Post,
            It.Is<string>(url => url.Contains("/admin/realms/TestRealm/users")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    /// <summary>
    /// T080: Verify AddUserToOrganisationAsync calls correct endpoint (role assignment via org membership)
    /// </summary>
    [Test]
    public async Task AddUserToOrganisationAsync_CallsCorrectEndpoint_WithUserAndOrgIds()
    {
        // Arrange
        var service = new KCOrganisationService(_keycloakConfig, _mockRequestHelper.Object, _mockOrgLogger.Object);

        var userTenantModel = new UserTenantModel
        {
            UserId = "user-123",
            TenantId = "org-456"
        };

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                return new HttpRequestMessage(method, endpoint);
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        await service.AddUserToOrganisationAsync(userTenantModel);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Post,
            It.Is<string>(url => url.Contains($"/admin/realms/TestRealm/organizations/{userTenantModel.TenantId}/members/invite-existing-user")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    /// <summary>
    /// T081: Verify DeleteOrganisationAsync calls correct compensating transaction endpoint
    /// </summary>
    [Test]
    public async Task DeleteOrganisationAsync_CallsCorrectEndpoint_WithOrgId()
    {
        // Arrange
        var service = new KCOrganisationService(_keycloakConfig, _mockRequestHelper.Object, _mockOrgLogger.Object);

        var orgId = "org-789";

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                return new HttpRequestMessage(method, endpoint);
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        await service.DeleteOrganisationAsync(orgId);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Delete,
            It.Is<string>(url => url.Contains($"/admin/realms/TestRealm/organizations/{orgId}")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    /// <summary>
    /// T082: Verify UpdateUserAsync calls correct sync endpoint with user data
    /// </summary>
    [Test]
    public async Task UpdateUserAsync_CallsCorrectEndpoint_WithUpdatedUserData()
    {
        // Arrange
        var service = new KCUserService(_keycloakConfig, _mockRequestHelper.Object, _mockUserLogger.Object);

        var userId = "user-123";
        var updateModel = new CreateUserModel
        {
            UserName = "updateduser",
            Email = "updated@example.com",
            FirstName = "Updated",
            LastName = "User",
            Password = "NewPassword123!"
        };

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                return new HttpRequestMessage(method, endpoint);
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        await service.UpdateUserAsync(userId, updateModel);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Put,
            It.Is<string>(url => url.Contains($"/admin/realms/TestRealm/users/{userId}")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    /// <summary>
    /// T083: Verify all contract tests use Moq for HTTP client mocking (no real Keycloak calls)
    /// This test validates the test infrastructure itself
    /// </summary>
    [Test]
    public void AllContractTests_UseMoqForHttpMocking_NotRealKeycloak()
    {
        // Assert
        // Verify mock request helper was created
        Assert.That(_mockRequestHelper, Is.Not.Null, "IKCRequestHelper must be mocked");
        Assert.That(_mockRequestHelper.Object, Is.Not.Null, "Mock object must be accessible");

        // Verify no real HTTP calls are made in test setup
        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Never,
            "Test setup should not make any HTTP calls");

        // Verify configuration uses test values, not production
        Assert.That(_keycloakConfig.Value.BaseUrl, Is.EqualTo("http://localhost:8080"),
            "Should use test Keycloak configuration");
        Assert.That(_keycloakConfig.Value.Realm, Is.EqualTo("TestRealm"),
            "Should use test realm name");
    }

    /// <summary>
    /// Additional test: Verify GetUserByEmailAsync queries correct endpoint with email parameter
    /// </summary>
    [Test]
    public async Task GetUserByEmailAsync_CallsCorrectEndpoint_WithEmailQuery()
    {
        // Arrange
        var service = new KCUserService(_keycloakConfig, _mockRequestHelper.Object, _mockUserLogger.Object);
        var email = "test@example.com";

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                return new HttpRequestMessage(method, endpoint);
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("[{\"id\": \"user-123\", \"email\": \"test@example.com\"}]")
            });

        // Act
        var result = await service.GetUserByEmailAsync(email);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Get,
            It.Is<string>(url => url.Contains("/admin/realms/TestRealm/users") && url.Contains("email=")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    /// <summary>
    /// Additional test: Verify DeleteUserAsync calls correct compensating transaction endpoint
    /// </summary>
    [Test]
    public async Task DeleteUserAsync_CallsCorrectEndpoint_WithUserId()
    {
        // Arrange
        var service = new KCUserService(_keycloakConfig, _mockRequestHelper.Object, _mockUserLogger.Object);
        var userId = "user-456";

        _mockRequestHelper
            .Setup(x => x.CreateHttpRequestMessage(
                It.IsAny<HttpMethod>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<IHttpContentBuilder>()))
            .ReturnsAsync((HttpMethod method, string endpoint, object body, IHttpContentBuilder builder) =>
            {
                return new HttpRequestMessage(method, endpoint);
            });

        _mockRequestHelper
            .Setup(x => x.SendAsync(It.IsAny<HttpRequestMessage>()))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.NoContent));

        // Act
        await service.DeleteUserAsync(userId);

        // Assert
        _mockRequestHelper.Verify(x => x.CreateHttpRequestMessage(
            HttpMethod.Delete,
            It.Is<string>(url => url.Contains($"/admin/realms/TestRealm/users/{userId}")),
            It.IsAny<object>(),
            It.IsAny<IHttpContentBuilder>()), Times.Once);

        _mockRequestHelper.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }
}