using CarInsurance.Api.Services;

namespace CarInsurance.Api.BackgroundServices;

public class PolicyExpirationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PolicyExpirationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);

    public PolicyExpirationBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<PolicyExpirationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Policy Expiration Background Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var policyExpirationService = scope.ServiceProvider.GetRequiredService<PolicyExpirationService>();
                
                await policyExpirationService.ProcessExpiredPoliciesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing expired policies");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Policy Expiration Background Service stopping");
    }
}