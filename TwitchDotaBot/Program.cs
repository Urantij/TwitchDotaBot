using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;

namespace TwitchDotaBot;

public class Program
{
    public static void Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(b =>
        {
            b.TimestampFormat = "[HH:mm:ss] ";
        });

        builder.Services.AddOptions<AppConfig>()
            .Bind(builder.Configuration.GetSection("App"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<TwitchApiConfig>()
            .Bind(builder.Configuration.GetSection("TwitchApi"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<ChatBotConfig>()
            .Bind(builder.Configuration.GetSection("TwitchChat"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddOptions<DotaConfig>()
            .Bind(builder.Configuration.GetSection("Dota"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        
        builder.Services.AddSingleton<SuperApi>();

        builder.Services.AddSingleton<ChatBot>();
        builder.Services.AddHostedService<ChatBot>(p => p.GetRequiredService<ChatBot>());
        
        builder.Services.AddSingleton<DotaClient>();
        builder.Services.AddHostedService<DotaClient>(p => p.GetRequiredService<DotaClient>());
        
        builder.Services.AddSingleton<Worker>();
        builder.Services.AddHostedService<Worker>(p => p.GetRequiredService<Worker>());

        IHost host = builder.Build();
        host.Run();
    }
}