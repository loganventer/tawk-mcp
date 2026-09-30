namespace Tawk.Mcp.Core;

public sealed class TawkControlException : Exception
{
    public const string NotRunningMessage =
        "tawk is not running, or its control socket is off (Settings > Automation > Control socket). tawk-mcp will connect as soon as it is.";

    public TawkControlException()
        : this(new ControlError("unknown", "The tawk control request failed."))
    {
    }

    public TawkControlException(string message)
        : this(new ControlError("unknown", message))
    {
    }

    public TawkControlException(string message, Exception innerException)
        : base(message, innerException)
    {
        Error = new ControlError("unknown", message);
        Code = ControlErrorCode.Unknown;
    }

    public TawkControlException(ControlError error)
        : base(error?.Message)
    {
        ArgumentNullException.ThrowIfNull(error);
        Error = error;
        Code = error.Kind;
    }

    public TawkControlException(ControlErrorCode code, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        Error = new ControlError(ControlErrorCodes.ToWire(code), message);
    }

    public ControlErrorCode Code { get; }

    public ControlError Error { get; }

    public static TawkControlException NotRunning(string? socketPath = null, Exception? innerException = null) =>
        new(
            ControlErrorCode.NotRunning,
            socketPath is null ? NotRunningMessage : $"{NotRunningMessage} Socket: {socketPath}",
            innerException);

    public static TawkControlException Offline(string message) => new(ControlErrorCode.Offline, message);
}
