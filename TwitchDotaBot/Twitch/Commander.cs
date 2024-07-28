using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch;

public class Commander : IHostedService
{
    private readonly ChatBot _chatBot;
    private readonly ILogger<Commander> _logger;

    public Commander(ChatBot chatBot, ILogger<Commander> logger)
    {
        _chatBot = chatBot;
        _logger = logger;
    }
    
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _chatBot.Channel.PrivateMessageReceived += ChannelOnPrivateMessageReceived;
        
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _chatBot.Channel.PrivateMessageReceived -= ChannelOnPrivateMessageReceived;
        
        return Task.CompletedTask;
    }

    private void ChannelOnPrivateMessageReceived(object? sender, TwitchPrivateMessage e)
    {
    }
}