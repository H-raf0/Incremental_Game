namespace GameServerApi;

using GameServerApi.Models;
using GameServerApi.Services;
using Microsoft.EntityFrameworkCore;

public class MyWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MyWorker> _logger;

    public MyWorker(IServiceProvider serviceProvider, ILogger<MyWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MyWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    // Get the database context and passive income service from the scope
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var passiveIncomeService = scope.ServiceProvider.GetRequiredService<PassiveIncomeService>();

                    // Get all users from the database
                    var users = await context.Users.ToListAsync();

                    // Apply passive income for each user
                    foreach (var user in users)
                    {
                        await passiveIncomeService.ApplyPassiveIncomeAsync(user.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating passive income");
            }

            // Run calculation every 5 seconds
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }

        _logger.LogInformation("MyWorker stopping...");
    }
}
