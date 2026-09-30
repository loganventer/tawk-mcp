namespace Tawk.Mcp.Engines.Memory;

/// <summary>One field a contact profile may hold, what values it takes, and who may set it.</summary>
public sealed record ContactFieldDefinition(string Name, string Group, FieldKind Kind, string Description)
{
    public IReadOnlyList<string>? Choices { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public int MaxItems { get; init; } = 20;

    /// <summary>False when only the user or the contact may state it, never an inference from chats.</summary>
    public bool Inferable { get; init; } = true;

    /// <summary>Special personal information (health, beliefs, and so on): only the user may set it, and it is hidden unless asked for.</summary>
    public bool Sensitive { get; init; }

    /// <summary>How long an inferred value lasts before it lapses.</summary>
    public TimeSpan? InferredLifetime { get; init; } = TimeSpan.FromDays(365);

    /// <summary>How long any value lasts, for things that go stale whoever said them.</summary>
    public TimeSpan? Lifetime { get; init; }
}
