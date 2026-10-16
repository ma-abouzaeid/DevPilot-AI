using DevPilotAI.API.Controllers;
using DevPilotAI.Application.Authentication;
using DevPilotAI.Application.Authentication.DTOs;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock;
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _authServiceMock = new Mock<IAuthService>();
        _controller = new AuthController(_authServiceMock.Object);
    }

    [Fact]
    public async Task Register_ValidDto_ShouldReturnCreatedResult()
    {
        var dto = new RegisterRequestDto
        {
            Name = "John Doe",
            Email = "john@example.com",
            Password = "Password123"
        };

        var responseDto = new RegisterResponseDto(
            Success: true,
            Message: "Registration successful.",
            Email: dto.Email,
            RequiresVerification: true
        );

        _authServiceMock
            .Setup(s => s.RegisterAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseDto);

        var result = await _controller.Register(dto, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(201, createdResult.StatusCode);
        Assert.NotNull(createdResult.Value);
    }

    [Fact]
    public async Task Login_ValidDto_ShouldReturnOkResult()
    {
        var dto = new LoginRequestDto
        {
            Email = "john@example.com",
            Password = "Password123"
        };

        var userDto = new UserDto(1, "John Doe", "john@example.com", DateTime.UtcNow);
        var responseDto = new LoginResponseDto(
            Success: true,
            AccessToken: "access-token-xyz",
            RefreshToken: "refresh-token-xyz",
            User: userDto
        );

        _authServiceMock
            .Setup(s => s.LoginAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseDto);

        var result = await _controller.Login(dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task RefreshToken_ValidDto_ShouldReturnOkResult()
    {
        var dto = new RefreshTokenRequestDto
        {
            RefreshToken = "existing-refresh-token"
        };

        var responseDto = new RefreshTokenResponseDto(
            Success: true,
            AccessToken: "new-access-token",
            RefreshToken: "new-refresh-token"
        );

        _authServiceMock
            .Setup(s => s.RefreshTokenAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseDto);

        var result = await _controller.RefreshToken(dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task Logout_ValidDto_ShouldReturnOkResult()
    {
        var dto = new LogoutRequestDto
        {
            RefreshToken = "token-to-revoke"
        };

        var responseDto = new LogoutResponseDto(
            Success: true,
            Message: "Logged out successfully."
        );

        _authServiceMock
            .Setup(s => s.LogoutAsync(dto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(responseDto);

        var result = await _controller.Logout(dto, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.NotNull(okResult.Value);
    }
}
