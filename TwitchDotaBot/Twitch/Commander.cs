using Microsoft.Extensions.Options;
using TwitchDotaBot.Twitch.Commands;
using TwitchSimpleLib.Chat.Messages;

namespace TwitchDotaBot.Twitch;

public class Commander : IHostedService
{
    private readonly BaseCommand[] _commands =
    [
        new StatCommand()
        {
            Triggers =
            [
                "победики",
                "лузики"
            ],
            LiteralTriggers =
            [
                "!wl"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            ModsOnly = false
        },
        new LastCommand()
        {
            Triggers =
            [
                "ласт"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            ModsOnly = false
        },
        new AverageCommand()
        {
            Triggers =
            [
                "авг",
                "аверага"
            ],
            LiteralTriggers =
            [
                "!avg"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            ModsOnly = false
        },
        new CancelPredictionCommand()
        {
            Triggers =
            [
                "отмена"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            // MainVillainOnly = true
        },
        new CancelPredictionExtremeCommand()
        {
            Triggers =
            [
                "суперотмена"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            MainVillainOnly = true
        },
        new TestPredictionCommand()
        {
            Triggers =
            [
                "тестпрогноз"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            MainVillainOnly = true
        },
        new CreateMatchPredictionCommand()
        {
            Triggers =
            [
                "ставка",
                "дота",
                "прогноз",
                "сделай"
            ],
            Cooldown = TimeSpan.FromSeconds(30)
        },
        new ClosePredictionCommand()
        {
            Triggers =
            [
                "закрыть"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            // MainVillainOnly = true
        },
        new PrevPlayersCommand()
        {
            Triggers =
            [
                "стримснайперы",
                "ласты",
                "ластики",
                "прошлые"
            ],
            Cooldown = TimeSpan.FromSeconds(30),
            ModsOnly = false,
        },
        new WinrateAgainstCommand()
        {
            Triggers =
            [
                "противостояния",
                "против",
            ],
            Cooldown = TimeSpan.FromMinutes(5)
        },
        new WinrateTogetherCommand()
        {
            Triggers =
            [
                "вместестояния",
                "вместе",
            ],
            Cooldown = TimeSpan.FromMinutes(5)
        }
    ];

    private readonly ChatBot _chatBot;
    private readonly AppConfig _config;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<Commander> _logger;
    private readonly IServiceScope _scope;

    private const string Prefix = "=";

    public Commander(ChatBot chatBot, IServiceScopeFactory scopeFactory, IOptions<AppConfig> options,
        IHostApplicationLifetime lifetime,
        ILogger<Commander> logger)
    {
        _chatBot = chatBot;
        _config = options.Value;
        _lifetime = lifetime;
        _logger = logger;
        _scope = scopeFactory.CreateScope();
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
        BaseCommand? targetCommand;

        string[] args;
        if (e.text.StartsWith(Prefix))
        {
            string[] split = e.text.Split(' ');

            if (split.Length == 0)
                return;

            string command = split[0][Prefix.Length..].ToLower();
            args = split.Skip(1).ToArray();

            targetCommand = _commands.FirstOrDefault(c => c.Triggers.Contains(command));

            if (targetCommand == null)
                return;
        }
        else
        {
            string literalTrigger = e.text.Split(' ', 2)[0].ToLower();

            targetCommand = _commands.FirstOrDefault(c => c.LiteralTriggers.Contains(literalTrigger));

            if (targetCommand == null)
                return;

            args = e.text.Split(' ').Skip(1).ToArray();
        }

        bool isMod = e.mod || e.badges.ContainsKey("broadcaster");
        bool isMainVillain = e.username.Equals(_config.MainVillainName, StringComparison.OrdinalIgnoreCase);

        if (targetCommand.MainVillainOnly)
        {
            if (!isMainVillain)
                return;
        }
        else if (targetCommand.ModsOnly && !isMod)
            return;

        if (!isMod && !isMainVillain)
        {
            TimeSpan? passed = DateTimeOffset.UtcNow - targetCommand.LastUse;

            if (passed <= targetCommand.Cooldown)
                return;
        }

        targetCommand.LastUse = DateTimeOffset.UtcNow;

        Task.Run(async () =>
        {
            try
            {
                await targetCommand.DoAsync(_scope.ServiceProvider, args, e,
                    cancellationToken: _lifetime.ApplicationStopping);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Ошибка при выполнении команды {command}", targetCommand.GetType().Name);
            }
        });
    }
}