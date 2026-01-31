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

    /// <summary>
    /// Executes the background service for distributing passive income.
    /// </summary>
    /// <param name="stoppingToken">Cancellation token to stop the service.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MyWorker (Passive Income) started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Worker is running at: {time}", DateTimeOffset.Now);

                // Create a scope to get scoped services (DbContext and PassiveIncomeService)
                using (var scope = _serviceProvider.CreateScope())
                {
                    var passiveIncomeService = scope.ServiceProvider.GetRequiredService<PassiveIncomeService>();
                    await passiveIncomeService.DistributePassiveIncomeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in passive income distribution");
            }

            // Distribute passive income every 30 seconds
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        _logger.LogInformation("MyWorker (Passive Income) stopped");
    }
}
