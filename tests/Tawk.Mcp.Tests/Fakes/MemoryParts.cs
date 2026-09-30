using Microsoft.Data.Sqlite;
using Tawk.Mcp.Core.Memory;
using Tawk.Mcp.Engines;
using Tawk.Mcp.Engines.Memory;
using Tawk.Mcp.Managers.Memory;
using Tawk.Mcp.ResourceAccess.Memory;

namespace Tawk.Mcp.Tests.Fakes;

/// <summary>The real memory stores, engines and managers over a database in a temporary folder.</summary>
public sealed class MemoryParts : IDisposable
{
    public MemoryParts(MemoryMode mode = MemoryMode.Write)
    {
        Folder = Path.Combine(Path.GetTempPath(), "tawk-memory-" + Guid.NewGuid().ToString("N")[..8]);
        Connections = new SqliteConnectionFactory(Path.Combine(Folder, "tawk-mcp", "memory.db"), new SqliteSchemaMigrator());
        Categories = new SqliteCategoryStore(Connections);
        Voices = new SqliteVoiceStore(Connections);
        Contacts = new SqliteContactStore(Connections);
        Templates = new SqliteTemplateStore(Connections);
        Guard = new MemoryWriteGuard(mode);
        Ensurer = new CategoryEnsurer(Categories);
        Selector = new VoiceSelector(Voices, Contacts, new VoiceResolver());
        Checker = new VoiceChecker(
            Extractor,
            [
                new ForbiddenPatternRule(), new CaseRule(), new EmojiRule(), new LengthRule(), new LanguageRule(),
                new AddressFormRule(), new RequiredMarkerRule(), new GreetingSignoffRule(), new BaselineDeviationRule(),
            ]);
        Catalog = new ContactFieldCatalog(
            StandardContactFields.All,
            [
                new TextValueValidator(), new TextListValueValidator(), new NumberValueValidator(), new BooleanValueValidator(),
                new ChoiceValueValidator(), new DateValueValidator(), new JsonValueValidator(),
            ]);
        CategoryManager = new CategoryManager(Categories, Formatter, Guard);
        VoiceManager = new VoiceManager(
            Voices, Ensurer, Chats, Selector, Checker, new VoiceGuideImporter(), new StyleBaselineCalculator(Extractor), Formatter, Fence, Guard, Clock);
        Profiles = new ContactProfileManager(Contacts, Voices, Ensurer, Chats, Catalog, Formatter, Fence, Guard, Clock);
        TemplateManager = new TemplateManager(
            Templates, Contacts, Voices, Ensurer, Chats, new TemplateRenderer(), Selector, Checker, Formatter, Fence, Guard, Clock);
    }

    public string Folder { get; }

    public ManualTimeProvider Clock { get; } = new();

    public FakeChatSource Chats { get; } = new FakeChatSource()
        .Add("27820000001@s.whatsapp.net", "Anneke Venter")
        .Add("27820000002@s.whatsapp.net", "Oom Stoffel")
        .Add("27820000003@s.whatsapp.net", "Neal Titus");

    public StyleFeatureExtractor Extractor { get; } = new();

    public UntrustedTextFence Fence { get; } = new();

    public MemoryFormatter Formatter { get; } = new();

    public SqliteConnectionFactory Connections { get; }

    public SqliteCategoryStore Categories { get; }

    public SqliteVoiceStore Voices { get; }

    public SqliteContactStore Contacts { get; }

    public SqliteTemplateStore Templates { get; }

    public MemoryWriteGuard Guard { get; }

    public CategoryEnsurer Ensurer { get; }

    public VoiceSelector Selector { get; }

    public VoiceChecker Checker { get; }

    public ContactFieldCatalog Catalog { get; }

    public CategoryManager CategoryManager { get; }

    public VoiceManager VoiceManager { get; }

    public ContactProfileManager Profiles { get; }

    public TemplateManager TemplateManager { get; }

    public void Dispose()
    {
        Connections.Dispose();
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(Folder))
        {
            Directory.Delete(Folder, true);
        }
    }
}
