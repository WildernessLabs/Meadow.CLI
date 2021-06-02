using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using Meadow.CLI.Commands;
using Meadow.CLI.Core.DeviceManagement;
using Meadow.CommandLine.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Meadow.CommandLine
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddLogging(
                builder =>
                {
                    var logLevel = LogLevel.Information;
                    var logModifier = args.FirstOrDefault(a => a.Contains("-v"))
                        ?.Count(x => x == 'v') ?? 0;

                    logLevel -= logModifier;
                    if (logLevel < 0)
                    {
                        logLevel = 0;
                    }
                    
                    Console.WriteLine($"Using log level {logLevel}");
                    builder.AddSimpleConsole(c =>
                    {
                        c.ColorBehavior = LoggerColorBehavior.Enabled;
                        c.SingleLine = true;
                        c.UseUtcTimestamp = true;
                    }).SetMinimumLevel(logLevel);
                });

            services.AddSingleton<MeadowDeviceManager>();
            AddCommandsAsServices(services);
            var serviceProvider = services.BuildServiceProvider();
            return await new CliApplicationBuilder().AddCommandsFromThisAssembly()
                                                    .UseTypeActivator(serviceProvider.GetService)
                                                    .Build()
                                                    .RunAsync();
        }

        private static void AddCommandsAsServices(IServiceCollection services)
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(Program));
            Trace.Assert(assembly != null);
            var types = assembly.GetTypes();

            var commands = types.Where(
                                       x => x.IsAssignableTo(typeof(MeadowSerialCommand))
                                         || x.IsAssignableTo(typeof(ICommand)))
                                   .Where(x => !x.IsAbstract);

            foreach (var command in commands)
            {
                services.AddTransient(command);
            }
        }
    }
}