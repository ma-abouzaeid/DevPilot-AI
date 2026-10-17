using System.Security.Claims;
using DevPilotAI.API.Controllers;
using DevPilotAI.Application.Authentication.DTOs;
using DevPilotAI.Application.Common.Models;
using DevPilotAI.Application.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DevPilotAI.Tests;

public class UsersControllerTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly UsersController _controller;

    public UsersControllerTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _controller = new UsersController(_userServiceMock.Object);
    }

    [Fact]
    public async Task DeleteMyAccount_ValidAuthenticatedClaim_ShouldReturnOk()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "developer@example.com")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        _userServiceMock
            .Setup(s => s.DeleteAccountAsync(42, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteCurrentUser(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        _userServiceMock.Verify(s => s.DeleteAccountAsync(42, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteMyAccount_MissingUserClaim_ShouldReturnUnauthorized()
    {
        var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = unauthenticatedUser }
        };

        var result = await _controller.DeleteCurrentUser(CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        _userServiceMock.Verify(s => s.DeleteAccountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteMyAccount_CannotDeleteOtherUser_ClaimIsAuthoritative()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "15"),
            new Claim(ClaimTypes.Email, "current@example.com")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        await _controller.DeleteCurrentUser(CancellationToken.None);

        _userServiceMock.Verify(s => s.DeleteAccountAsync(15, It.IsAny<CancellationToken>()), Times.Once);
        _userServiceMock.Verify(s => s.DeleteAccountAsync(It.Is<int>(id => id != 15), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetCurrentProfile_ValidAuthenticatedClaim_ShouldReturnOk()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "developer@example.com")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        var expectedDto = new UserDto(42, "John Doe", "john@example.com", null);
        _userServiceMock.Setup(s => s.GetCurrentProfileAsync(42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        var result = await _controller.GetCurrentProfile(CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(expectedDto, response.Data);
    }

    [Fact]
    public async Task GetCurrentProfile_MissingUserClaim_ShouldReturnUnauthorized()
    {
        var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = unauthenticatedUser }
        };

        var result = await _controller.GetCurrentProfile(CancellationToken.None);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        _userServiceMock.Verify(s => s.GetCurrentProfileAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateCurrentProfile_ValidAuthenticatedClaim_ShouldReturnOk()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "42"),
            new Claim(ClaimTypes.Email, "developer@example.com")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        var request = new UpdateUserProfileRequestDto("Jane Doe");
        var updatedDto = new UserDto(42, "Jane Doe", "john@example.com", null);
        _userServiceMock.Setup(s => s.UpdateCurrentProfileAsync(42, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDto);

        var result = await _controller.UpdateCurrentProfile(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(updatedDto, response.Data);
    }

    [Fact]
    public async Task UpdateCurrentProfile_MissingUserClaim_ShouldReturnUnauthorized()
    {
        var unauthenticatedUser = new ClaimsPrincipal(new ClaimsIdentity());
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = unauthenticatedUser }
        };
        var request = new UpdateUserProfileRequestDto("Jane Doe");

        var result = await _controller.UpdateCurrentProfile(request, CancellationToken.None);
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        _userServiceMock.Verify(s => s.UpdateCurrentProfileAsync(It.IsAny<int>(), It.IsAny<UpdateUserProfileRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Ensure a user cannot update another user's profile because the endpoint derives the ID from the token.
    [Fact]
    public async Task UpdateCurrentProfile_AttemptToUpdateOtherUser_IgnoredBecauseIdFromClaim()
    {
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "15"),
            new Claim(ClaimTypes.Email, "current@example.com")
        }, "mock"));

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };
        var request = new UpdateUserProfileRequestDto("New Name");
        var updatedDto = new UserDto(15, "New Name", "current@example.com", null);
        _userServiceMock.Setup(s => s.UpdateCurrentProfileAsync(15, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedDto);

        var result = await _controller.UpdateCurrentProfile(request, CancellationToken.None);
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        var response = Assert.IsType<ApiResponse<UserDto>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal(updatedDto, response.Data);
    }
}
