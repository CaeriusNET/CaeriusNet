using System.Collections;

namespace CaeriusNet.Generator.Models;

/// <summary>
///     Value-equatable wrapper around <see cref="ImmutableArray{T}" />, designed to flow through Roslyn
///     incremental pipelines without busting the cache.
/// </summary>
/// <remarks>
///     <para>
///         Roslyn's incremental engine compares pipeline values with <see cref="object.Equals(object?)" />.
///         <see cref="ImmutableArray{T}" /> compares by reference, which means even an unchanged sequence
///         registers as a difference and forces downstream stages to re-run. <see cref="EquatableArray{T}" />
///         compares element-by-element so semantically-equal collections re-use cached output.
///     </para>
///     <para>
///         Backed by an <see cref="ImmutableArray{T}" /> to keep allocations and indirection minimal; only
///         a single struct field is added on top of the underlying array reference.
///     </para>
/// </remarks>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _array;

    public EquatableArray(ImmutableArray<T> array)
    {
        _array = array;
    }

    public static EquatableArray<T> Empty => new(ImmutableArray<T>.Empty);

    public int Count => _array.IsDefault ? 0 : _array.Length;

    public T this[int index] => _array[index];

    public ImmutableArray<T> AsImmutableArray()
    {
        return _array.IsDefault ? ImmutableArray<T>.Empty : _array;
    }

    public bool Equals(EquatableArray<T> other)
    {
        if (_array.IsDefault) return other._array.IsDefault;
        if (other._array.IsDefault) return false;
        if (_array.Length != other._array.Length) return false;

        for (var i = 0; i < _array.Length; i++)
            if (!_array[i].Equals(other._array[i]))
                return false;

        return true;
    }

    public override bool Equals(object? obj)
    {
        return obj is EquatableArray<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        if (_array.IsDefault) return 0;

        unchecked
        {
            var hash = 17;
            for (var i = 0; i < _array.Length; i++)
                hash = hash * 31 + _array[i].GetHashCode();
            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)AsImmutableArray()).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right)
    {
        return !left.Equals(right);
    }
}
