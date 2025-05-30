using SwAIvyn.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SwAIvyn.Services;

/// <summary>
/// Background service that performs nightly reconciliation between SQLite, Neo4j, and Weaviate.
/// Ensures data consistency across the three-database harmony architecture.
/// </summary>
public class TripleStoreReconcileJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TripleStoreReconcileJob> _logger;
    private readonly TimeSpan _reconciliationInterval;

    public TripleStoreReconcileJob(
        IServiceProvider serviceProvider,
        ILogger<TripleStoreReconcileJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        // Run reconciliation every 24 hours at 2 AM
        _reconciliationInterval = TimeSpan.FromHours(24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TripleStoreReconcileJob started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Calculate delay until next 2 AM
                var now = DateTime.Now;
                var next2AM = DateTime.Today.AddDays(1).AddHours(2);
                
                // If it's past 2 AM today and we haven't run yet, run at next 2 AM
                if (now.Hour < 2)
                {
                    next2AM = DateTime.Today.AddHours(2);
                }

                var delayUntilNext2AM = next2AM - now;
                
                _logger.LogInformation("Next reconciliation scheduled for: {NextRun} (in {Delay})", 
                    next2AM, delayUntilNext2AM);

                // Wait until 2 AM or cancellation
                await Task.Delay(delayUntilNext2AM, stoppingToken);

                if (stoppingToken.IsCancellationRequested)
                    break;

                // Perform reconciliation
                await PerformReconciliation();

                // After reconciliation, wait for the full interval before next check
                await Task.Delay(_reconciliationInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("TripleStoreReconcileJob cancellation requested");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TripleStoreReconcileJob execution loop");
                
                // Wait 1 hour before retrying if there's an error
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }

        _logger.LogInformation("TripleStoreReconcileJob stopped");
    }

    private async Task PerformReconciliation()
    {
        try
        {
            _logger.LogInformation("Starting nightly triple-store reconciliation");

            using var scope = _serviceProvider.CreateScope();
            var memoryService = scope.ServiceProvider.GetRequiredService<IMemoryService>();

            var report = await memoryService.ReconcileMemoriesAsync();

if (report.InconsistenciesFound == 0)
            {
                _logger.LogInformation("Reconciliation completed successfully. Total memories checked: {Total}, Inconsistencies found: {Found}, Fixed: {Fixed}, Duration: {Duration}ms",
                    report.TotalMemoriesChecked,
report.InconsistenciesFound,
                    report.InconsistenciesFixed,
                    report.Duration.TotalMilliseconds);
            }
            else
            {
                _logger.LogWarning("Reconciliation completed with inconsistencies. Fixed: {Fixed}, Errors: {ErrorCount}",
                    report.InconsistenciesFixed,
                    report.Errors.Count);

                foreach (var error in report.Errors)
                {
                    _logger.LogError("Reconciliation error: {Error}", error);
                }

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to perform nightly reconciliation");
        }
    }

    public override async Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TripleStoreReconcileJob is stopping");
        await base.StopAsync(stoppingToken);
    }
}
