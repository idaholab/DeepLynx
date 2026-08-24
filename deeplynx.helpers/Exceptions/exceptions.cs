namespace deeplynx.helpers.exceptions;

public class DependencyDeletionException : Exception
{
    public DependencyDeletionException(string message) : base(message) { }
}

public class NoResultsException : Exception
{
    public NoResultsException(string message) : base(message) { }
}

public class InvalidRequestException : Exception
{
    public InvalidRequestException(string message) : base(message) { }
}

public class OauthException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public OauthException(string errorCode, string errorDescription, int statusCode) : base(errorDescription)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}
