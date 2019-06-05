using Microsoft.Extensions.Logging;

namespace Image2Pdf.Tools.Services {
    public class MainService {

        private ILogger<MainService> _logger;
        private ConvertService _convert;

        public MainService(ILogger<MainService> logger, ConvertService convert) {
            _logger = logger;
            _convert = convert;
        }

        public void Start() {
            _logger.LogInformation("AAA ...");
        }
    }
}