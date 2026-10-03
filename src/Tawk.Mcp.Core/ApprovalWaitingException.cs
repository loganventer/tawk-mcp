namespace Tawk.Mcp.Core;

/// <summary>
/// A write that tawk queued for an answer, handed back instead of waited on because this instance may answer
/// its own requests. Not a failure: the request is still alive in tawk under <see cref="RequestId"/>.
/// </summary>
public sealed class ApprovalWaitingException : Exception
{
    public ApprovalWaitingException()
        : this(string.Empty, string.Empty)
    {
    }

    public ApprovalWaitingException(string message)
        : base(message)
    {
        RequestId = string.Empty;
        Op = string.Empty;
    }

    public ApprovalWaitingException(string message, Exception innerException)
        : base(message, innerException)
    {
        RequestId = string.Empty;
        Op = string.Empty;
    }

    public ApprovalWaitingException(string requestId, string op)
        : base($"Request {requestId} ({op}) waits for an answer in tawk.")
    {
        RequestId = requestId;
        Op = op;
    }

    public string RequestId { get; }

    public string Op { get; }
}
