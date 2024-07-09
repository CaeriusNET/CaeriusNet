namespace CaeriusNet.Mappers;

public delegate T MapFromReaderDelegate<out T>(SqlDataReader reader);