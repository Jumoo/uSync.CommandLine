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

        // uSync handlers (content and media especially) can take well over the
        // default 100 seconds on a big site, so give each request longer.
        // (set "uSync:Command:Timeout" in appsettings.json, e.g. "01:00:00", to change it)
        var timeout = config.GetValue("uSync:Command:Timeout", TimeSpan.FromMinutes(30));
        services.AddSingleton(new HttpClient { Timeout = timeout });
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
