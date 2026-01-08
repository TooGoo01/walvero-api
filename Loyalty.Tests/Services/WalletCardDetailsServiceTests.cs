using FluentAssertions;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.Business.Services.Implementations.Main;
using Loyalty.DataAccess.Entities.Main;
using Loyalty.DataAccess.Enums;
using Loyalty.DataAccess.Repositories.Abstractions.Main;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace Loyalty.Tests.Services
{
    public class WalletCardDetailsServiceTests
    {
        private readonly Mock<ITenantProvider> _tenantProviderMock;
        private readonly Mock<ITenantRepository> _tenantRepositoryMock;
        private readonly Mock<ILoyaltyCardRepository> _cardRepositoryMock;
        private readonly Mock<IConfiguration> _configurationMock;
        private readonly Mock<ILogger<WalletCardDetailsService>> _loggerMock;
        private readonly WalletCardDetailsService _service;

        public WalletCardDetailsServiceTests()
        {
            _tenantProviderMock = new Mock<ITenantProvider>();
            _tenantRepositoryMock = new Mock<ITenantRepository>();
            _cardRepositoryMock = new Mock<ILoyaltyCardRepository>();
            _configurationMock = new Mock<IConfiguration>();
            _loggerMock = new Mock<ILogger<WalletCardDetailsService>>();

            // Setup default configuration
            _configurationMock.Setup(c => c["App:BaseUrl"]).Returns("https://test.walvero.com");

            _service = new WalletCardDetailsService(
                _tenantProviderMock.Object,
                _tenantRepositoryMock.Object,
                _cardRepositoryMock.Object,
                _configurationMock.Object,
                _loggerMock.Object);
        }

        #region GetTenantWalletInfoAsync Tests

        [Fact]
        public async Task GetTenantWalletInfoAsync_ValidTenant_ReturnsPublicInfoOnly()
        {
            // Arrange
            var tenantId = 1;
            var tenant = CreateTestTenant(tenantId);
            
            _tenantRepositoryMock
                .Setup(r => r.GetTenantForWalletDetailsAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            // Act
            var result = await _service.GetTenantWalletInfoAsync(tenantId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Mode.Should().Be(WalletCardDetailsMode.TenantOnly);
            result.Data.Tenant.Should().NotBeNull();
            result.Data.Card.Should().BeNull("Card should be null in TenantOnly mode");
            
            // Verify tenant info is populated
            result.Data.Tenant.TenantId.Should().Be(tenantId);
            result.Data.Tenant.TenantName.Should().Be("Test Company");
        }

        [Fact]
        public async Task GetTenantWalletInfoAsync_ValidTenant_DoesNotContainSensitiveFields()
        {
            // Arrange
            var tenantId = 1;
            var tenant = CreateTestTenant(tenantId);
            
            _tenantRepositoryMock
                .Setup(r => r.GetTenantForWalletDetailsAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            // Act
            var result = await _service.GetTenantWalletInfoAsync(tenantId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Card.Should().BeNull("No card data should be present in TenantOnly mode");
            
            // Ensure no customer-specific data is present
            // (Card being null means no CardNumber, BarcodeValue, Points, etc.)
        }

        [Fact]
        public async Task GetTenantWalletInfoAsync_TenantNotFound_ReturnsFailure()
        {
            // Arrange
            var tenantId = 999;
            
            _tenantRepositoryMock
                .Setup(r => r.GetTenantForWalletDetailsAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Tenant?)null);

            // Act
            var result = await _service.GetTenantWalletInfoAsync(tenantId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task GetTenantWalletInfoAsync_ReturnsCorrectBrandingInfo()
        {
            // Arrange
            var tenantId = 1;
            var tenant = CreateTestTenant(tenantId);
            
            _tenantRepositoryMock
                .Setup(r => r.GetTenantForWalletDetailsAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tenant);

            // Act
            var result = await _service.GetTenantWalletInfoAsync(tenantId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Tenant.PrimaryColor.Should().Be("#FF0000");
            result.Data.Tenant.SecondaryColor.Should().Be("#00FF00");
            result.Data.Tenant.Description.Should().Be("Test description");
            result.Data.Tenant.LogoUrl.Should().Contain("test.walvero.com");
        }

        #endregion

        #region GetCardDetailsAsync Tests

        [Fact]
        public async Task GetCardDetailsAsync_AuthorizedUser_ReturnsFullDetails()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, tenantId, applicationUserId);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, applicationUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Mode.Should().Be(WalletCardDetailsMode.CardDetails);
            result.Data.Tenant.Should().NotBeNull();
            result.Data.Card.Should().NotBeNull("Card should be present in CardDetails mode");
            
            // Verify card details
            result.Data.Card!.CardId.Should().Be(cardId);
            result.Data.Card.CardNumber.Should().Be("TEST-CARD-001");
            result.Data.Card.CurrentPoints.Should().Be(500);
        }

        [Fact]
        public async Task GetCardDetailsAsync_CardNotFound_ReturnsFailure()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 999;
            
            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns("42");
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((LoyaltyCard?)null);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found");
        }

        [Fact]
        public async Task GetCardDetailsAsync_TenantMismatch_ReturnsNotFound()
        {
            // Arrange
            var requestedTenantId = 1;
            var actualTenantId = 2;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, actualTenantId, applicationUserId); // Card belongs to different tenant

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            // Act
            var result = await _service.GetCardDetailsAsync(requestedTenantId, cardId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found", "Should not reveal that card exists in different tenant");
        }

        [Fact]
        public async Task GetCardDetailsAsync_UnauthorizedUser_ReturnsNotFound()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var ownerUserId = 42;
            var attackerUserId = 999;
            var card = CreateTestCard(cardId, tenantId, ownerUserId);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(attackerUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, attackerUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("not found", "Should not reveal card exists to unauthorized user");
        }

        [Fact]
        public async Task GetCardDetailsAsync_NoAuthenticatedUser_ReturnsAuthenticationRequired()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var card = CreateTestCard(cardId, tenantId, 42);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns((string?)null);
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Authentication required");
        }

        [Fact]
        public async Task GetCardDetailsAsync_InvalidUserIdFormat_ReturnsAuthenticationRequired()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var card = CreateTestCard(cardId, tenantId, 42);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns("not-a-number");
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Authentication required");
        }

        [Fact]
        public async Task GetCardDetailsAsync_ReturnsCustomerSensitiveData()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, tenantId, applicationUserId);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, applicationUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Card.Should().NotBeNull();
            
            // Verify sensitive data is present
            result.Data.Card!.CardNumber.Should().NotBeEmpty();
            result.Data.Card.BarcodeValue.Should().NotBeEmpty();
            result.Data.Card.CustomerFullName.Should().Be("John Doe");
            result.Data.Card.CustomerPhone.Should().Be("+994501234567");
        }

        #endregion

        #region Tier Calculation Tests

        [Fact]
        public async Task GetCardDetailsAsync_BronzeTier_CalculatesCorrectly()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, tenantId, applicationUserId, currentPoints: 50);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, applicationUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Card!.TierName.Should().Be("Bronze");
        }

        [Fact]
        public async Task GetCardDetailsAsync_SilverTier_CalculatesCorrectly()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, tenantId, applicationUserId, currentPoints: 1500);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, applicationUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Card!.TierName.Should().Be("Silver");
        }

        [Fact]
        public async Task GetCardDetailsAsync_GoldTier_CalculatesCorrectly()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, tenantId, applicationUserId, currentPoints: 5000);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, applicationUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Card!.TierName.Should().Be("Gold");
        }

        [Fact]
        public async Task GetCardDetailsAsync_PlatinumTier_CalculatesCorrectly()
        {
            // Arrange
            var tenantId = 1;
            var cardId = 100;
            var applicationUserId = 42;
            var card = CreateTestCard(cardId, tenantId, applicationUserId, currentPoints: 15000);

            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns(applicationUserId.ToString());
            _cardRepositoryMock
                .Setup(r => r.GetCardWithFullDetailsAsync(cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(card);
            _cardRepositoryMock
                .Setup(r => r.CardBelongsToUserAsync(cardId, applicationUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.GetCardDetailsAsync(tenantId, cardId);

            // Assert
            result.Success.Should().BeTrue();
            result.Data!.Card!.TierName.Should().Be("Platinum");
        }

        #endregion

        #region Helper Methods

        private static Tenant CreateTestTenant(int tenantId)
        {
            return new Tenant
            {
                Id = tenantId,
                CompanyName = "Test Company",
                Subdomain = "test",
                ContactEmail = "test@example.com",
                ContactPhone = "+994501234567",
                LocationUrl = "https://maps.google.com/test",
                IsActive = true,
                Branding = new TenantBranding
                {
                    Id = 1,
                    TenantId = tenantId,
                    LogoUrl = "uploads/logo.png",
                    PrimaryColor = "#FF0000",
                    SecondaryColor = "#00FF00",
                    BackgroundColor = "#FFFFFF",
                    TextColor = "#000000",
                    Description = "Test description",
                    Website = "https://test.com"
                },
                Category = new Category
                {
                    Id = 1,
                    AzName = "Test Kateqoriya",
                    EnName = "Test Category",
                    RuName = "Тестовая категория"
                },
                LoyaltyPrograms = new List<LoyaltyProgram>
                {
                    new LoyaltyProgram
                    {
                        Id = 1,
                        TenantId = tenantId,
                        ProgramName = "Test Program",
                        Description = "Test program description",
                        IsActive = true,
                        CardTemplate = new CardTemplate
                        {
                            Id = 1,
                            LoyaltyProgramId = 1,
                            RewardDescription = "Get 10% cashback",
                            BackgroundColor = "#FFFFFF",
                            ForegroundColor = "#000000",
                            LabelColor = "#999999",
                            BronzeThreshold = 0,
                            BronzePercent = 2,
                            SilverThreshold = 1000,
                            SilverPercent = 5,
                            GoldThreshold = 5000,
                            GoldPercent = 10,
                            PlatinumThreshold = 10000,
                            PlatinumPercent = 15
                        }
                    }
                }
            };
        }

        private static LoyaltyCard CreateTestCard(int cardId, int tenantId, int applicationUserId, int currentPoints = 500)
        {
            var tenant = CreateTestTenant(tenantId);
            var program = tenant.LoyaltyPrograms.First();

            return new LoyaltyCard
            {
                Id = cardId,
                TenantId = tenantId,
                CustomerId = 1,
                LoyaltyProgramId = program.Id,
                CardNumber = "TEST-CARD-001",
                BarcodeValue = "BC123456789",
                BarcodeType = BarcodeType.QR,
                CurrentPoints = currentPoints,
                LifetimePoints = currentPoints + 100,
                CurrentSteps = 5,
                CompletedCycles = 2,
                Status = CardStatus.Active,
                ActivatedAt = DateTime.UtcNow.AddMonths(-6),
                IsActive = true,
                Customer = new Customer
                {
                    Id = 1,
                    TenantId = tenantId,
                    FirstName = "John",
                    LastName = "Doe",
                    PhoneNumber = "+994501234567",
                    Email = "john@example.com",
                    applicationUserId = applicationUserId,
                    IsActive = true
                },
                Tenant = tenant,
                LoyaltyProgram = program
            };
        }

        #endregion
    }
}
