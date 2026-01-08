using FluentAssertions;
using Loyalty.Business.Dtos.Get.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Loyalty.Tests.Controllers
{
    public class WalletCardDetailsControllerTests
    {
        private readonly Mock<IWalletCardDetailsService> _serviceMock;
        private readonly WalletCardDetailsController _controller;

        public WalletCardDetailsControllerTests()
        {
            _serviceMock = new Mock<IWalletCardDetailsService>();
            _controller = new WalletCardDetailsController(_serviceMock.Object);
        }

        private void SetupAnonymousUser()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity()); // Not authenticated
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        private void SetupAuthenticatedUser(string userId = "42")
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, "Customer")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(identity);
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
        }

        #region GetTenantWalletInfo Tests

        [Fact]
        public async Task GetTenantWalletInfo_ValidTenant_ReturnsOk()
        {
            // Arrange
            SetupAnonymousUser();
            var tenantId = 1;
            var response = new WalletCardDetailsResponseDto
            {
                Mode = WalletCardDetailsMode.TenantOnly,
                Tenant = new TenantWalletPublicDto { TenantId = tenantId, TenantName = "Test" },
                Card = null
            };
            
            _serviceMock
                .Setup(s => s.GetTenantWalletInfoAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Ok(response));

            // Act
            var result = await _controller.GetTenantWalletInfo(tenantId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)result.Result!;
            var serviceResult = (ServiceResult<WalletCardDetailsResponseDto>)okResult.Value!;
            serviceResult.Success.Should().BeTrue();
            serviceResult.Data!.Mode.Should().Be(WalletCardDetailsMode.TenantOnly);
            serviceResult.Data.Card.Should().BeNull();
        }

        [Fact]
        public async Task GetTenantWalletInfo_AllowsAnonymous()
        {
            // Arrange
            SetupAnonymousUser();
            var tenantId = 1;
            var response = new WalletCardDetailsResponseDto
            {
                Mode = WalletCardDetailsMode.TenantOnly,
                Tenant = new TenantWalletPublicDto { TenantId = tenantId, TenantName = "Test" },
                Card = null
            };
            
            _serviceMock
                .Setup(s => s.GetTenantWalletInfoAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Ok(response));

            // Act
            var result = await _controller.GetTenantWalletInfo(tenantId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
        }

        [Fact]
        public async Task GetTenantWalletInfo_TenantNotFound_Returns404()
        {
            // Arrange
            SetupAnonymousUser();
            var tenantId = 999;
            
            _serviceMock
                .Setup(s => s.GetTenantWalletInfoAsync(tenantId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Fail("Tenant not found"));

            // Act
            var result = await _controller.GetTenantWalletInfo(tenantId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetTenantWalletInfo_InvalidTenantId_ReturnsBadRequest()
        {
            // Arrange
            SetupAnonymousUser();
            var tenantId = 0;

            // Act
            var result = await _controller.GetTenantWalletInfo(tenantId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetTenantWalletInfo_NegativeTenantId_ReturnsBadRequest()
        {
            // Arrange
            SetupAnonymousUser();
            var tenantId = -1;

            // Act
            var result = await _controller.GetTenantWalletInfo(tenantId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        #endregion

        #region GetCardDetails Tests

        [Fact]
        public async Task GetCardDetails_AuthenticatedUser_ReturnsDetails()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 1;
            var cardId = 100;
            var response = new WalletCardDetailsResponseDto
            {
                Mode = WalletCardDetailsMode.CardDetails,
                Tenant = new TenantWalletPublicDto { TenantId = tenantId, TenantName = "Test" },
                Card = new WalletCardPrivateDto 
                { 
                    CardId = cardId, 
                    CardNumber = "TEST-001",
                    CurrentPoints = 500 
                }
            };
            
            _serviceMock
                .Setup(s => s.GetCardDetailsAsync(tenantId, cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Ok(response));

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
            var okResult = (OkObjectResult)result.Result!;
            var serviceResult = (ServiceResult<WalletCardDetailsResponseDto>)okResult.Value!;
            serviceResult.Success.Should().BeTrue();
            serviceResult.Data!.Mode.Should().Be(WalletCardDetailsMode.CardDetails);
            serviceResult.Data.Card.Should().NotBeNull();
        }

        [Fact]
        public async Task GetCardDetails_CardNotFound_Returns404()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 1;
            var cardId = 999;
            
            _serviceMock
                .Setup(s => s.GetCardDetailsAsync(tenantId, cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Fail("Card not found"));

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetCardDetails_AuthenticationRequired_Returns401()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 1;
            var cardId = 100;
            
            _serviceMock
                .Setup(s => s.GetCardDetailsAsync(tenantId, cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Fail("Authentication required"));

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<UnauthorizedObjectResult>();
        }

        [Fact]
        public async Task GetCardDetails_InvalidTenantId_ReturnsBadRequest()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 0;
            var cardId = 100;

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetCardDetails_InvalidCardId_ReturnsBadRequest()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 1;
            var cardId = 0;

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetCardDetails_NegativeCardId_ReturnsBadRequest()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 1;
            var cardId = -1;

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task GetCardDetails_TenantMismatch_Returns404()
        {
            // Arrange
            SetupAuthenticatedUser();
            var tenantId = 1;
            var cardId = 100;
            
            // Service returns "not found" for tenant mismatch (to avoid leaking info)
            _serviceMock
                .Setup(s => s.GetCardDetailsAsync(tenantId, cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Fail("Card not found"));

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        [Fact]
        public async Task GetCardDetails_UnauthorizedAccess_Returns404()
        {
            // Arrange
            SetupAuthenticatedUser("attacker-user");
            var tenantId = 1;
            var cardId = 100;
            
            // Service returns "not found" for unauthorized access (to avoid leaking info)
            _serviceMock
                .Setup(s => s.GetCardDetailsAsync(tenantId, cardId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalletCardDetailsResponseDto>.Fail("Card not found"));

            // Act
            var result = await _controller.GetCardDetails(tenantId, cardId, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<NotFoundObjectResult>();
        }

        #endregion
    }
}
