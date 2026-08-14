using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.Metrics;
using System.Text;
using CaeriusNet.Abstractions;
using CaeriusNet.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace CaeriusNet.Redis;

internal sealed class RedisCacheManager : IRedisCacheManager, IDisposable
{
    private const uint Magic = 0x54454E43; // CNET in little-endian bytes.
    private const byte EnvelopeVersion = 1;

    private static readonly string InstrumentationVersion =
        typeof(RedisCacheManager).Assembly.GetName().Version?.ToString(3) ?? "12.0.0";

    private readonly Counter<long> _corruptions;
    private readonly IDatabase _database;
    private readonly string _keyPrefix;
    private readonly ILogger<RedisCacheManager> _logger;
    private readonly Meter _meter = new(CaeriusRedisTelemetry.MeterName, InstrumentationVersion);

    public RedisCacheManager(
        IConnectionMultiplexer multiplexer,
        IOptions<CaeriusRedisOptions> options,
        ILogger<RedisCacheManager> logger)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger;
        _database = multiplexer.GetDatabase(options.Value.Database);
        _keyPrefix = options.Value.KeyPrefix;
        ArgumentException.ThrowIfNullOrWhiteSpace(_keyPrefix);
        _corruptions = _meter.CreateCounter<long>("caerius.cache.corruptions", "{payload}");
    }

    public void Dispose()
    {
        _meter.Dispose();
    }

    public async ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
        string key,
        ICaeriusCacheCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        Validate(key, codec);
        var redisKey = (RedisKey)(_keyPrefix + key);
        var leaseTask = _database.StringGetLeaseAsync(redisKey);
        Lease<byte>? lease;
        try
        {
            lease = await leaseTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // WaitAsync cancels the wait, not the Redis operation. Observe the late task and
            // dispose the lease it eventually produces so a cancelled caller cannot leak it.
            // The task may complete in the narrow race between WaitAsync choosing cancellation
            // and this catch executing, so this is deliberately unconditional.
            _ = DisposeLeaseWhenCompletedAsync(leaseTask);
            throw;
        }

        using (lease)
        {
            if (lease is null)
                return CaeriusCacheLookup<T>.Miss;

            try
            {
                var payload = ReadEnvelope(lease.Span, codec);
                return CaeriusCacheLookup<T>.Hit(codec.Decode(payload));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The codec boundary intentionally turns all malformed or incompatible payloads
                // into a controlled miss. A caller cancellation is kept distinct above.
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    var errorType = GetSafeErrorType(exception);
                    CaeriusRedisLog.CorruptPayload(_logger, errorType);
                }

                if (_corruptions.Enabled)
                    _corruptions.Add(1);
                try
                {
                    _ = await _database.KeyDeleteAsync(redisKey)
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception deleteException)
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        var errorType = GetSafeErrorType(deleteException);
                        CaeriusRedisLog.CorruptRemovalFailed(_logger, errorType);
                    }
                }

                return CaeriusCacheLookup<T>.Miss;
            }
        }
    }

    public async ValueTask SetAsync<T>(
        string key,
        T value,
        ICaeriusCacheCodec<T> codec,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default) where T : notnull
    {
        Validate(key, codec);
        if (expiration is { } actualExpiration && actualExpiration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiration));

        var payload = new ArrayBufferWriter<byte>();
        codec.Encode(payload, value);
        var envelope = WriteEnvelope(codec, payload.WrittenSpan);
        try
        {
            _ = await _database.StringSetAsync(
                    _keyPrefix + key,
                    envelope.WrittenMemory,
                    expiration,
                    false)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CaeriusCacheException("set", exception);
        }
    }

    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        try
        {
            _ = await _database.KeyDeleteAsync(_keyPrefix + key)
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CaeriusCacheException("remove", exception);
        }
    }

    private static async Task DisposeLeaseWhenCompletedAsync(Task<Lease<byte>?> leaseTask)
    {
        try
        {
            using var lease = await leaseTask.ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The caller cancelled before the Redis operation completed. Awaiting observes a
            // late transport failure; there is no request left to surface it to.
        }
    }

    private static ArrayBufferWriter<byte> WriteEnvelope<T>(ICaeriusCacheCodec<T> codec, ReadOnlySpan<byte> payload)
    {
        var codecId = Encoding.UTF8.GetBytes(codec.CodecId);
        var contractHash = Encoding.UTF8.GetBytes(codec.ContractHash);
        if (codecId.Length > ushort.MaxValue || contractHash.Length > ushort.MaxValue)
            throw new CaeriusContractException("Codec identifiers and contract hashes must fit in 65,535 UTF-8 bytes.");

        var writer = new ArrayBufferWriter<byte>(13 + codecId.Length + contractHash.Length + payload.Length);
        var header = writer.GetSpan(13);
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        header[4] = EnvelopeVersion;
        BinaryPrimitives.WriteUInt16LittleEndian(header[5..], (ushort)codecId.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(header[7..], (ushort)contractHash.Length);
        BinaryPrimitives.WriteInt32LittleEndian(header[9..], payload.Length);
        writer.Advance(13);
        Write(writer, codecId);
        Write(writer, contractHash);
        Write(writer, payload);
        return writer;
    }

    private static ReadOnlySpan<byte> ReadEnvelope<T>(ReadOnlySpan<byte> envelope, ICaeriusCacheCodec<T> codec)
    {
        if (envelope.Length < 13 || BinaryPrimitives.ReadUInt32LittleEndian(envelope) != Magic)
            throw new CaeriusContractException("Redis payload magic is invalid.");
        if (envelope[4] != EnvelopeVersion)
            throw new CaeriusContractException("Redis payload envelope version is unsupported.");
        var codecLength = BinaryPrimitives.ReadUInt16LittleEndian(envelope[5..]);
        var contractLength = BinaryPrimitives.ReadUInt16LittleEndian(envelope[7..]);
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(envelope[9..]);
        var expectedLength = 13L + codecLength + contractLength + payloadLength;
        if (payloadLength < 0 || expectedLength != envelope.Length)
            throw new CaeriusContractException("Redis payload length is invalid.");

        var offset = 13;
        var codecId = Encoding.UTF8.GetString(envelope.Slice(offset, codecLength));
        offset += codecLength;
        var contractHash = Encoding.UTF8.GetString(envelope.Slice(offset, contractLength));
        offset += contractLength;
        if (!string.Equals(codecId, codec.CodecId, StringComparison.Ordinal) ||
            !string.Equals(contractHash, codec.ContractHash, StringComparison.Ordinal))
            throw new CaeriusContractException(
                "Redis payload codec or contract hash does not match the current DTO contract.");
        return envelope.Slice(offset, payloadLength);
    }

    private static void Write(ArrayBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        var target = writer.GetSpan(value.Length);
        value.CopyTo(target);
        writer.Advance(value.Length);
    }

    private static void Validate<T>(string key, ICaeriusCacheCodec<T> codec)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentException.ThrowIfNullOrWhiteSpace(codec.CodecId);
        ArgumentException.ThrowIfNullOrWhiteSpace(codec.ContractHash);
    }

    private static string GetSafeErrorType(Exception exception)
    {
        return exception.GetType().FullName ?? exception.GetType().Name;
    }
}
