namespace Robotron2084.Persistence;

/// <summary>
/// A saved file that EXISTS could not be read or parsed (ERR-1): the message names the
/// file and the cause so the failure is readable. A missing file is not an error — a
/// fresh cabinet comes up with its factory values.
/// </summary>
public sealed class PersistenceException(string message, Exception innerException) : Exception(message, innerException);
