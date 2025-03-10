namespace TwitchDotaBot;

public class AppConfig
{
    public required string TwitchUsername { get; init; }
    public required string TwitchId { get; init; }
    public required ulong SteamId { get; init; }

    public int PredictionTimeWindow { get; init; } = 200;
    public string MainVillainName { get; init; } = "urantij";
}