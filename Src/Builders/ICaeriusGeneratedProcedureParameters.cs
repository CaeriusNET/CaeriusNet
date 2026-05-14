namespace CaeriusNet.Builders;

/// <summary>
///     Binds a generated parameter record to a typed stored procedure builder.
/// </summary>
/// <typeparam name="TProcedure">Generated procedure descriptor type.</typeparam>
/// <typeparam name="TSelf">Generated parameter record type.</typeparam>
public interface ICaeriusGeneratedProcedureParameters<TProcedure, TSelf>
    where TProcedure : struct, ICaeriusGeneratedProcedure<TProcedure>
    where TSelf : ICaeriusGeneratedProcedureParameters<TProcedure, TSelf>
{
    /// <summary>
    ///     Adds all generated SQL parameters to the supplied builder in manifest ordinal order.
    /// </summary>
    static abstract void Bind(StoredProcedureParametersBuilder<TProcedure> builder, TSelf parameters);
}
