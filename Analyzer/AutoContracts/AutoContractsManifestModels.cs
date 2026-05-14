using System.Collections.Generic;

namespace CaeriusNet.Analyzer.AutoContracts;

internal sealed record AutoContractsManifest(
    int Version,
    string Namespace,
    EquatableArray<AutoContractsTableType> TableTypes,
    EquatableArray<AutoContractsProcedure> Procedures);

internal sealed record AutoContractsTableType(
    string Schema,
    string Name,
    string ClrName,
    EquatableArray<AutoContractsColumn> Columns);

internal sealed record AutoContractsProcedure(
    string Schema,
    string Name,
    string ClrName,
    string ParametersClrName,
    string? ResultClrName,
    EquatableArray<AutoContractsParameter> Parameters,
    AutoContractsResultSet ResultSet);

internal sealed record AutoContractsParameter(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool IsTableType,
    bool IsOutput,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);

internal sealed record AutoContractsResultSet(
    string Status,
    EquatableArray<AutoContractsColumn> Columns);

internal sealed record AutoContractsColumn(
    int Ordinal,
    string Name,
    string SqlType,
    string ClrType,
    bool Nullable,
    int? MaxLength,
    byte? Precision,
    byte? Scale);

internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>
    where T : IEquatable<T>
{
    internal ImmutableArray<T> Items { get; } = items.IsDefault ? ImmutableArray<T>.Empty : items;

    internal static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public bool Equals(EquatableArray<T> other)
    {
        if (Items.Length != other.Items.Length)
            return false;

        for (var i = 0; i < Items.Length; i++)
            if (!EqualityComparer<T>.Default.Equals(Items[i], other.Items[i]))
                return false;

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in Items)
            hash = hash * 31 + EqualityComparer<T>.Default.GetHashCode(item);

        return hash;
    }

    public static implicit operator ImmutableArray<T>(EquatableArray<T> array)
    {
        return array.Items;
    }
}
