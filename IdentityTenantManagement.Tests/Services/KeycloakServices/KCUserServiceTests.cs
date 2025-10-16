using System.Net;
using System.Text;
using System.Text.Json; 
using IO.Swagger.Model;
using KeycloakAdapter.Exceptions;
using KeycloakAdapter.Helpers;
using KeycloakAdapter.Helpers.ContentBuilders;
using KeycloakAdapter.Models;
using KeycloakAdapter.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NotFoundException = IdentityTenantManagement.Exceptions.NotFoundException;

namespace IdentityTenantManagement.Tests.Services.KeycloakServices;

[TestFixture]
public class KCUserServiceTests
{
    private Mock<IOptions<KeycloakConfig>> _mockOptions;
    private Mock<IKCRequestHelper> _mockRequestHelper;
    private KCUserService _service;
    private KeycloakConfig _config;
    private Mock<ILogger<KCUserService>> _kcUserServiceLogger;

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

        _mockRequestHelper = new Mock<IKCRequestHelper>();
        
        _kcUserServiceLogger =  new Mock<ILogger<KCUserService>>();

        _service = new KCUserService(_mockOptions.Object, _mockRequestHelper.Object,  _kcUserServiceLogger.Object);
    }

    #region GetUserByEmailAsync Tests

    [Test]
    public async Task GetUserByEmailAsync_ReturnsUser_WhenResponseIsSuccessful()
    {
        // Arrange
        var email = "test@example.com";
        var expectedUser = new UserRepresentation
        {
            Id = "user-123",
            Email = email,
            Username = "tester",
            FirstName = "Test",
            LastName = "User"
        };
        var json = JsonSerializer.Serialize(new List<UserRepresentation> { expectedUser });

        var httpResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users?q={email}");

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        var result = await _service.GetUserByEmailAsync(email);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Email, Is.EqualTo(email));
            Assert.That(result.Username, Is.EqualTo("tester"));
            Assert.That(result.Id, Is.EqualTo("user-123"));
        });

        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void GetUserByEmailAsync_ThrowsKeycloakException_WhenResponseIsNotSuccessful()
    {
        // Arrange
        var email = "bad@example.com";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, "fake");

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Internal server error", HttpStatusCode.InternalServerError, "Error"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(async () => await _service.GetUserByEmailAsync(email));
    }

    [Test]
    public void GetUserByEmailAsync_ThrowsNotFoundException_WhenUserNotFound()
    {
        // Arrange
        var email = "notfound@example.com";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, "fake");
        var json = JsonSerializer.Serialize(new List<UserRepresentation>());
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
        Assert.ThrowsAsync<NotFoundException>(async () => await _service.GetUserByEmailAsync(email));
    }

    [Test]
    [TestCase("")]
    [TestCase("invalid-email")]
    public void GetUserByEmailAsync_ThrowsKeycloakException_ForInvalidEmail(string email)
    {
        // Arrange
        var fakeRequest = new HttpRequestMessage(HttpMethod.Get, "fake");

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Get, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Bad request", HttpStatusCode.BadRequest, "Invalid email"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(async () => await _service.GetUserByEmailAsync(email));
    }

    #endregion

    #region CreateUserAsync Tests

    [Test]
    public async Task CreateUserAsync_SendsPostRequest_WithCorrectUserData()
    {
        // Arrange
        var model = new CreateUserModel
        {
            UserName = "newuser",
            Email = "new@example.com",
            FirstName = "John",
            LastName = "Doe",
            Password = "pass123"
        };

        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.Created);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        await _service.CreateUserAsync(model);

        // Assert
        _mockRequestHelper.Verify(h =>
            h.CreateHttpRequestMessage(
                HttpMethod.Post,
                endpoint,
                It.Is<UserRepresentation>(u =>
                    u.Username == model.UserName &&
                    u.Email == model.Email &&
                    u.FirstName == model.FirstName &&
                    u.LastName == model.LastName &&
                    u.Enabled == true &&
                    u.EmailVerified == true &&
                    u.Credentials.Count == 1
                ),
                It.IsAny<JsonContentBuilder>()),
            Times.Once);

        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void CreateUserAsync_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var model = new CreateUserModel
        {
            UserName = "bad",
            Email = "fail@example.com",
            FirstName = "Bad",
            LastName = "User",
            Password = "pass"
        };

        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Bad request", HttpStatusCode.BadRequest, "Error"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(() => _service.CreateUserAsync(model));
    }

    [Test]
    public void CreateUserAsync_ThrowsKeycloakException_WhenUserAlreadyExists()
    {
        // Arrange
        var model = new CreateUserModel
        {
            UserName = "existing",
            Email = "existing@example.com",
            FirstName = "Existing",
            LastName = "User",
            Password = "pass"
        };

        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Post, endpoint, It.IsAny<object>(), It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Conflict", HttpStatusCode.Conflict, "User already exists"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(() => _service.CreateUserAsync(model));
    }

    #endregion

    #region DeleteUserAsync Tests

    [Test]
    public async Task DeleteUserAsync_DeletesUserSuccessfully_WhenUserExists()
    {
        // Arrange
        var userId = "user-123";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);
        var httpResponse = new HttpResponseMessage(HttpStatusCode.NoContent);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ReturnsAsync(httpResponse);

        // Act
        await _service.DeleteUserAsync(userId);

        // Assert
        _mockRequestHelper.Verify(h =>
            h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()),
            Times.Once);
        _mockRequestHelper.Verify(h => h.SendAsync(It.IsAny<HttpRequestMessage>()), Times.Once);
    }

    [Test]
    public void DeleteUserAsync_ThrowsKeycloakException_WhenUserNotFound()
    {
        // Arrange
        var userId = "nonexistent-user";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("User not found", HttpStatusCode.NotFound, "Not found"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(() => _service.DeleteUserAsync(userId));
    }

    [Test]
    public void DeleteUserAsync_ThrowsKeycloakException_WhenRequestFails()
    {
        // Arrange
        var userId = "user-123";
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users/{userId}";

        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, endpoint, null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Internal server error", HttpStatusCode.InternalServerError, "Error"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(() => _service.DeleteUserAsync(userId));
    }

    [Test]
    [TestCase("")] 
    public void DeleteUserAsync_ThrowsException_WhenUserIdIsInvalid(string userId)
    {
        // Arrange
        var endpoint = $"{_config.BaseUrl}/admin/realms/{_config.Realm}/users/{userId}";
        var fakeRequest = new HttpRequestMessage(HttpMethod.Delete, endpoint);

        _mockRequestHelper
            .Setup(h => h.CreateHttpRequestMessage(HttpMethod.Delete, It.IsAny<string>(), null, It.IsAny<JsonContentBuilder>()))
            .ReturnsAsync(fakeRequest);

        _mockRequestHelper
            .Setup(h => h.SendAsync(fakeRequest))
            .ThrowsAsync(new KeycloakException("Bad request", HttpStatusCode.BadRequest, "Invalid user ID"));

        // Act & Assert
        Assert.ThrowsAsync<KeycloakException>(() => _service.DeleteUserAsync(userId));
    }

    #endregion
}
