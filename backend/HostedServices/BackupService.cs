using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SwAIvyn.HostedServices
{
    public class BackupService : BackgroundService
    {
        private readonly ILogger<BackupService> _logger;
        private readonly TimeSpan _period = TimeSpan.FromHours(24);

        public BackupService(ILogger<BackupService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_period);
            
            while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await PerformBackupAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while performing backup");
                }
            }
        }

        private async Task PerformBackupAsync()
        {
            _logger.LogInformation("Performing scheduled backup at {time}", DateTimeOffset.Now);
            
            // Placeholder for backup logic
            await Task.Delay(1000);
            
            _logger.LogInformation("Backup completed successfully at {time}", DateTimeOffset.Now);
        }
    }
}
