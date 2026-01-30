using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using GameServerApi.Exceptions;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;

namespace GameServerApi.Tests
{
    public class UserServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private JwtService CreateJwtService()
        {
            var jwtSettings = new JwtSettings
            {
                Key = "YourSuperSecretKeyOfAtLeast32CharactersLongForHmacSha256!!",
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenExpirationMinutes = 60
            };
            var options = Options.Create(jwtSettings);
            return new JwtService(options, new NullLogger<JwtService>());
        }

        [Fact]
        public async Task RegisterUser_ShouldCreateUser_WhenValidData()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = CreateJwtService();
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var userPass = new UserPass("TestUser", "Password123!");
            var (tokenResponse, userPublic) = await service.RegisterUserAsync(userPass);

            Assert.NotNull(tokenResponse);
            Assert.False(string.IsNullOrEmpty(tokenResponse.AccessToken));
            Assert.False(string.IsNullOrEmpty(tokenResponse.RefreshToken));
            Assert.Equal("TestUser", userPublic.Username);
            Assert.Equal(Role.ADMIN, userPublic.Role); // First user is admin

            var userInDb = await context.Users.FirstOrDefaultAsync(u => u.Username == "TestUser");
            Assert.NotNull(userInDb);
        }

        [Fact]
        public async Task RegisterUser_ShouldThrow_WhenDuplicateUsername()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var existing = new User("Existing", "pwd", Role.USER);
            context.Users.Add(existing);
            await context.SaveChangesAsync();

            var jwtService = CreateJwtService();
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.RegisterUserAsync(new UserPass("Existing", "pwd2"));
            });
        }

        [Fact]
        public async Task Login_ShouldReturnToken_WhenCredentialsValid()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("loginUser", "Password123!", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var jwtService = CreateJwtService();
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var (tokenResponse, userPublic) = await service.LoginAsync(new UserPass("loginUser", "Password123!"));

            Assert.NotNull(tokenResponse);
            Assert.False(string.IsNullOrEmpty(tokenResponse.AccessToken));
            Assert.Equal("loginUser", userPublic.Username);
        }

        [Fact]
        public async Task Login_ShouldThrow_WhenPasswordInvalid()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("loginUser2", "PasswordABC", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var jwtService = CreateJwtService();
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.LoginAsync(new UserPass("loginUser2", "WrongPassword"));
            });
        }

        [Fact]
        public async Task RefreshToken_ShouldReturnNewTokens_WhenValidRefreshToken()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = CreateJwtService();
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            // Register to get tokens
            var (firstTokens, _) = await service.RegisterUserAsync(new UserPass("refreshUser", "Password123!"));
            var refreshToken = firstTokens.RefreshToken;

            // Use refresh token
            var (newTokens, _) = await service.RefreshTokenAsync(refreshToken);

            Assert.NotNull(newTokens);
            Assert.False(string.IsNullOrEmpty(newTokens.AccessToken));
            Assert.NotEqual(firstTokens.AccessToken, newTokens.AccessToken); // New access token
            Assert.NotEqual(refreshToken, newTokens.RefreshToken); // New refresh token
        }

        [Fact]
        public async Task RefreshToken_ShouldThrow_WhenInvalidRefreshToken()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = CreateJwtService();
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.RefreshTokenAsync("invalid-refresh-token");
            });
        }
    }
}
