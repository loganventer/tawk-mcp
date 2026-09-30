namespace Tawk.Mcp.Core;

public sealed record HelloInfo(int Protocol, string Tawk, string Access, AccountInfo? Account, bool Connected);
