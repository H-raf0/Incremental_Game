using System;
using System.Threading.Tasks;
using System.Linq;
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

            var (token, userPublic) = await service.RegisterAsync(userPass);

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
                await service.RegisterAsync(new UserPass("Existing", "pwd2"));
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

        [Fact]
        public async Task Login_ShouldThrow_WhenUserNotFound()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.LoginAsync(new UserPass("missing", "pwd"));
            });
        }

        [Fact]
        public async Task GetAllUsersAsync_ReturnsPublicUsers()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Users.Add(new User("u1", "pwd", Role.USER));
            context.Users.Add(new User("u2", "pwd", Role.ADMIN));
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var users = await service.GetAllUsersAsync();

            Assert.Equal(2, users.Count);
        }

        [Fact]
        public async Task GetUserByIdAsync_ReturnsUser_WhenExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("lookup", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var found = await service.GetUserByIdAsync(user.Id);

            Assert.Equal("lookup", found.Username);
        }

        [Fact]
        public async Task GetUserByIdAsync_Throws_WhenMissing()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.GetUserByIdAsync(999);
            });
        }

        [Fact]
        public async Task SearchUsersAsync_IsCaseInsensitive()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Users.Add(new User("AlphaUser", "pwd", Role.USER));
            context.Users.Add(new User("beta", "pwd", Role.USER));
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var results = await service.SearchUsersAsync("ALPHA");

            Assert.Single(results);
            Assert.Equal("AlphaUser", results.First().Username);
        }

        [Fact]
        public async Task SearchUsersAsync_ReturnsEmpty_WhenNameBlank()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var results = await service.SearchUsersAsync("  ");

            Assert.Empty(results);
        }

        [Fact]
        public async Task GetAllAdminUsersAsync_ReturnsOnlyAdmins()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Users.Add(new User("admin", "pwd", Role.ADMIN));
            context.Users.Add(new User("user", "pwd", Role.USER));
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var admins = await service.GetAllAdminUsersAsync();

            Assert.Single(admins);
            Assert.Equal("admin", admins.First().Username);
        }

        [Fact]
        public async Task RegisterUser_AssignsUserRole_WhenAdminExists()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Users.Add(new User("admin", "pwd", Role.ADMIN));
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var (_, userPublic) = await service.RegisterAsync(new UserPass("regular", "pwd"));

            Assert.Equal(Role.USER, userPublic.Role);
        }

        [Fact]
        public async Task UpdateUserAsync_UpdatesFields()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("old", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            var update = new UserUpdate("newname", "newpass", Role.ADMIN);
            var updated = await service.UpdateUserAsync(user.Id, update);

            Assert.Equal("newname", updated.Username);
            Assert.Equal(Role.ADMIN, updated.Role);
            Assert.True(updated.VerifyPassword("newpass"));
        }

        [Fact]
        public async Task UpdateUserAsync_Throws_WhenMissing()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.UpdateUserAsync(404, new UserUpdate("x", "y", Role.USER));
            });
        }

        [Fact]
        public async Task DeleteUserAsync_RemovesUser()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var user = new User("todelete", "pwd", Role.USER);
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await service.DeleteUserAsync(user.Id);

            var removed = await context.Users.FindAsync(user.Id);
            Assert.Null(removed);
        }

        [Fact]
        public async Task DeleteUserAsync_Throws_WhenMissing()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            var jwtService = new JwtService(new Mock<IConfiguration>().Object);
            var service = new UserService(context, jwtService, new NullLogger<UserService>());

            await Assert.ThrowsAsync<GameException>(async () =>
            {
                await service.DeleteUserAsync(999);
            });
        }
    }
}
