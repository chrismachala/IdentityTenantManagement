using IdentityTenantManagement.Constants;
using IdentityTenantManagement.Models.Onboarding;
using IdentityTenantManagement.Models.Organisations;
using IdentityTenantManagement.Models.Users;
using IdentityTenantManagement.Repositories;
using IdentityTenantManagement.Services;
using IdentityTenantManagement.Services.KeycloakServices;
using IdentityTenantManagementDatabase.Models;
using IO.Swagger.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace IdentityTenantManagement.Tests.Services
{
    [TestFixture]
    public class OnboardingServiceTests
    {
        private Mock<IKCOrganisationService> _mockOrgService;
        private Mock<IKCUserService> _mockUserService;
        private Mock<IUnitOfWork> _mockUnitOfWork;
        private Mock<IUserRepository> _mockUserRepository;
        private Mock<ITenantRepository> _mockTenantRepository;
        private Mock<IExternalIdentityRepository> _mockExternalIdentityRepository;
        private Mock<IIdentityProviderRepository> _mockIdentityProviderRepository;
        private Mock<ITenantUserRepository> _mockTenantUserRepository;
        private Mock<ILogger<OnboardingService>> _mockLogger;
        private OnboardingService _service;
        private IdentityProvider _keycloakProvider;

        [SetUp]
        public void Setup()
        {
            _mockOrgService = new Mock<IKCOrganisationService>();
            _mockUserService = new Mock<IKCUserService>();
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockUserRepository = new Mock<IUserRepository>();
            _mockTenantRepository = new Mock<ITenantRepository>();
            _mockExternalIdentityRepository = new Mock<IExternalIdentityRepository>();
            _mockIdentityProviderRepository = new Mock<IIdentityProviderRepository>();
            _mockTenantUserRepository = new Mock<ITenantUserRepository>();
            _mockLogger = new Mock<ILogger<OnboardingService>>();

            // Set up the pre-seeded Keycloak provider
            _keycloakProvider = new IdentityProvider
            {
                Id = Guid.Parse("049284C1-FF29-4F28-869F-F64300B69719"),
                Name = "Keycloak",
                ProviderType = "oidc"
            };

            // Set up repository properties on UnitOfWork
            _mockUnitOfWork.Setup(x => x.Users).Returns(_mockUserRepository.Object);
            _mockUnitOfWork.Setup(x => x.Tenants).Returns(_mockTenantRepository.Object);
            _mockUnitOfWork.Setup(x => x.ExternalIdentities).Returns(_mockExternalIdentityRepository.Object);
            _mockUnitOfWork.Setup(x => x.IdentityProviders).Returns(_mockIdentityProviderRepository.Object);
            _mockUnitOfWork.Setup(x => x.TenantUsers).Returns(_mockTenantUserRepository.Object);

            // Mock IdentityProvider lookup
            _mockIdentityProviderRepository
                .Setup(x => x.GetByNameAsync("Keycloak"))
                .ReturnsAsync(_keycloakProvider);

            _service = new OnboardingService(
                _mockOrgService.Object,
                _mockUserService.Object,
                _mockUnitOfWork.Object,
                _mockLogger.Object
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

            _mockUnitOfWork.Setup(x => x.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.Tenants.AddAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

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

            // Verify User is created with internal GUID (NOT Keycloak GUID)
            _mockUserRepository.Verify(x => x.AddAsync(It.Is<User>(u =>
                u.Email == "user@example.com" &&
                u.FirstName == "John" &&
                u.LastName == "Doe"
            )), Times.Once);

            // Verify Tenant is created with internal GUID (NOT Keycloak GUID)
            _mockTenantRepository.Verify(x => x.AddAsync(It.Is<Tenant>(t =>
                t.Domains.Any(d => d.Domain == "example.com") &&
                t.Name == "ExampleOrg"
            )), Times.Once);

            // Verify ExternalIdentity records are created for both user and tenant
            _mockExternalIdentityRepository.Verify(x => x.AddAsync(It.Is<ExternalIdentity>(e =>
                e.EntityTypeId == ExternalIdentityEntityTypeIds.User &&
                e.ExternalIdentifier == userId &&
                e.ProviderId == _keycloakProvider.Id
            )), Times.Once);

            _mockExternalIdentityRepository.Verify(x => x.AddAsync(It.Is<ExternalIdentity>(e =>
                e.EntityTypeId == ExternalIdentityEntityTypeIds.Tenant &&
                e.ExternalIdentifier == orgId &&
                e.ProviderId == _keycloakProvider.Id
            )), Times.Once);

            // Verify TenantUser relationship is created
            _mockTenantUserRepository.Verify(x => x.AddAsync(It.Is<TenantUser>(tu =>
                tu.Role == "owner"
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

            _mockUnitOfWork
                .Setup(x => x.Users.AddAsync(It.IsAny<User>()))
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

            _mockUnitOfWork.Setup(x => x.Users.AddAsync(It.IsAny<User>()))
                .Returns(Task.CompletedTask)
                .Callback(() => callOrder.Add("AddUser"));

            _mockUnitOfWork.Setup(x => x.Tenants.AddAsync(It.IsAny<Tenant>()))
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

            _mockUnitOfWork.Setup(x => x.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.Tenants.AddAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);

            // Act
            await _service.OnboardOrganisationAsync(model);

            // Assert - Verify that the first domain is used
            _mockUnitOfWork.Verify(x => x.Tenants.AddAsync(It.Is<Tenant>(t =>
                t.Domains.Any(d => d.IsPrimary && d.Domain == "primary.com")
            )), Times.Once);
        }

        #endregion

        #region Saga Compensating Transaction Tests

        [Test]
        public async Task OnboardOrganisationAsync_DeletesCreatedUser_WhenOrganisationCreationFails()
        {
            // Arrange
            var userId = Guid.NewGuid().ToString();
            var model = new TenantUserOnboardingModel
            {
                CreateUserModel = new CreateUserModel { Email = "user@example.com" },
                CreateTenantModel = new CreateTenantModel { Domain = "example.com" }
            };

            var userRep = new UserRepresentation(userId) { Email = "user@example.com" };

            _mockUserService.Setup(x => x.CreateUserAsync(It.IsAny<CreateUserModel>())).Returns(Task.CompletedTask);
            _mockUserService.Setup(x => x.GetUserByEmailAsync("user@example.com")).ReturnsAsync(userRep);
            _mockUserService.Setup(x => x.DeleteUserAsync(userId)).Returns(Task.CompletedTask);

            _mockOrgService
                .Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>()))
                .ThrowsAsync(new Exception("Organisation creation failed"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("Organisation creation failed"));

            // Verify compensating transaction
            _mockUserService.Verify(x => x.DeleteUserAsync(userId), Times.Once);
        }

        [Test]
        public async Task OnboardOrganisationAsync_DeletesUserAndOrg_WhenLinkingFails()
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
            _mockUserService.Setup(x => x.DeleteUserAsync(userId)).Returns(Task.CompletedTask);

            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com")).ReturnsAsync(orgRep);
            _mockOrgService.Setup(x => x.DeleteOrganisationAsync(orgId)).Returns(Task.CompletedTask);
            _mockOrgService
                .Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>()))
                .ThrowsAsync(new Exception("Failed to add user to organisation"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.Exception.With.Message.EqualTo("Failed to add user to organisation"));

            // Verify compensating transactions in reverse order
            _mockOrgService.Verify(x => x.DeleteOrganisationAsync(orgId), Times.Once);
            _mockUserService.Verify(x => x.DeleteUserAsync(userId), Times.Once);
        }

        [Test]
        public async Task OnboardOrganisationAsync_RollsBackAllChanges_WhenDatabaseCommitFails()
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
            _mockUserService.Setup(x => x.DeleteUserAsync(userId)).Returns(Task.CompletedTask);

            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com")).ReturnsAsync(orgRep);
            _mockOrgService.Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.RemoveUserFromOrganisationAsync(userId, orgId)).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.DeleteOrganisationAsync(orgId)).Returns(Task.CompletedTask);

            _mockUnitOfWork.Setup(x => x.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.Tenants.AddAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(null!));
            _mockUnitOfWork.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Database commit failed"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.TypeOf<DbUpdateException>());

            // Verify all compensating transactions were called
            _mockUnitOfWork.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockOrgService.Verify(x => x.RemoveUserFromOrganisationAsync(userId, orgId), Times.Once);
            _mockOrgService.Verify(x => x.DeleteOrganisationAsync(orgId), Times.Once);
            _mockUserService.Verify(x => x.DeleteUserAsync(userId), Times.Once);
        }

        [Test]
        public async Task OnboardOrganisationAsync_ContinuesCompensatingTransactions_EvenWhenSomeFail()
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
            _mockUserService.Setup(x => x.DeleteUserAsync(userId)).Returns(Task.CompletedTask);

            _mockOrgService.Setup(x => x.CreateOrgAsync(It.IsAny<CreateTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.GetOrganisationByDomain("example.com")).ReturnsAsync(orgRep);
            _mockOrgService.Setup(x => x.AddUserToOrganisationAsync(It.IsAny<UserTenantModel>())).Returns(Task.CompletedTask);
            _mockOrgService.Setup(x => x.DeleteOrganisationAsync(orgId)).Returns(Task.CompletedTask);

            // RemoveUser fails, but other compensations should still execute
            _mockOrgService
                .Setup(x => x.RemoveUserFromOrganisationAsync(userId, orgId))
                .ThrowsAsync(new Exception("Failed to remove user"));

            _mockUnitOfWork.Setup(x => x.Users.AddAsync(It.IsAny<User>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.Tenants.AddAsync(It.IsAny<Tenant>())).Returns(Task.CompletedTask);
            _mockUnitOfWork.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(null!));
            _mockUnitOfWork.Setup(x => x.RollbackAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _mockUnitOfWork
                .Setup(x => x.CommitAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DbUpdateException("Database commit failed"));

            // Act & Assert
            Assert.That(async () => await _service.OnboardOrganisationAsync(model),
                Throws.TypeOf<DbUpdateException>());

            // Verify all compensating transactions were attempted despite failure
            _mockUnitOfWork.Verify(x => x.RollbackAsync(It.IsAny<CancellationToken>()), Times.Once);
            _mockOrgService.Verify(x => x.RemoveUserFromOrganisationAsync(userId, orgId), Times.Once);
            _mockOrgService.Verify(x => x.DeleteOrganisationAsync(orgId), Times.Once);
            _mockUserService.Verify(x => x.DeleteUserAsync(userId), Times.Once);
        }

        #endregion
    }
}
