namespace CaeriusNet.Utilities;

/// <summary>
///     Helper for array pooling to reduce allocations
/// </summary>
static internal class ArrayPoolHelper
{
	/// <summary>
	///     Retrieves an array from the shared pool with a minimum length.
	/// </summary>
	/// <typeparam name="T">The type of the array elements.</typeparam>
	/// <param name="minimumLength">The minimum length of the array to rent.</param>
	/// <returns>An array of type T that is at least the requested length.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T[] Rent<T>(int minimumLength)
	{
		return ArrayPool<T>.Shared.Rent(minimumLength);
	}

	/// <summary>
	///     Returns an array to the shared pool.
	/// </summary>
	/// <typeparam name="T">The type of the array elements.</typeparam>
	/// <param name="array">The array to return to the pool.</param>
	/// <param name="clearArray">Indicates whether to clear the array before returning it to the pool.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Return<T>(T[] array, bool clearArray = false)
	{
		ArrayPool<T>.Shared.Return(array, clearArray);
	}
}