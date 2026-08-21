using System.Data.SqlTypes;

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
            return (T?)CaeriusValueConverter.Convert(value, typeof(T));
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or
                                       SqlTypeException)
        {
            throw new CaeriusContractException(
                $"Output parameter '{normalized}' cannot be converted from {value.GetType().FullName} to {typeof(T).FullName}.",
                ex);
        }
    }
}

internal static class CaeriusValueConverter
{
    internal static object Convert(object value, Type destinationType)
    {
        var target = Nullable.GetUnderlyingType(destinationType) ?? destinationType;
        if (target.IsInstanceOfType(value))
            return value;

        if (target.IsEnum)
        {
            var underlying = Enum.GetUnderlyingType(target);
            var numeric = ConvertNumeric(value, underlying);
            return Enum.ToObject(target, numeric);
        }

        if (target == typeof(Guid))
            return value switch
            {
                Guid guid => guid,
                string text => Guid.Parse(text),
                byte[] bytes => new Guid(bytes),
                _ => throw new InvalidCastException()
            };
        if (target == typeof(DateOnly))
            return value switch
            {
                DateOnly date => date,
                DateTime dateTime => DateOnly.FromDateTime(dateTime),
                _ => throw new InvalidCastException()
            };
        if (target == typeof(TimeOnly))
            return value switch
            {
                TimeOnly time => time,
                TimeSpan timeSpan => TimeOnly.FromTimeSpan(timeSpan),
                _ => throw new InvalidCastException()
            };
        if (target == typeof(SqlDecimal))
            return value switch
            {
                SqlDecimal sqlDecimal => sqlDecimal,
                decimal decimalValue => new SqlDecimal(decimalValue),
                string text => SqlDecimal.Parse(text),
                _ => throw new InvalidCastException()
            };
        return ConvertNumeric(value, target);
    }

    private static object ConvertNumeric(object value, Type target)
    {
        // Convert.ChangeType rounds Decimal/Double values when converting to an integral
        // destination.  SQL result/output conversions instead follow C# checked-cast semantics
        // so that narrowing never silently wraps and has the same behaviour for scalars, enums,
        // and output parameters.
        if (target == typeof(sbyte))
            return checked((sbyte)ConvertToDecimal(value));
        if (target == typeof(byte))
            return checked((byte)ConvertToDecimal(value));
        if (target == typeof(short))
            return checked((short)ConvertToDecimal(value));
        if (target == typeof(ushort))
            return checked((ushort)ConvertToDecimal(value));
        if (target == typeof(int))
            return checked((int)ConvertToDecimal(value));
        if (target == typeof(uint))
            return checked((uint)ConvertToDecimal(value));
        if (target == typeof(long))
            return checked((long)ConvertToDecimal(value));
        if (target == typeof(ulong))
            return checked((ulong)ConvertToDecimal(value));
        if (target == typeof(decimal))
            return ConvertToDecimal(value);
        if (value is SqlDecimal sourceSqlDecimal)
            value = sourceSqlDecimal.Value;

        return System.Convert.ChangeType(value, target, CultureInfo.InvariantCulture)!;
    }

    private static decimal ConvertToDecimal(object value)
    {
        return value switch
        {
            SqlDecimal sqlDecimal => sqlDecimal.Value,
            decimal decimalValue => decimalValue,
            byte byteValue => byteValue,
            sbyte signedByteValue => signedByteValue,
            short shortValue => shortValue,
            ushort unsignedShortValue => unsignedShortValue,
            int intValue => intValue,
            uint unsignedIntValue => unsignedIntValue,
            long longValue => longValue,
            ulong unsignedLongValue => unsignedLongValue,
            float singleValue => checked((decimal)singleValue),
            double doubleValue => checked((decimal)doubleValue),
            string text => decimal.Parse(text, NumberStyles.Number, CultureInfo.InvariantCulture),
            _ => System.Convert.ToDecimal(value, CultureInfo.InvariantCulture)
        };
    }
}
