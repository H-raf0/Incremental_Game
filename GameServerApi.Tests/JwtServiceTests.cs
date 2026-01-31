using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;
using GameServerApi.Models;

namespace GameServerApi.Tests
{
    public class JwtServiceTests
    {
        [Fact]
        public void GenerateToken_IncludesExpectedClaims()
        {
            var config = new ConfigurationBuilder().Build();
            var service = new JwtService(config);
            var user = new User("tester", "password", Role.ADMIN) { Id = 42 };

            var token = service.GenerateToken(user);

            Assert.False(string.IsNullOrWhiteSpace(token));

            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(token);

            Assert.Equal("localhost:5000", jwt.Issuer);
            Assert.Contains("localhost:5000", jwt.Audiences);

            var nameId = jwt.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier).Value;
            var name = jwt.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.Name).Value;
            var role = jwt.Claims.First(c => c.Type == System.Security.Claims.ClaimTypes.Role).Value;

            Assert.Equal("42", nameId);
            Assert.Equal("tester", name);
            Assert.Equal("Admin", role);
        }
    }
}
