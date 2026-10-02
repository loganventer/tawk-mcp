using Tawk.Mcp.Core.Okf;

namespace Tawk.Mcp.Engines.Knowledge;

/// <summary>Turns stored knowledge into model-ready text. Formatting only; fencing is the caller's job.</summary>
public interface IKnowledgeFormatter
{
    string Observations(IReadOnlyList<ObservationView> observations, int hiddenSensitive, DateTimeOffset now);

    string Concept(
        OkfConcept concept, IReadOnlyList<RelatedConcept> relations, IReadOnlyList<ObservationView> observations, int hiddenSensitive, DateTimeOffset now);
}
