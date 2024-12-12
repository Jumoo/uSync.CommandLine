using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using System.CommandLine.Builder;
using System.CommandLine.Parsing;

using Umbraco.Management.Api;

using uSync.Commands.Core.Commands;

namespace uSync;
internal static class BuildHelper
{

    public static Parser BuildParser(IServiceProvider serviceProvider)
    {
        var commandLineBuilder = new CommandLineBuilder();

        foreach (var command in serviceProvider.GetServices<ISyncCommand>())
        {
            commandLineBuilder.Command.Add(command.Command);
        }

        return commandLineBuilder.UseDefaults().Build();
    }

    public static ServiceProvider BuildServiceProvider(
        IEnumerable<Type> commands)
    {
        var services = new ServiceCollection();
        IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true)
            .Build();

        // services.AddHttpClient();

        services.AddSingleton<HttpClient>();
        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<TextWriter>(Console.Out);

        services.AddCommands(commands);

        return services.BuildServiceProvider();
    }

    private static IServiceCollection AddCommands(this IServiceCollection services, 
        IEnumerable<Type> commands)
    {
        foreach (var command in commands)
        {
            services.AddSingleton(typeof(ISyncCommand), command);
        }

        return services;
    }
}
