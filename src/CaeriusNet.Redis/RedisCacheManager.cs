using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
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

    private const string ReadTaggedScript = """
                                            local payload = redis.call('HGET', KEYS[1], 'payload')
                                            if not payload then return false end
                                            for i = 2, #KEYS do
                                              local current = redis.call('GET', KEYS[i]) or '0'
                                              local stored = redis.call('HGET', KEYS[1], 'g' .. (i - 2))
                                              if not stored or stored ~= current then
                                                redis.call('DEL', KEYS[1])
                                                return false
                                              end
                                            end
                                            return payload
                                            """;

    private const string PublishTaggedScript = """
                                               for i = 2, #KEYS do
                                                 local current = redis.call('GET', KEYS[i]) or '0'
                                                 if current ~= ARGV[i + 1] then return 0 end
                                               end
                                               redis.call('DEL', KEYS[1])
                                               redis.call('HSET', KEYS[1], 'payload', ARGV[1])
                                               for i = 2, #KEYS do
                                                 redis.call('HSET', KEYS[1], 'g' .. (i - 2), ARGV[i + 1])
                                               end
                                               local expiry = tonumber(ARGV[2])
                                               if expiry and expiry > 0 then redis.call('PEXPIRE', KEYS[1], expiry) end
                                               return 1
                                               """;

    private const string InvalidateTagsScript = """
                                                for i = 1, #KEYS do redis.call('INCR', KEYS[i]) end
                                                return #KEYS
                                                """;

    private const string CompareDeleteStringScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    private const string CompareDeleteHashScript =
        "if redis.call('HGET', KEYS[1], 'payload') == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    // Cache identities are protocol values. Replacement fallback would make two distinct malformed
    // strings address the same Redis key or envelope identity, so reject malformed UTF-16 instead.
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly string InstrumentationVersion =
        typeof(RedisCacheManager).Assembly.GetName().Version?.ToString(3) ?? "11.2.0";

    private readonly Counter<long> _corruptions;
    private readonly IDatabase _database;
    private readonly string _keyPrefix;
    private readonly ILogger<RedisCacheManager> _logger;
    private readonly int _maxPayloadBytes;
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
        _keyPrefix = options.Value.KeyPrefix ??
                     throw new ArgumentException("Redis key prefix cannot be null.", nameof(options));
        _maxPayloadBytes = options.Value.MaxPayloadBytes;
        if (_maxPayloadBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Maximum Redis payload size must be positive.");
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
        catch (RedisTimeoutException exception)
        {
            throw new CaeriusCacheException("get", exception);
        }
        catch (RedisException exception)
        {
            throw new CaeriusCacheException("get", exception);
        }

        using (lease)
        {
            if (lease is null)
                return CaeriusCacheLookup<T>.Miss;

            try
            {
                EnsureStoredPayloadWithinLimit(lease.Span.Length);
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
                RecordCorruption(exception);
                try
                {
                    _ = await _database.ScriptEvaluateAsync(
                            CompareDeleteStringScript,
                            [redisKey],
                            [lease.Span.ToArray()])
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
        ValidatePayloadSize(envelope.WrittenCount);
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

    public async ValueTask<CaeriusCacheTagSnapshot> CaptureTagSnapshotAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count == 0)
            return CaeriusCacheTagSnapshot.Empty;
        try
        {
            var keys = BuildGenerationKeys(tags);
            var values = await _database.StringGetAsync(keys).WaitAsync(cancellationToken).ConfigureAwait(false);
            var generations = ImmutableArray.CreateBuilder<long>(values.Length);
            foreach (var value in values)
                generations.Add(value.HasValue && long.TryParse(
                    value.ToString(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var generation)
                    ? generation
                    : 0L);

            return new CaeriusCacheTagSnapshot(generations.MoveToImmutable());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CaeriusCacheException("capture-tag-snapshot", exception);
        }
    }

    public async ValueTask<CaeriusCacheLookup<T>> TryGetAsync<T>(
        string key,
        IReadOnlyList<string> tags,
        ICaeriusCacheCodec<T> codec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count == 0)
            return await TryGetAsync(key, codec, cancellationToken).ConfigureAwait(false);

        Validate(key, codec);
        var keys = BuildScriptKeys(key, tags);
        RedisResult result;
        try
        {
            result = await _database.ScriptEvaluateAsync(ReadTaggedScript, keys)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CaeriusCacheException("get", exception);
        }

        if (result.IsNull)
            return CaeriusCacheLookup<T>.Miss;
        var envelope = (byte[]?)result;
        if (envelope is null)
            return CaeriusCacheLookup<T>.Miss;
        try
        {
            EnsureStoredPayloadWithinLimit(envelope.Length);
            return CaeriusCacheLookup<T>.Hit(codec.Decode(ReadEnvelope(envelope, codec)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            RecordCorruption(exception);
            try
            {
                _ = await _database.ScriptEvaluateAsync(
                        CompareDeleteHashScript,
                        [keys[0]],
                        [envelope])
                    .WaitAsync(cancellationToken).ConfigureAwait(false);
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

    public async ValueTask<bool> SetIfTagSnapshotUnchangedAsync<T>(
        string key,
        T value,
        IReadOnlyList<string> tags,
        CaeriusCacheTagSnapshot snapshot,
        ICaeriusCacheCodec<T> codec,
        TimeSpan? expiration,
        CancellationToken cancellationToken = default) where T : notnull
    {
        ArgumentNullException.ThrowIfNull(tags);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (tags.Count == 0)
        {
            await SetAsync(key, value, codec, expiration, cancellationToken).ConfigureAwait(false);
            return true;
        }

        if (snapshot.Generations.Length != tags.Count)
            throw new CaeriusContractException("The Redis tag snapshot does not match the dependency tag count.");

        Validate(key, codec);
        var payload = new ArrayBufferWriter<byte>();
        codec.Encode(payload, value);
        var envelope = WriteEnvelope(codec, payload.WrittenSpan);
        ValidatePayloadSize(envelope.WrittenCount);
        var expiryMilliseconds = expiration is null
            ? 0L
            : Math.Max(1L, checked((long)expiration.Value.TotalMilliseconds));
        var arguments = new RedisValue[2 + snapshot.Generations.Length];
        arguments[0] = envelope.WrittenMemory.ToArray();
        arguments[1] = expiryMilliseconds;
        for (var index = 0; index < snapshot.Generations.Length; index++)
            arguments[index + 2] = snapshot.Generations[index];
        try
        {
            var result = await _database.ScriptEvaluateAsync(PublishTaggedScript, BuildScriptKeys(key, tags), arguments)
                .WaitAsync(cancellationToken).ConfigureAwait(false);
            return (long)result == 1L;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CaeriusCacheException("set-if-tag-snapshot-unchanged", exception);
        }
    }

    public async ValueTask InvalidateTagsAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        if (tags.Count == 0)
            return;
        try
        {
            _ = await _database.ScriptEvaluateAsync(InvalidateTagsScript, BuildGenerationKeys(tags))
                .WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new CaeriusCacheException("invalidate-tags", exception);
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

    private RedisKey[] BuildScriptKeys(string key, IReadOnlyList<string> tags)
    {
        var keys = new RedisKey[tags.Count + 1];
        keys[0] = _keyPrefix + key;
        var generationKeys = BuildGenerationKeys(tags);
        generationKeys.CopyTo(keys, 1);
        return keys;
    }

    private RedisKey[] BuildGenerationKeys(IReadOnlyList<string> tags)
    {
        var keys = new RedisKey[tags.Count];
        for (var index = 0; index < tags.Count; index++)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tags[index]);
            var digest = SHA256.HashData(StrictUtf8.GetBytes(tags[index]));
            keys[index] = _keyPrefix + "caerius:v2:tag:" + Convert.ToHexString(digest).ToLowerInvariant();
        }

        return keys;
    }

    private void ValidatePayloadSize(int length)
    {
        if (length > _maxPayloadBytes)
            throw new CaeriusCacheException(
                "payload-limit",
                new InvalidOperationException($"The encoded Redis payload exceeds {_maxPayloadBytes} bytes."));
    }

    private void EnsureStoredPayloadWithinLimit(int length)
    {
        if (length > _maxPayloadBytes)
            throw new CaeriusContractException(
                $"The stored Redis payload exceeds the configured {_maxPayloadBytes}-byte limit.");
    }

    private void RecordCorruption(Exception exception)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
            CaeriusRedisLog.CorruptPayload(_logger, GetSafeErrorType(exception));
        if (_corruptions.Enabled)
            _corruptions.Add(1);
    }

    private static ArrayBufferWriter<byte> WriteEnvelope<T>(ICaeriusCacheCodec<T> codec, ReadOnlySpan<byte> payload)
    {
        var codecId = StrictUtf8.GetBytes(codec.CodecId);
        var contractHash = StrictUtf8.GetBytes(codec.ContractHash);
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
        var codecId = StrictUtf8.GetString(envelope.Slice(offset, codecLength));
        offset += codecLength;
        var contractHash = StrictUtf8.GetString(envelope.Slice(offset, contractLength));
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
