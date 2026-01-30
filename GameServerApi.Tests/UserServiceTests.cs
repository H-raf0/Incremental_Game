using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
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

        [Fact]
        public async Task RegisterUser_ShouldCreateUser_WhenValidData()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            var configMock = new Mock<IConfiguration>();
            // Use a sufficiently long key for HmacSha256 (>= 32 bytes)
            configMock.Setup(c => c["Jwt:Key"]).Returns("abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");
            configMock.Setup(c => c["Jwt:Issuer"]).Returns("localhost:5000");
            configMock.Setup(c => c["Jwt:Audience"]).Returns("localhost:5000");

            var jwtService = new JwtService(configMock.Object);

            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var userPass = new UserPass("TestUser", "Password123!");

            var (token, userPublic) = await service.RegisterUserAsync(userPass);

            Assert.False(string.IsNullOrEmpty(token));
            Assert.Equal("TestUser", userPublic.Username);

            var userInDb = await context.Users.FirstOrDefaultAsync(u => u.Username == "TestUser");
            Assert.NotNull(userInDb);
            Assert.Equal(Role.ADMIN, userInDb.Role);
        }

        [Fact]
        public async Task RegisterUser_ShouldThrow_WhenDuplicateUsername()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            var existing = new User("Existing", "pwd", Role.USER);
            context.Users.Add(existing);
            await context.SaveChangesAsync();

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Jwt:Key"]).Returns("abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");
            var jwtService = new JwtService(configMock.Object);

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

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Jwt:Key"]).Returns("abcdefghijklmnopqrstuvwxyz0123456789ABCDEFG");
            var jwtService = new JwtService(configMock.Object);

            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var (token, userPublic) = await service.LoginAsync(new UserPass("loginUser", "Password123!"));

            Assert.False(string.IsNullOrEmpty(token));
            Assert.Equal("loginUser", userPublic.Username);
        }

        [Fact]
        public async Task Login_ShouldThrow_WhenPasswordInvalid()
        {
            var context = CreateContext(Guid.NewGuid().ToString());

            var user = new User("loginUser2", "PasswordABC", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["Jwt:Key"]).Returns("TestSecretKeyForJwt_ChangeMe");
            var jwtService = new JwtService(configMock.Object);

            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.LoginAsync(new UserPass("loginUser2", "WrongPassword"));
            });
        }
    }
}
