using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CliFx;
using Meadow.CommandLine.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace Meadow.CommandLine
{
    public class Program
    {
        public static async Task<int> Main()
        {
            var services = new ServiceCollection();
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