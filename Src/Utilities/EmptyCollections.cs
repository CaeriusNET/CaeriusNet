namespace CaeriusNet.Utilities;

/// <summary>
///     Provides cached empty collections to avoid repeated allocations
/// </summary>
static internal class EmptyCollections
{
	/// <summary>
	///     Returns a cached empty read-only collection for the given type.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <returns>A read-only collection with no elements.</returns>
	/// <remarks>
	///     This method returns a singleton instance of an empty read-only collection
	///     to avoid unnecessary allocations when an empty collection is needed.
	///     The returned collection is thread-safe and immutable.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlyCollection<T> ReadOnlyCollection<T>()
	{
		return EmptyReadOnlyCollection<T>.Instance;
	}

	private static class EmptyReadOnlyCollection<T>
	{
		public static readonly ReadOnlyCollection<T> Instance =
			new(Array.Empty<T>());
	}
}