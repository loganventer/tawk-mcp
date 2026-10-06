namespace Tawk.Mcp.Core.Transcription;

/// <summary>What the user set for transcription when starting tawk-mcp. An agent chooses only within it.</summary>
public sealed record TranscriptionOptions
{
    public const string Auto = "auto";

    /// <summary>The default until the user names another: close to the largest model in accuracy and several times quicker, which is what mixed and smaller languages need.</summary>
    public const string DefaultModel = "large-v3-turbo";

    public const int DefaultMaxLanguages = 3;

    public const int DefaultMaxSeconds = 3600;

    public const int DefaultTimeoutS = 300;

    public const int DefaultKeptJobs = 50;

    public TranscriptionEngine Engine { get; init; } = TranscriptionEngine.Auto;

    /// <summary>The base address of the transcriber for <see cref="TranscriptionEngine.Http"/>.</summary>
    public Uri? Url { get; init; }

    /// <summary>The program and its arguments for <see cref="TranscriptionEngine.Command"/>, with placeholders.</summary>
    public string? Command { get; init; }

    /// <summary>The model used when a call names none and tawk's settings panel names none either. Null is <see cref="DefaultModel"/>.</summary>
    public string? Model { get; init; }

    /// <summary>The model names an agent may ask for, besides the default.</summary>
    public IReadOnlyList<string> Models { get; init; } = [];

    /// <summary>The languages used when a call names none and tawk's settings panel names none either: ISO 639-1 codes or auto. Null is auto.</summary>
    public IReadOnlyList<string>? Languages { get; init; }

    public int MaxLanguages { get; init; } = DefaultMaxLanguages;

    /// <summary>The longest recording accepted, where the engine reports a length.</summary>
    public int MaxSeconds { get; init; } = DefaultMaxSeconds;

    /// <summary>How long one pass may run.</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(DefaultTimeoutS);

    public int Concurrency { get; init; } = 1;

    /// <summary>How many finished jobs are kept in memory for get_transcript.</summary>
    public int KeptJobs { get; init; } = DefaultKeptJobs;

    /// <summary>How long a finished job is kept.</summary>
    public TimeSpan KeptFor { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>Transcribe every voice note another person sends, without being asked. Used only when tawk's settings panel has no such switch. Null is off.</summary>
    public bool? Automatic { get; init; }

    /// <summary>The folder of model files for the model tawk-mcp loads itself.</summary>
    public string ModelDirectory { get; init; } = string.Empty;

    /// <summary>How long that model stays loaded with no job to run. Zero keeps it loaded.</summary>
    public TimeSpan IdleUnload { get; init; } = TimeSpan.FromMinutes(15);

    /// <summary>How long a job waits for another tawk-mcp on this machine to let go of its model.</summary>
    public TimeSpan LockWait { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>The names of the models tawk-mcp can load, as tawk's settings list them.</summary>
    public static IReadOnlyList<string> KnownModels { get; } = ["tiny", "base", "small", "medium", "large-v3-turbo", "large-v3"];

    public bool Enabled => Engine != TranscriptionEngine.Off;
}
