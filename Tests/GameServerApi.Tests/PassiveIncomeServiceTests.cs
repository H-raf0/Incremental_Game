using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using GameServerApi.Services;
using GameServerApi.Models;

namespace GameServerApi.Tests
{
    public class PassiveIncomeServiceTests
    {
        private static ApplicationDbContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task DistributePassiveIncome_IncrementsAllProgressions()
        {
            var context = CreateContext(Guid.NewGuid().ToString());
            context.Progressions.Add(new Progression(1) { Count = 5 });
            context.Progressions.Add(new Progression(2) { Count = 3 });
            await context.SaveChangesAsync();

            var updated = await PassiveIncomeService.DistributePassiveIncomeAsync(context, CancellationToken.None);

            Assert.Equal(2, updated);

            var p1 = await context.Progressions.FirstAsync(p => p.UserId == 1);
            var p2 = await context.Progressions.FirstAsync(p => p.UserId == 2);

            Assert.Equal(6, p1.Count);
            Assert.Equal(4, p2.Count);
        }
    }
}
