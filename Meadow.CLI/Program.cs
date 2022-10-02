﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using Meadow.CLI.Commands;
using Meadow.CLI.Core;
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
            var logModifier = args.FirstOrDefault(a => a.Contains("-m"))
                                  ?.Count(x => x == 'm') ?? 0;

            logLevel -= logModifier;
            if (logLevel < 0)
            {
                logLevel = 0;
            }

            var outputTemplate = logLevel == LogEventLevel.Verbose
                                     ? "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                                     : "{Message:lj}{NewLine}{Exception}";
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose()
                                                  .WriteTo.Console(logLevel, outputTemplate)
                                                  .CreateLogger();

            // Log that we're using a log level other than default of Information
            if (logLevel != LogEventLevel.Information)
            {
                Console.WriteLine($"Using log level {logLevel}");
            }

            var services = new ServiceCollection();
            services.AddLogging(
                builder =>
                {
                    builder.AddSerilog(Log.Logger, dispose:true);
                });

            services.AddSingleton<MeadowDeviceManager>();
            services.AddSingleton<DownloadManager>();
            AddCommandsAsServices(services);
            var serviceProvider = services.BuildServiceProvider();

            try
            {
                await new CliApplicationBuilder().AddCommandsFromThisAssembly()
                                                                    .UseTypeActivator(serviceProvider.GetService)
                                                                    .SetExecutableName("meadow")
                                                                    .Build()
                                                                    .RunAsync();
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Operation failed: {ex.Message}");
#if DEBUG
                throw; //debug spew for debug builds
#endif
            }

            Console.WriteLine("Done!");

            Environment.Exit(0);
            return 0;
        }

        private static void AddCommandsAsServices(IServiceCollection services)
        {
            var assembly = System.Reflection.Assembly.GetAssembly(typeof(Program));
            Trace.Assert(assembly != null);
            var types = assembly.GetTypes();

            var commands = types.Where(
                                       x => x.IsAssignableTo(typeof(MeadowSerialCommand))
                                            || x.IsAssignableTo(typeof(MeadowCommand))
                                            || x.IsAssignableTo(typeof(ICommand)))
                                   .Where(x => !x.IsAbstract);

            foreach (var command in commands)
            {
                services.AddTransient(command);
            }
        }
    }
}