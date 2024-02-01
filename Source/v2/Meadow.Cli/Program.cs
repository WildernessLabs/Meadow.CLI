using CliFx;
using Meadow.CLI;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Cloud;
using Meadow.Cloud.Identity;
using Meadow.Software;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System.Diagnostics;

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
                builder.AddSerilog(Log.Logger, dispose: true);
            });

        services.AddSingleton<MeadowConnectionManager>();
        services.AddSingleton<FileManager>();
        services.AddSingleton<ISettingsManager, SettingsManager>();
        services.AddSingleton<IPackageManager, PackageManager>();

        services.AddSingleton<UserService>();
        services.AddSingleton<DeviceService>();
        services.AddSingleton<CollectionService>();
        services.AddSingleton<CommandService>();
        services.AddSingleton<PackageService>();
        services.AddSingleton<ApiTokenService>();
        services.AddSingleton<IdentityManager, IdentityManager>();

        if (File.Exists("appsettings.json"))
        {
            var config = new ConfigurationBuilder()
                                   .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                                   .AddJsonFile("appsettings.json")
                                   .Build();

            services.AddScoped<IConfiguration>(_ => config);
        }
        else
        {
            services.AddScoped<IConfiguration>(_ => null);
        }

        AddCommandsAsServices(services);

        var serviceProvider = services.BuildServiceProvider();

        try
        {
            await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .UseTypeActivator(serviceProvider.GetService!)
                .SetExecutableName("meadow")
                .Build()
                .RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Operation failed: {ex.Message}");
#if DEBUG
            throw; //debug spew for debug builds
#endif
        }

        Environment.Exit(0);
        return 0;
    }

    private static void AddCommandsAsServices(IServiceCollection services)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly(); //.GetAssembly(typeof(Program));
        Trace.Assert(assembly != null);
        var types = assembly?.GetTypes();

        var commands = types?.Where(x => x.IsAssignableTo(typeof(ICommand)))
                            .Where(x => !x.IsAbstract);

        if (commands != null)
        {
            foreach (var command in commands)
            {
                services.AddTransient(command);
            }
        }
    }
}