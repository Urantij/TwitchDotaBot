using System.Text;
using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;
using TwitchDotaBot.Twitch.Commands;
using TwitchLib.Api;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Predictions;
using TwitchLib.Api.Helix.Models.Predictions.CreatePrediction;
using Outcome = TwitchLib.Api.Helix.Models.Predictions.CreatePrediction.Outcome;

namespace TwitchDotaBot;

public class Worker : IHostedService
{
    private readonly DotaClient _dota;
    private readonly ChatBot _chatBot;
    private readonly SuperApi _api;
    private readonly DotaHeroes _heroes;
    private readonly AppConfig _appConfig;
    private readonly ILogger<Worker> _logger;

    public MatchModel? MatchModelToIgnore { get; set; }

    public MatchModel? CurrentMatch { get; private set; }
    public Prediction? CurrentPrediction { get; private set; }

    public Worker(DotaClient dota, ChatBot chatBot, SuperApi api, DotaHeroes heroes, IOptions<AppConfig> appOptions,
        ILogger<Worker> logger)
    {
        _dota = dota;
        _chatBot = chatBot;
        _api = api;
        _heroes = heroes;
        _appConfig = appOptions.Value;
        _logger = logger;

        _dota.NewMatchFound += DotaOnNewMatchFound;
        _dota.MatchClosed += DotaOnMatchClosed;
    }

    private void DotaOnNewMatchFound(MatchModel obj)
    {
        if (MatchModelToIgnore?.Id == obj.Id)
        {
            _logger.LogInformation("Пришёл новый матч, игнорируем.");
            return;
        }

        Task.Run(async () =>
        {
            if (CurrentPrediction != null && CurrentMatch?.MatchResult == MatchResult.None)
            {
                await _chatBot.Channel.SendMessageAsync($"Найден новый матч, но прогноз уже существует.");
                return;
            }

            try
            {
                await StartPredictionAsync(obj);
            }
            catch (TwitchLib.Api.Core.Exceptions.BadRequestException e)
            {
                await _chatBot.Channel.SendMessageAsync($"Найден новый матч, но не удалось создать прогноз на него.");

                try
                {
                    string message = await e.HttpResponse.Content.ReadAsStringAsync();

                    _logger.LogError("Ошибка при попытке запустить ставку. {message}", message);
                }
                catch
                {
                    _logger.LogError(e, "Ошибка при попытке запустить ставку. Сообщение не удалось загрузить.");
                }
            }
            catch (Exception e)
            {
                await _chatBot.Channel.SendMessageAsync($"Найден новый матч, но не удалось создать прогноз на него.");

                _logger.LogError(e, "Ошибка при попытке запустить ставку.");
            }
        });
    }

    private void DotaOnMatchClosed(MatchModel obj)
    {
        if (obj.Id != CurrentMatch?.Id)
            return;

        if (CurrentPrediction == null)
            return;

        CurrentMatch = obj;

        bool? win = DetermineWin(obj, _appConfig.SteamId);

        Prediction thatPrediction = CurrentPrediction;
        MatchModel thatMatch = CurrentMatch;
        Task.Run(async () =>
        {
            try
            {
                await ClosePredictionAsync(win, thatPrediction);
            }
            catch (TwitchLib.Api.Core.Exceptions.BadRequestException e)
            {
                try
                {
                    string message = await e.HttpResponse.Content.ReadAsStringAsync();

                    _logger.LogError("Ошибка при попытке закрыть ставку. {message}", message);

                    // if (message.Contains("prediction event has already ended"))
                    // {
                    //     await _chatBot.Channel.SendMessageAsync("Не удалось закрыть прогноз - он уже завершён.");
                    // }
                }
                catch
                {
                    _logger.LogError(e, "Ошибка при попытке закрыть ставку. Сообщение не удалось загрузить.");
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Ошибка при попытке закрыть ставку.");
            }

            if (thatPrediction == CurrentPrediction)
                CurrentPrediction = null;
        });
    }

    public async Task StartPredictionAsync(MatchModel? match = null)
    {
        _logger.LogInformation("Запускаем ставку.");

        TwitchAPI api = await _api.GetApiAsync();

        string? heroName = null;
        // ну по приколу чисто заюзал
        if (match?.Players?.FirstOrDefault(p => p.SteamId == _appConfig.SteamId)?.HeroId is { } heroId)
        {
            heroName = _heroes.GerHeroName(heroId);
        }

        string title;
        TimeSpan? passed = DateTime.UtcNow - match?.GameDate;
        if (passed > TimeSpan.FromMinutes(5))
        {
            title = $"Победа в игре дота2? ({passed.Value.TotalMinutes:F0}м назад)";
        }
        else
        {
            title = "Победа в игре дота2?";
        }

        const int titlesLimit = 45;

        if (heroName != null)
        {
            if (title.Length + heroName.Length + 1 > titlesLimit)
            {
                int diff = (title.Length + heroName.Length + 1) - titlesLimit;

                if (diff >= heroName.Length + 1)
                {
                    heroName = null;
                }
                else
                {
                    heroName = heroName.Substring(0, heroName.Length - diff);
                }
            }

            if (heroName != null)
            {
                title += $" {heroName}";
            }
        }

        CreatePredictionResponse response = await api.Helix.Predictions.CreatePredictionAsync(
            new CreatePredictionRequest()
            {
                BroadcasterId = _appConfig.TwitchId, Title = title,
                PredictionWindowSeconds = _appConfig.PredictionTimeWindow,
                Outcomes = [new Outcome { Title = "Да" }, new Outcome { Title = "Нет" }]
            });

        CurrentPrediction = response.Data[0];

        CurrentMatch = match;

        await _chatBot.Channel.SendMessageAsync("Запустил ставку.");

        _logger.LogInformation("Запустил ставку {id}", CurrentPrediction.Id);
    }

    public async Task ClosePredictionAsync(bool? win, Prediction prediction)
    {
        _logger.LogInformation("Закрываем ставку. {id}", prediction.Id);

        TwitchAPI api = await _api.GetApiAsync();

        if (win == null)
        {
            await api.Helix.Predictions.EndPredictionAsync(_appConfig.TwitchId, prediction.Id,
                PredictionEndStatus.CANCELED);

            await _chatBot.Channel.SendMessageAsync("Отменил ставку.");

            CurrentPrediction = null;
            _logger.LogInformation("Отменил ставку {id}", prediction.Id);
            return;
        }

        string targetTitle = win == true ? "Да" : "Нет";
        TwitchLib.Api.Helix.Models.Predictions.Outcome? targetOutcome =
            prediction.Outcomes.FirstOrDefault(p => p.Title == targetTitle);

        if (targetOutcome == null)
            return;

        await api.Helix.Predictions.EndPredictionAsync(_appConfig.TwitchId, prediction.Id,
            PredictionEndStatus.RESOLVED, targetOutcome.Id);

        CurrentPrediction = null;

        int wins = 0;
        int loses = 0;
        bool? isStreakWin = null;
        try
        {
            (wins, loses, _, isStreakWin) = await StatCommand.LoadMatchesStatsAsync(_dota, _appConfig.SteamId);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Не удалось стату за стрим подгрузить.");
        }

        // xd
        StringBuilder sb = new();
        if (win == true)
        {
            string phrase;

            if (isStreakWin == true)
            {
                phrase = Random.Shared.GetItems([
                    "Очередная победа",
                    "Ещё одна победа",
                    "Очередной вин",
                    "Ещё один вин",
                ], 1)[0];
            }
            else
            {
                phrase = Random.Shared.GetItems([
                    "Победа",
                    "Вин",
                ], 1)[0];
            }

            sb.Append(phrase);
            sb.Append(" EZ");
        }
        else
        {
            string phrase;

            if (isStreakWin == false)
            {
                phrase = Random.Shared.GetItems([
                    "Очередное поражение",
                    "Ещё одно поражение",
                    "Очередной луз",
                    "Ещё один луз",
                ], 1)[0];
            }
            else
            {
                phrase = Random.Shared.GetItems([
                    "Поражение",
                    "Луз",
                ], 1)[0];
            }

            sb.Append(phrase);
            sb.Append(" Sadge");
        }

        if (wins != 0 || loses != 0)
        {
            sb.Append($" {wins}-{loses}");
        }

        await _chatBot.Channel.SendMessageAsync(sb.ToString());

        _logger.LogInformation("Закрыл ставку {id}", prediction.Id);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Запускается работник");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _dota.NewMatchFound -= DotaOnNewMatchFound;
        _dota.MatchClosed -= DotaOnMatchClosed;

        return Task.CompletedTask;
    }

    // вот бы хелпер сделать дааа
    public static bool? DetermineWin(MatchModel match, ulong id)
    {
        if (match.MatchResult == MatchResult.Finished && match.DetailsInfo?.RadiantWin != null)
        {
            bool? isRadiant = match.Players?.FirstOrDefault(p => p.SteamId == id)?.TeamNumber == 0;

            if (isRadiant == null)
            {
                return null;
            }

            return match.DetailsInfo.RadiantWin == isRadiant;
        }
        else
        {
            return null;
        }
    }
}