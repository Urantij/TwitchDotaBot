using TwitchDotaBot.Dota;
using TwitchDotaBot.Job;
using TwitchDotaBot.Twitch;

namespace TwitchDotaBot;

public class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(b => { b.TimestampFormat = "[HH:mm:ss] "; });

        // https://github.com/dotnet/runtime/issues/95006
        {
            CancerConfigLoader bind = CancerConfigLoader.Load();

            builder.Services.AddCancerOptions<AppConfig>("App", bind);
            builder.Services.AddCancerOptions<TwitchApiConfig>("TwitchApi", bind);
            builder.Services.AddCancerOptions<ChatBotConfig>("TwitchChat", bind);
            builder.Services.AddCancerOptions<DotaHeroesConfig>("DotaHeroes", bind);
            builder.Services.AddCancerOptions<DotaConfig>("Dota", bind);
            builder.Services.AddCancerOptions<MedusaShameConfig>("Shame", bind);
        }

        builder.Services.AddSingleton<SuperApi>();

        builder.Services.AddSingleton<ChatBot>();
        builder.Services.AddHostedService<ChatBot>(p => p.GetRequiredService<ChatBot>());

        builder.Services.AddSingleton<DotaHeroes>();
        builder.Services.AddHostedService<DotaHeroes>(p => p.GetRequiredService<DotaHeroes>());

        builder.Services.AddSingleton<DotaClient>();
        builder.Services.AddHostedService<DotaClient>(p => p.GetRequiredService<DotaClient>());

        builder.Services.AddSingleton<Commander>();
        builder.Services.AddHostedService<Commander>(p => p.GetRequiredService<Commander>());

        builder.Services.AddSingleton<Worker>();
        builder.Services.AddHostedService<Worker>(p => p.GetRequiredService<Worker>());

        builder.Services.AddSingleton<MedusaShame>();
        builder.Services.AddHostedService<MedusaShame>(p => p.GetRequiredService<MedusaShame>());

        builder.Services.AddSingleton<LastikiAnnouncer>();
        builder.Services.AddHostedService<LastikiAnnouncer>(p => p.GetRequiredService<LastikiAnnouncer>());

        IHost host = builder.Build();

        {
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Запускаю...");
        }

        host.Run();
    }
}