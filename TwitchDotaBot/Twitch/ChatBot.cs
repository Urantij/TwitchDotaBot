using Microsoft.Extensions.Options;
using TwitchSimpleLib.Chat;

namespace TwitchDotaBot.Twitch;

public class ChatBotConfig
{
    public required string Username { get; init; }
    public required string Token { get; init; }
}

public class ChatBot : IHostedService
{
    private readonly ILogger _logger;

    public TwitchChatClient Client { get; init; }

    public ChatAutoChannel Channel { get; init; }

    public ChatBot(IOptions<AppConfig> appOptions, IOptions<ChatBotConfig> chatOptions,
        IHostApplicationLifetime applicationLifetime, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(this.GetType());

        Client = new TwitchChatClient(true,
            new TwitchChatClientOpts(chatOptions.Value.Username, chatOptions.Value.Token), loggerFactory,
            applicationLifetime.ApplicationStopping);

        Client.AuthFailed += AuthFailed;

        Channel = Client.AddAutoJoinChannel(appOptions.Value.TwitchUsername);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Client.ConnectAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Client.Close();

        return Task.CompletedTask;
    }

    private void AuthFailed(object? sender, EventArgs e)
    {
        _logger.LogCritical("AuthFailed");
    }
}