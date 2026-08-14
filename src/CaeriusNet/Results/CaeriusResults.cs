namespace CaeriusNet.Results;

public enum CaeriusResultSetCountPolicy : byte
{
    Exactly = 0,
    AllowMissing = 1,
    AllowAdditional = 2,
    AllowMissingOrAdditional = 3
}

public readonly record struct CaeriusCommandResult<TOutput>(TOutput Output);

public readonly record struct CaeriusExecutionResult<TData, TOutput>(TData Data, TOutput Output);

public sealed class CaeriusOutputValues
{
    internal CaeriusOutputValues(IReadOnlyDictionary<string, object?> values)
    {
        Values = values;
    }

    public IReadOnlyDictionary<string, object?> Values { get; }

    public T? Get<T>(string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterName);
        var normalized = StoredProcedureCommandBuilder.NormalizeParameterName(parameterName);
        if (!Values.TryGetValue(normalized, out var value))
            throw new KeyNotFoundException($"Output parameter '{normalized}' was not declared.");
        if (value is null or DBNull)
            return default;
        if (value is T typed)
            return typed;
        try
        {
            var target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            return (T?)Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new CaeriusContractException(
                $"Output parameter '{normalized}' cannot be converted from {value.GetType().FullName} to {typeof(T).FullName}.",
                ex);
        }
    }
}
