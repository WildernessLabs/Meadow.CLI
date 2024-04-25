using CliFx;
using Meadow.CLI;
using Meadow.CLI.Commands.DeviceManagement;
using Meadow.Cloud.Client;
using Meadow.Cloud.Client.Identity;
using Meadow.Package;
using Meadow.Software;
using Meadow.Telemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SystemConsole.Themes;
using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Meadow.SoftwareManager")]

public class Program
{
    public static async Task<int> Main(string[] _)
    {
        var outputTemplate = "{Message:lj}{NewLine}{Exception}";
        Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose()
                                              .WriteTo.Console(LogEventLevel.Information, outputTemplate,
                                                                theme: ConsoleTheme.None)
                                              .CreateLogger();

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
        services.AddSingleton<IdentityManager>();
        services.AddSingleton<JsonDocumentBindingConverter>();
        services.AddSingleton<IMeadowCloudClient, MeadowCloudClient>();
        services.AddSingleton(MeadowCloudUserAgent.Cli);

        services.AddHttpClient<MeadowCloudClient>();

        // Required to disable console logging of HttpClient
        services.RemoveAll<IHttpMessageHandlerBuilderFilter>();

        if (File.Exists("appsettings.json"))
        {
            var config = new ConfigurationBuilder()
                                   .SetBasePath(Directory.GetCurrentDirectory())
                                   .AddJsonFile("appsettings.json")
                                   .Build();

            services.AddScoped<IConfiguration>(_ => config);
        }
        else
        {
            services.AddScoped<IConfiguration>(_ => null!);
        }

        AddCommandsAsServices(services);

        var serviceProvider = services.BuildServiceProvider();

        int returnCode;

        try
        {
            returnCode = await new CliApplicationBuilder()
                .AddCommandsFromThisAssembly()
                .UseTypeActivator(serviceProvider.GetService!)
                .SetExecutableName("meadow")
                .Build()
                .RunAsync();
        }
        catch (CommandException ce)
        {
            returnCode = ce.ExitCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Operation failed: {ex.Message}");
            returnCode = 1;
        }
        finally
        {
            MeadowTelemetry.Current.Dispose();
        }

        return returnCode;
    }

    private static void AddCommandsAsServices(IServiceCollection services)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
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