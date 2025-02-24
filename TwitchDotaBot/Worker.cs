using Dota2Dispenser.Shared.Consts;
using Dota2Dispenser.Shared.Models;
using Microsoft.Extensions.Options;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;
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
    private readonly AppConfig _appConfig;
    private readonly ILogger<Worker> _logger;

    public MatchModel? CurrentMatch { get; private set; }
    public Prediction? CurrentPrediction { get; private set; }

    public Worker(DotaClient dota, ChatBot chatBot, SuperApi api, IOptions<AppConfig> appOptions,
        ILogger<Worker> logger)
    {
        _dota = dota;
        _chatBot = chatBot;
        _api = api;
        _appConfig = appOptions.Value;
        _logger = logger;

        _dota.NewMatchFound += DotaOnNewMatchFound;
        _dota.MatchClosed += DotaOnMatchClosed;
    }

    private void DotaOnNewMatchFound(MatchModel obj)
    {
        if (CurrentPrediction != null && CurrentMatch?.MatchResult == MatchResult.None)
            return;

        CurrentMatch = obj;

        Task.Run(async () =>
        {
            try
            {
                await StartPredictionAsync();
            }
            catch (TwitchLib.Api.Core.Exceptions.BadRequestException e)
            {
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

        bool? win;
        if (obj.MatchResult == MatchResult.Finished)
        {
            bool? isRadiant = obj.Players?.FirstOrDefault(p => p.SteamId == _appConfig.SteamId)?.TeamNumber == 0;

            if (isRadiant == null)
            {
                win = null;
            }
            else
            {
                win = obj.DetailsInfo?.RadiantWin == isRadiant;
            }
        }
        else
        {
            win = null;
        }

        Task.Run(async () =>
        {
            try
            {
                await ClosePredictionAsync(win, CurrentPrediction);
            }
            catch (TwitchLib.Api.Core.Exceptions.BadRequestException e)
            {
                try
                {
                    string message = await e.HttpResponse.Content.ReadAsStringAsync();

                    _logger.LogError("Ошибка при попытке закрыть ставку. {message}", message);
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
        });
    }

    public async Task StartPredictionAsync()
    {
        _logger.LogInformation("Запускаем ставку.");

        TwitchAPI api = await _api.GetApiAsync();

        string title;

        TimeSpan? passed = DateTime.UtcNow - CurrentMatch?.GameDate;
        if (passed > TimeSpan.FromMinutes(5))
        {
            title = $"Победа в игре дота2? ({passed.Value.TotalMinutes:F0} минут назад)";
        }
        else
        {
            title = "Победа в игре дота2?";
        }

        CreatePredictionResponse? response = await api.Helix.Predictions.CreatePredictionAsync(
            new CreatePredictionRequest()
            {
                BroadcasterId = _appConfig.TwitchId, Title = title,
                PredictionWindowSeconds = _appConfig.PredictionTimeWindow,
                Outcomes = [new Outcome { Title = "Да" }, new Outcome { Title = "Нет" }]
            });

        CurrentPrediction = response.Data[0];

        await _chatBot.Channel.SendMessageAsync("Запустил ставку.");

        _logger.LogInformation("Запустил ставку {id}", CurrentPrediction.Id);
    }

    public async Task ClosePredictionAsync(bool? win, Prediction prediction)
    {
        _logger.LogInformation("Закрываем ставку.");

        TwitchAPI api = await _api.GetApiAsync();

        if (win == null)
        {
            await api.Helix.Predictions.EndPredictionAsync(_appConfig.TwitchId, prediction.Id,
                PredictionEndStatus.CANCELED);

            await _chatBot.Channel.SendMessageAsync("Отменил ставку.");
            return;
        }

        string targetTitle = win == true ? "Да" : "Нет";
        TwitchLib.Api.Helix.Models.Predictions.Outcome? targetOutcome =
            prediction.Outcomes.FirstOrDefault(p => p.Title == targetTitle);

        if (targetOutcome == null)
            return;

        await api.Helix.Predictions.EndPredictionAsync(_appConfig.TwitchId, prediction.Id,
            PredictionEndStatus.RESOLVED, targetOutcome.Id);

        await _chatBot.Channel.SendMessageAsync("Закрыл ставку.");

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
}