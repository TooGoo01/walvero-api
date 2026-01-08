using FluentAssertions;
using Loyalty.Business.Dtos.Post.Main;
using Loyalty.Business.Results;
using Loyalty.Business.Services.Abstractions.Main;
using Loyalty.WebAPI.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Security.Claims;

namespace Loyalty.Tests.Controllers
{
    public class WalveroWalletPassControllerTests
    {
        private readonly Mock<IWalveroWalletPassService> _serviceMock;
        private readonly Mock<ITenantProvider> _tenantProviderMock;
        private readonly WalveroWalletPassController _controller;

        public WalveroWalletPassControllerTests()
        {
            _serviceMock = new Mock<IWalveroWalletPassService>();
            _tenantProviderMock = new Mock<ITenantProvider>();
            _controller = new WalveroWalletPassController(_serviceMock.Object, _tenantProviderMock.Object);
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

        [Fact]
        public async Task CreateWalveroPass_InvalidUserId_ReturnsBadRequest()
        {
            // Arrange
            SetupAuthenticatedUser();
            var request = new CreateWalveroPassRequestDto { UserId = 0, Platform = "ios" };

            // Act
            var result = await _controller.CreateWalveroPass(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateWalveroPass_InvalidPlatform_ReturnsBadRequest()
        {
            // Arrange
            SetupAuthenticatedUser();
            var request = new CreateWalveroPassRequestDto { UserId = 42, Platform = "" };

            // Act
            var result = await _controller.CreateWalveroPass(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateWalveroPass_WrongUser_Returns403()
        {
            // Arrange
            SetupAuthenticatedUser("42");
            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns("42");
            var request = new CreateWalveroPassRequestDto { UserId = 999, Platform = "ios" };

            // Act
            var result = await _controller.CreateWalveroPass(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<ObjectResult>();
            var objectResult = (ObjectResult)result.Result!;
            objectResult.StatusCode.Should().Be(403);
        }

        [Fact]
        public async Task CreateWalveroPass_MissingWalveroId_Returns400()
        {
            // Arrange
            SetupAuthenticatedUser("42");
            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns("42");
            var request = new CreateWalveroPassRequestDto { UserId = 42, Platform = "ios" };
            
            _serviceMock
                .Setup(s => s.CreateWalveroPassAsync(42, "ios", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalveroWalletPassResponseDto>.Fail("User has no walveroId"));

            // Act
            var result = await _controller.CreateWalveroPass(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<BadRequestObjectResult>();
        }

        [Fact]
        public async Task CreateWalveroPass_Success_Returns200()
        {
            // Arrange
            SetupAuthenticatedUser("42");
            _tenantProviderMock.Setup(p => p.CurrentUserId).Returns("42");
            var request = new CreateWalveroPassRequestDto { UserId = 42, Platform = "ios" };
            
            var response = new WalveroWalletPassResponseDto
            {
                TenantId = 999,
                UserId = 42,
                Platform = "ios",
                WalveroIdMasked = "WLV****001",
                ApplePassUrl = "/passes/walvero/1/test.pkpass",
                PassId = 1
            };

            _serviceMock
                .Setup(s => s.CreateWalveroPassAsync(42, "ios", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<WalveroWalletPassResponseDto>.Ok(response));

            // Act
            var result = await _controller.CreateWalveroPass(request, CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<OkObjectResult>();
        }
    }
}

