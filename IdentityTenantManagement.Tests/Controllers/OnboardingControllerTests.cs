using IdentityTenantManagement.Controllers;
using IdentityTenantManagement.Models.Onboarding; 
using IdentityTenantManagement.Services;
using KeycloakAdapter.Models;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace IdentityTenantManagement.Tests.Controllers;

[TestFixture]
public class OnboardingControllerTests
{
    private Mock<IOnboardingService> _mockOnboardingService;
    private OnboardingController _controller;

    [SetUp]
    public void Setup()
    {
        _mockOnboardingService = new Mock<IOnboardingService>();
        _controller = new OnboardingController(_mockOnboardingService.Object);
    }

    #region OnboardOrganisation Tests

    [Test]
    public async Task OnboardOrganisation_ReturnsOk_WhenOnboardingSucceeds()
    {
        // Arrange
        var model = new TenantUserOnboardingModel
        {
            CreateUserModel = new CreateUserModel
            {
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Password = "password123"
            },
            CreateTenantModel = new CreateTenantModel
            {
                Name = "TestOrg",
                Domain = "testorg.com"
            }
        };

        _mockOnboardingService
            .Setup(s => s.OnboardOrganisationAsync(It.IsAny<TenantUserOnboardingModel>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.OnboardOrganisation(model);

        // Assert
        Assert.That(result, Is.InstanceOf<OkObjectResult>());

        var okResult = result as OkObjectResult;
        Assert.That(okResult, Is.Not.Null);
        Assert.That(okResult!.StatusCode, Is.EqualTo(200));

        var responseValue = okResult.Value;
        Assert.That(responseValue, Is.Not.Null);

        var message = responseValue!.GetType().GetProperty("message")?.GetValue(responseValue);
        Assert.That(message, Is.EqualTo("Client Onboarded successfully"));

        _mockOnboardingService.Verify(s => s.OnboardOrganisationAsync(model), Times.Once);
    }

    [Test]
    public void OnboardOrganisation_ThrowsException_WhenServiceFails()
    {
        // Arrange
        var model = new TenantUserOnboardingModel
        {
            CreateUserModel = new CreateUserModel
            {
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Password = "password123"
            },
            CreateTenantModel = new CreateTenantModel
            {
                Name = "TestOrg",
                Domain = "testorg.com"
            }
        };

        _mockOnboardingService
            .Setup(s => s.OnboardOrganisationAsync(It.IsAny<TenantUserOnboardingModel>()))
            .ThrowsAsync(new Exception("Onboarding failed"));

        // Act & Assert
        Assert.ThrowsAsync<Exception>(async () =>
            await _controller.OnboardOrganisation(model));

        _mockOnboardingService.Verify(s => s.OnboardOrganisationAsync(model), Times.Once);
    }

    [Test]
    public void OnboardOrganisation_ThrowsHttpRequestException_WhenKeycloakFails()
    {
        // Arrange
        var model = new TenantUserOnboardingModel
        {
            CreateUserModel = new CreateUserModel
            {
                UserName = "testuser",
                Email = "test@example.com",
                FirstName = "Test",
                LastName = "User",
                Password = "password123"
            },
            CreateTenantModel = new CreateTenantModel
            {
                Name = "TestOrg",
                Domain = "testorg.com"
            }
        };

        _mockOnboardingService
            .Setup(s => s.OnboardOrganisationAsync(It.IsAny<TenantUserOnboardingModel>()))
            .ThrowsAsync(new HttpRequestException("Keycloak request failed"));

        // Act & Assert
        Assert.ThrowsAsync<HttpRequestException>(async () =>
            await _controller.OnboardOrganisation(model));

        _mockOnboardingService.Verify(s => s.OnboardOrganisationAsync(model), Times.Once);
    }

    [Test]
    public async Task OnboardOrganisation_PassesCorrectModel_ToService()
    {
        // Arrange
        var model = new TenantUserOnboardingModel
        {
            CreateUserModel = new CreateUserModel
            {
                UserName = "specificuser",
                Email = "specific@example.com",
                FirstName = "Specific",
                LastName = "User",
                Password = "specificpassword"
            },
            CreateTenantModel = new CreateTenantModel
            {
                Name = "SpecificOrg",
                Domain = "specificorg.com"
            }
        };

        _mockOnboardingService
            .Setup(s => s.OnboardOrganisationAsync(It.IsAny<TenantUserOnboardingModel>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.OnboardOrganisation(model);

        // Assert - Verify the exact model was passed
        _mockOnboardingService.Verify(s => s.OnboardOrganisationAsync(
            It.Is<TenantUserOnboardingModel>(m =>
                m.CreateUserModel.UserName == "specificuser" &&
                m.CreateUserModel.Email == "specific@example.com" &&
                m.CreateUserModel.FirstName == "Specific" &&
                m.CreateUserModel.LastName == "User" &&
                m.CreateTenantModel.Name == "SpecificOrg" &&
                m.CreateTenantModel.Domain == "specificorg.com")),
            Times.Once);
    }

    [Test]
    public async Task OnboardOrganisation_CallsServiceOnce()
    {
        // Arrange
        var model = new TenantUserOnboardingModel
        {
            CreateUserModel = new CreateUserModel
            {
                UserName = "testuser",
                Email = "test@example.com"
            },
            CreateTenantModel = new CreateTenantModel
            {
                Name = "TestOrg",
                Domain = "testorg.com"
            }
        };

        _mockOnboardingService
            .Setup(s => s.OnboardOrganisationAsync(It.IsAny<TenantUserOnboardingModel>()))
            .Returns(Task.CompletedTask);

        // Act
        await _controller.OnboardOrganisation(model);

        // Assert
        _mockOnboardingService.Verify(
            s => s.OnboardOrganisationAsync(It.IsAny<TenantUserOnboardingModel>()),
            Times.Once);
        _mockOnboardingService.VerifyNoOtherCalls();
    }

    #endregion
}