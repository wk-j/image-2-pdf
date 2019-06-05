using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Image2Pdf.Tools {
    public class LifetimeEventsHostedService : IHostedService {
        private readonly ILogger<LifetimeEventsHostedService> _logger;

        public LifetimeEventsHostedService(ILogger<LifetimeEventsHostedService> logger) {
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("LifetimeEventsHostedService ...");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("Stop ...");
            return Task.CompletedTask;
        }
    }
}
