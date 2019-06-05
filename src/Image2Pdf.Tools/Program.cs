using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Image2Pdf.Tools.Services;

namespace Image2Pdf.Tools {
    class Program {
        static async Task Main(string[] args) {
            var collection = new ServiceCollection();
            collection.AddLogging(options => options.AddConsole());

            collection.AddSingleton<TiffConverter>();
            collection.AddSingleton<PathService>(new PathService("/usr/local/bin", ".temp", ".image"));
            collection.AddSingleton<Quality>(new Quality { });
            collection.AddSingleton<CommandProcessor>();
            collection.AddSingleton<ConvertService>();
            collection.AddSingleton<MainService>();

            var provider = collection.BuildServiceProvider();
            var service = provider.GetService<MainService>();

            service.Start();

            await Task.CompletedTask;
        }
    }
}