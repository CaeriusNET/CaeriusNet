namespace CaeriusNet.Redis;

public sealed class CaeriusRedisOptions
{
    public int Database { get; set; } = -1;
    public string KeyPrefix { get; set; } = string.Empty;
    public int MaxPayloadBytes { get; set; } = 1024 * 1024;
}
