using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using Meadow.CLI.Commands;
using Meadow.CLI.Core.DeviceManagement;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace Meadow.CLI
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var logLevel = LogEventLevel.Information;
            var logModifier = args.FirstOrDefault(a => a.Contains("-v"))
                                  ?.Count(x => x == 'v') ?? 0;

            logLevel -= logModifier;
            if (logLevel < 0)
            {
                logLevel = 0;
            }

            var outputTemplate = logLevel == LogEventLevel.Verbose
                                     ? "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                                     : "{Message:lj}{NewLine}{Exception}";
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose()
                                                  .WriteTo.Console(
                                                      logLevel,
                                                      outputTemplate)
                                                  .CreateLogger();
            Console.WriteLine($"Using log level {logLevel}");
            var services = new ServiceCollection();
            services.AddLogging(
                builder =>
                {
                    builder.AddSerilog(Log.Logger, dispose:true);
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
                                            || x.IsAssignableTo(typeof(MeadowCommand)))
                                   .Where(x => !x.IsAbstract);

            foreach (var command in commands)
            {
                services.AddTransient(command);
            }
        }
    }
}