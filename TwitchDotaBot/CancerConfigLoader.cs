using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TwitchDotaBot.Dota;
using TwitchDotaBot.Twitch;

namespace TwitchDotaBot;

// https://github.com/Urantij/HipDiscordBot/blob/master/HipDiscordBot/Utilities/CancerConfigLoader.cs

[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(TwitchApiConfig))]
[JsonSerializable(typeof(ChatBotConfig))]
[JsonSerializable(typeof(DotaConfig))]
[JsonSerializable(typeof(MedusaShameConfig))]
public partial class ConfigSerializerContext : JsonSerializerContext
{
}

public class CancerConfigLoader
{
    private const string MyConfigPath = "./appsettings.json";

    private readonly JsonNode _root;

    public CancerConfigLoader(JsonNode root)
    {
        this._root = root;
    }

    public bool TryLoadConfig<TConfig>(string path, [MaybeNullWhen(false)] out TConfig config) where TConfig : class
    {
        JsonNode? targetNode = LocateNode(_root, path);

        if (targetNode == null)
        {
            config = null;
            return false;
        }

        var result = targetNode.Deserialize(typeof(TConfig), ConfigSerializerContext.Default) as TConfig;

        if (result == null)
            throw new Exception("result null");

        config = result;
        return true;
    }

    public TConfig LoadConfig<TConfig>(string path) where TConfig : class
    {
        JsonNode? targetNode = LocateNode(_root, path);

        if (targetNode == null)
        {
            throw new Exception("node null");
        }

        var result = targetNode.Deserialize(typeof(TConfig), ConfigSerializerContext.Default) as TConfig;

        if (result == null)
            throw new Exception("result null");

        return result;
    }

    private static JsonNode? LocateNode(JsonNode start, string path)
    {
        string[] entries = path.Split('/');

        JsonNode? targetNode = start;

        foreach (string entry in entries)
        {
            JsonNode? node = targetNode[entry];

            if (node == null)
                return null;

            targetNode = node;
        }

        return targetNode;
    }

    public static CancerConfigLoader Load()
    {
        string content = File.ReadAllText(MyConfigPath);

        JsonNode? root = JsonNode.Parse(content);
        if (root == null)
            throw new Exception("root null");

        return new CancerConfigLoader(root);
    }
}