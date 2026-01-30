using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;

namespace GameServerApi.Services;

public class PassiveIncomeService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PassiveIncomeService> _logger;

    public PassiveIncomeService(IServiceProvider serviceProvider, ILogger<PassiveIncomeService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // Permet de distribuer le revenu passif à tous les utilisateurs (testable)
    public static async Task<int> DistributePassiveIncomeAsync(ApplicationDbContext dbContext, CancellationToken cancellationToken)
    {
        // Récupérer toutes les progressions
        var progressions = await dbContext.Progressions.ToListAsync(cancellationToken);

        // Ajouter 1 point au score de chaque utilisateur
        foreach (var progression in progressions)
        {
            progression.Count += 1;
        }

        // Sauvegarder les modifications si nécessaire
        if (progressions.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return progressions.Count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PassiveIncomeService démarré.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Création d'un scope manuel pour accéder au DbContext
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                    // Utiliser la méthode testable extraite pour distribuer le revenu passif
                    var updatedCount = await DistributePassiveIncomeAsync(dbContext, stoppingToken);
                    if (updatedCount > 0)
                    {
                        _logger.LogInformation("Revenu passif distribué : +1 point à {UserCount} utilisateur(s)", updatedCount);
                    }
                }

                // Attendre 30 secondes avant la prochaine distribution
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (Exception ex)
            {
                // Si c'est une annulation, quitter proprement sans enregistrer comme erreur
                if (ex is OperationCanceledException || stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("PassiveIncomeService cancellation requested, stopping service.");
                    break;
                }

                _logger.LogError(ex, "Erreur lors de la distribution du revenu passif");
                // Attendre avant de réessayer ; si l'annulation arrive pendant l'attente, sortir proprement
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("PassiveIncomeService cancellation during delay, stopping service.");
                    break;
                }
            }
        }

        _logger.LogInformation("PassiveIncomeService arrêté.");
    }
}
