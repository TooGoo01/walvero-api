using FluentAssertions;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Entities.Users;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Loyalty.DataAccess.UnitOfWorks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Loyalty.Tests.Services
{
    public class WalveroWalletPassServiceTests
    {
        private readonly Mock<IUnitOfWork> _uowMock;
        private readonly Mock<ITenantRepository> _tenantRepositoryMock;
        private readonly Mock<IWalveroWalletPassRepository> _passRepositoryMock;
        private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
        private readonly Mock<IWalletCredentialProvider> _credProviderMock;
        private readonly Mock<IApplePassBuilder> _applePassBuilderMock;
        private readonly Mock<IApplePassSigner> _applePassSignerMock;
        private readonly Mock<IGoogleWalletServiceFactory> _googleWalletFactoryMock;
        private readonly Mock<IFilePathResolver> _filePathResolverMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<WalveroWalletPassService>> _loggerMock;
        private readonly Mock<IAppEnvironment> _envMock;
        private readonly WalveroWalletPassService _service;

        public WalveroWalletPassServiceTests()
        {
            _uowMock = new Mock<IUnitOfWork>();
            _tenantRepositoryMock = new Mock<ITenantRepository>();
            _passRepositoryMock = new Mock<IWalveroWalletPassRepository>();
            _userManagerMock = new Mock<UserManager<ApplicationUser>>(
                Mock.Of<IUserStore<ApplicationUser>>(),
                null, null, null, null, null, null, null, null);
            _credProviderMock = new Mock<IWalletCredentialProvider>();
            _applePassBuilderMock = new Mock<IApplePassBuilder>();
            _applePassSignerMock = new Mock<IApplePassSigner>();
            _googleWalletFactoryMock = new Mock<IGoogleWalletServiceFactory>();
            _filePathResolverMock = new Mock<IFilePathResolver>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<WalveroWalletPassService>>();
            _envMock = new Mock<IAppEnvironment>();

            _configurationMock.Setup(c => c["App:BaseUrl"]).Returns("https://test.walvero.com");
            _envMock.Setup(e => e.WebRootPath).Returns("/wwwroot");

            _service = new WalveroWalletPassService(
                _uowMock.Object,
                _tenantRepositoryMock.Object,
                _passRepositoryMock.Object,
                _userManagerMock.Object,
                _credProviderMock.Object,
                _applePassBuilderMock.Object,
                _applePassSignerMock.Object,
                _googleWalletFactoryMock.Object,
                _filePathResolverMock.Object,
                _configurationMock.Object,
                _loggerMock.Object,
                _envMock.Object);
        }

        [Fact]
        public async Task CreateWalveroPassAsync_MissingWalveroId_Returns400()
        {
            // Arrange
            var userId = 1;
            var user = new ApplicationUser { Id = userId, WalveroId = null };
            _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
                .ReturnsAsync(user);

            // Act
            var result = await _service.CreateWalveroPassAsync(userId, "ios");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("walveroId");
        }

        [Fact]
        public async Task CreateWalveroPassAsync_InvalidPlatform_Returns400()
        {
            // Arrange
            var userId = 1;

            // Act
            var result = await _service.CreateWalveroPassAsync(userId, "invalid");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Platform");
        }

        [Fact]
        public async Task CreateWalveroPassAsync_WalveroTenantNotFound_Returns500()
        {
            // Arrange
            var userId = 1;
            var user = new ApplicationUser { Id = userId, WalveroId = 994100001 };
            _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
                .ReturnsAsync(user);
            _tenantRepositoryMock.Setup(r => r.FindWalveroTenantAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((Tenant?)null);

            // Act
            var result = await _service.CreateWalveroPassAsync(userId, "ios");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("shadow tenant missing");
        }

        [Fact]
        public async Task CreateWalveroPassAsync_UserNotFound_ReturnsFailure()
        {
            // Arrange
            var userId = 999;
            _userManagerMock.Setup(m => m.FindByIdAsync(userId.ToString()))
                .ReturnsAsync((ApplicationUser?)null);

            // Act
            var result = await _service.CreateWalveroPassAsync(userId, "ios");

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }
    }
}

