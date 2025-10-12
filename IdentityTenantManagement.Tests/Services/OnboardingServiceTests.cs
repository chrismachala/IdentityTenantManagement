
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using IdentityTenantManagement.EFCore;
using IdentityTenantManagement.Models.Onboarding;
using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Models.Users;
using IdentityTenantManagement.Repositories;
using IdentityTenantManagement.Services;
using IdentityTenantManagement.Services.KeycloakServices;
using IO.Swagger.Model;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace IdentityTenantManagement.Tests.Services
{
    [TestFixture]
    public class OnboardingServiceTests
    {
        private Mock<IKCOrganisationService> _mockOrgService;
        private Mock<IKCUserService> _mockUserService;
        private Mock<IUserRepository> _mockUserRepository;
        private Mock<ITenantRepository> _mockTenantRepository;
        private OnboardingService _service;

        [SetUp]
        public void Setup()
        {
            _mockOrgService = new Mock<IKCOrganisationService>();
            _mockUserService = new Mock<IKCUserService>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTenantRepository = new Mock<ITenantRepository>();

            _service = new OnboardingService(
                _mockOrgService.Object,
                _mockUserService.Object,
                _mockUserRepository.Object,
                _mockTenantRepository.Object
            );
        }

        #region OnboardOrganisationAsync Tests

        [Test]
        public async Task OnboardOrganisationAsync_CompletesSuccessfully_WhenAllStepsSucceed()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();

            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel
                {
                    Email = "user@example.com",
                    FirstName = "John",
                    LastName = "Doe"
                },
                CreateTenantModel = new CreateTenantModel
                {
                    Domain = "example.com",
                    Name = "ExampleOrg"
                }
            };

            var userRep = new UserRepresentation(userId)
            {
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            var orgRep = new OrganizationRepresentation
            {
                Id = orgId,
                Name = "ExampleOrg",
                Domains = new List<OrganizationDomainRepresentation>
                {
                    new OrganizationDomainRepresentation("example.com")
                }
            };

            _mockUserService.Setup(x => x.CreateUserAsync(model.CreateUserModel)).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);

            _mockOrgService.Setup(x => x.CreateOrgAsync(model.CreateTenantModel)).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com")).ReturnsAsync(orgRep);
            _mockOrgService.Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>())).Returns(Task.CompletedTask);

            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _mockTenantRepository.Setup(x => x.AddAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

            // Act
            await _service.OnboardOrganisationAsync(model);

            // Assert
            _mockUserService.Verify(x => x.CreateUserAsync(model.CreateUserModel), Times.Once);
            _mockUserService.Verify(x => x.GetUserByEmailAsync("user@example.com"), Times.Once);

            _mockOrgService.Verify(x => x.CreateOrgAsync(model.CreateTenantModel), Times.Once);
            _mockOrgService.Verify(x => x.GetOrganisationByDomain("example.com"), Times.Once);
            _mockOrgService.Verify(x => x.AddUserToOrganisationAsync(It.Is<UserTenantModel>(
                u => u.UserId == userId && u.TenantId == orgId
            )), Times.Once);

            _mockUserRepository.Verify(x => x.AddAsync(It.Is<User>(u =>
                u.GUserId == Guid.Parse(userId) &&
                u.SEmail == "user@example.com" &&
                u.SFirstName == "John" &&
                u.SLastName == "Doe"
            )), Times.Once);

            _mockTenantRepository.Verify(x => x.AddAsync(It.Is<Tenant>(t =>
                t.GTenantId == Guid.Parse(orgId) &&
                t.SDomain == "example.com" &&
                t.SName == "ExampleOrg"
            )), Times.Once);
        }

        [Test]
        public void OnboardOrganisationAsync_Throws_WhenUserCreationFails()
        {
            // Arrange
            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "fail@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            _mockUserService
                .Setup(x => x.CreateUserAsync(model.CreateUserModel))
                .ThrowsAsync(new Exception("User creation failed"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("User creation failed"));
        }

        [Test]
        public void OnboardOrganisationAsync_Throws_WhenOrganisationCreationFails()
        {
            // Arrange
            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel
                {
                    Email = "user@example.com",
                    FirstName = "John",
                    LastName = "Doe"
                },
                CreateTenantModel = new CreateTenantModel
                {
                    Domain = "example.com",
                    Name = "ExampleOrg"
                }
            };

            var userRep = new UserRepresentation(Guid.NewGuid().ToString())
            {
                Email = "user@example.com",
                FirstName = "John",
                LastName = "Doe"
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);

            _mockOrgService
                .Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>()))
                .ThrowsAsync(new Exception("Organisation creation failed"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("Organisation creation failed"));
        }

        [Test]
        public void OnboardOrganisationAsync_Throws_WhenGetUserByEmailFails()
        {
            // Arrange
            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService
                .Setup(x => x.GetUserByEmailAsync("user@example.com"))
                .ThrowsAsync(new Exception("User not found"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("User not found"));
        }

        [Test]
        public void OnboardOrganisationAsync_Throws_WhenGetOrganisationByDomainFails()
        {
            // Arrange
            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            var userRep = new UserRepresentation(Guid.NewGuid().ToString())
            {
                Email = "user@example.com"
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);
            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService
                .Setup(x => x.GetOrganisationByDomain("example.com"))
                .ThrowsAsync(new Exception("Organisation not found"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("Organisation not found"));
        }

        [Test]
        public void OnboardOrganisationAsync_Throws_WhenAddUserToOrganisationFails()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();

            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            var userRep = new UserRepresentation(userId) { Email = "user@example.com" };
            var orgRep = new OrganizationRepresentation
            {
                Id = orgId,
                Domains = new List<OrganizationDomainRepresentation>
                {
                    new OrganizationDomainRepresentation("example.com")
                }
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);
            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com")).ReturnsAsync(orgRep);
            _mockOrgService
                .Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>()))
                .ThrowsAsync(new Exception("Failed to add user to organisation"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("Failed to add user to organisation"));
        }

        [Test]
        public void OnboardOrganisationAsync_Throws_WhenDatabaseSaveFails()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();

            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            var userRep = new UserRepresentation(userId) { Email = "user@example.com" };
            var orgRep = new OrganizationRepresentation
            {
                Id = orgId,
                Domains = new List<OrganizationDomainRepresentation>
                {
                    new OrganizationDomainRepresentation("example.com")
                }
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);
            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com")).ReturnsAsync(orgRep);
            _mockOrgService.Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>())).Returns(Task.CompletedTask);

            _mockUserRepository
                .Setup(x => x.AddAsync(It.IsAny<User>()))
                .ThrowsAsync(new DbUpdateException("Database save failed"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.TypeOf<DbUpdateException>());
        }

        [Test]
        public async Task OnboardOrganisationAsync_CallsServicesInCorrectOrder()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();
            var callOrder = new List<string>();

            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            var userRep = new UserRepresentation(userId) { Email = "user@example.com" };
            var orgRep = new OrganizationRepresentation
            {
                Id = orgId,
                Domains = new List<OrganizationDomainRepresentation>
                {
                    new OrganizationDomainRepresentation("example.com")
                }
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>()))
                .Returns(Task.CompletedTask)
                .Callback(() => callOrder.Add("CreateUser"));

            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com"))
                .ReturnsAsync(userRep)
                .Callback(() => callOrder.Add("GetUser"));

            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>()))
                .Returns(Task.CompletedTask)
                .Callback(() => callOrder.Add("CreateOrg"));

            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com"))
                .ReturnsAsync(orgRep)
                .Callback(() => callOrder.Add("GetOrg"));

            _mockOrgService.Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>()))
                .Returns(Task.CompletedTask)
                .Callback(() => callOrder.Add("AddUserToOrg"));

            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask)
                .Callback(() => callOrder.Add("AddUser"));

            _mockTenantRepository.Setup(x => x.AddAsync(It.IsAny<Tenant>()))
                .Returns(Task.CompletedTask)
                .Callback(() => callOrder.Add("AddTenant"));

            // Act
            await _service.OnboardOrganisationAsync(model);

            // Assert
            Assert.That(callOrder, Is.EqualTo(new List<string>
            {
                "CreateUser",
                "GetUser",
                "CreateOrg",
                "GetOrg",
                "AddUserToOrg",
                "AddUser",
                "AddTenant"
            }));
        }

        [Test]
        public async Task OnboardOrganisationAsync_HandlesMultipleDomains_UsesFirstDomain()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var orgId = Guid.NewGuid().ToString();

            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel
                {
                    Domain = "primary.com",
                    Name = "MultiDomainOrg"
                }
            };

            var userRep = new UserRepresentation(userId) { Email = "user@example.com" };
            var orgRep = new OrganizationRepresentation
            {
                Id = orgId,
                Name = "MultiDomainOrg",
                Domains = new List<OrganizationDomainRepresentation>
                {
                    new OrganizationDomainRepresentation("primary.com"),
                    new OrganizationDomainRepresentation("secondary.com"),
                    new OrganizationDomainRepresentation("tertiary.com")
                }
            };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);
            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("primary.com")).ReturnsAsync(orgRep);
            _mockOrgService.Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>())).Returns(Task.CompletedTask);

            _mockUserRepository.Setup(x => x.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _mockTenantRepository.Setup(x => x.AddAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

            // Act
            await _service.OnboardOrganisationAsync(model);

            // Assert - Verify that the first domain is used
            _mockTenantRepository.Verify(x => x.AddAsync(It.Is<Tenant>(t =>
                t.SDomain == "primary.com"
            )), Times.Once);
        }

        #endregion
    }
}
