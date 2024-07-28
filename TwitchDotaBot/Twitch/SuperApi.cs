using Microsoft.Extensions.Options;
using TwitchLib.Api;
using TwitchLib.Api.Auth;

namespace TwitchDotaBot.Twitch;

public class TwitchApiConfig
{
    public string ClientId { get; set; }
    public string Secret { get; set; }
    public string RefreshToken { get; set; }
}

public class SuperApi
{
    private readonly TwitchAPI _api;

    private readonly string _refreshToken;

    public SuperApi(IOptions<TwitchApiConfig> options)
    {
        _refreshToken = options.Value.RefreshToken;

        _api = new TwitchAPI();
        _api.Settings.ClientId = options.Value.ClientId;
        _api.Settings.Secret = options.Value.Secret;
    }

    public async Task<TwitchAPI> GetApiAsync()
    {
        if (_api.Settings.AccessToken != null)
        {
            ValidateAccessTokenResponse? response = await _api.Auth.ValidateAccessTokenAsync();

            if (response == null)
            {
                _api.Settings.AccessToken = null;
            }
        }

        if (_api.Settings.AccessToken == null)
        {
            RefreshResponse refreshResponse =
                await _api.Auth.RefreshAuthTokenAsync(_refreshToken, _api.Settings.Secret);

            _api.Settings.AccessToken = refreshResponse.AccessToken;
        }

        return _api;
    }
}