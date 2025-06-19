namespace Iris.Brokers.Exceptions;

public class InvalidConnectionException : Exception
{
    public InvalidConnectionException()
    {
    }

    public InvalidConnectionException(string message) 
        : base(message)
    {
    }

    public InvalidConnectionException(string message, Exception innerException) 
        : base(message, innerException)
    {
    }
}