using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace TwitchDotaBot;

// https://github.com/Urantij/HipDiscordBot/blob/master/HipDiscordBot/Utilities/ServiceCollectionCancerExtensions.cs

public static class ServiceCollectionCancerExtensions
{
    public static void AddCancerOptions<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TConfig>(
        this IServiceCollection serviceCollection, string path, CancerConfigLoader loader) where TConfig : class
    {
        TConfig config = loader.LoadConfig<TConfig>(path);

        serviceCollection.AddCancerOptions<TConfig>(config);
    }

    public static void AddCancerOptions<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TConfig>(
        this IServiceCollection serviceCollection, TConfig config) where TConfig : class
    {
        IOptions<TConfig> options = new OptionsWrapper<TConfig>(config);

        serviceCollection.AddSingleton<IOptions<TConfig>>(sc => options);
    }
}