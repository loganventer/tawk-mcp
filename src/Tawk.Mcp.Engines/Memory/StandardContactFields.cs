namespace Tawk.Mcp.Engines.Memory;

/// <summary>
/// The profile fields tawk-mcp knows. Contact details follow JSContact (RFC 9553); communication fields follow politeness
/// and accommodation research; personality is kept to coarse bands because inferring it from chat is only weakly accurate.
/// </summary>
public static class StandardContactFields
{
    private static readonly string[] Bands = ["low", "mid", "high"];

    private static readonly string[] Relations =
    [
        "spouse", "partner", "child", "parent", "sibling", "grandparent", "grandchild", "in-law", "relative",
        "close-friend", "friend", "acquaintance", "colleague", "ex-colleague", "manager", "client", "service-provider",
        "church", "neighbour", "other",
    ];

    private static readonly string[] SchwartzValues =
    [
        "self-direction", "stimulation", "hedonism", "achievement", "power", "security", "conformity", "tradition",
        "benevolence", "universalism",
    ];

    public static IReadOnlyList<ContactFieldDefinition> All { get; } =
    [
        // Identity.
        new("nicknames", "identity", FieldKind.TextList, "Names they go by, with who uses them if it matters: [\"Ma\", \"Tokkie\"]."),
        new("pronouns", "identity", FieldKind.Text, "Their pronouns, only when known.") { Inferable = false },
        new("preferred_languages", "identity", FieldKind.TextList, "Languages in order of preference, as BCP 47 codes: [\"af\", \"en\"]."),
        new("birthday", "identity", FieldKind.Date, "2026-09-30, or --09-30 when the year is unknown."),
        new("anniversaries", "identity", FieldKind.Json, "Other dates worth remembering: [{\"kind\": \"wedding\", \"date\": \"2010-03-06\"}]."),
        new("occupation", "identity", FieldKind.Text, "What they do."),
        new("employer", "identity", FieldKind.Text, "Where they work."),
        new("home_area", "identity", FieldKind.Text, "Town or suburb, never a street address."),
        new("household", "identity", FieldKind.Text, "Who they live with, in a few words."),

        // Relationship.
        new("relation", "relationship", FieldKind.Choice, "What they are to the user.") { Choices = Relations },
        new("related_to", "relationship", FieldKind.Json, "Links to other contacts: [{\"jid\": \"...\", \"relation\": \"spouse\"}]."),
        new("closeness", "relationship", FieldKind.WholeNumber, "1 distant to 5 very close (social distance).") { Min = 1, Max = 5 },
        new("power_relative", "relationship", FieldKind.WholeNumber, "-2 they defer to the user, 0 equals, 2 the user defers to them.") { Min = -2, Max = 2 },
        new("known_since", "relationship", FieldKind.Text, "When they met, as a year or date."),
        new("how_met", "relationship", FieldKind.Text, "How they know each other."),

        // Communication.
        new("address_form", "communication", FieldKind.Choice, "How the user addresses them.")
        {
            Choices = ["jy", "u", "oom-tannie", "pa-ma", "first-name", "nickname", "sir-madam"],
        },
        new("they_call_me", "communication", FieldKind.Text, "What they call the user."),
        new("language_mix", "communication", FieldKind.Choice, "The language of their chat.") { Choices = ["af", "en", "mix"] },
        new("prefers_voice_notes", "communication", FieldKind.Boolean, "Whether they prefer voice notes to text."),
        new("usual_response_time", "communication", FieldKind.Text, "How quickly they usually answer."),
        new("good_hours", "communication", FieldKind.Text, "When they like to be messaged, such as 07:00-21:00."),
        new("brevity", "communication", FieldKind.WholeNumber, "1 long messages welcome to 5 keep it very short.") { Min = 1, Max = 5 },
        new("directness", "communication", FieldKind.WholeNumber, "1 soften everything to 5 be blunt.") { Min = 1, Max = 5 },
        new("humour_ok", "communication", FieldKind.Boolean, "Whether jokes land well."),
        new("crude_ok", "communication", FieldKind.Boolean, "Whether swearing and crude jokes are fine."),
        new("emoji_ok", "communication", FieldKind.Boolean, "Whether emoji are welcome."),
        new("topics_enjoy", "communication", FieldKind.TextList, "Things they like talking about."),
        new("topics_avoid", "communication", FieldKind.TextList, "Things to stay away from."),
        new("accommodation", "communication", FieldKind.Number, "0 keep the user's own voice, 1 mirror theirs.") { Min = 0, Max = 1 },
        new("approach", "communication", FieldKind.Text, "How best to approach them: what works, what to avoid."),
        new("stress_signals", "communication", FieldKind.Text, "How they come across when under pressure, so it is not misread."),

        // Personality: coarse on purpose, and only as a guide to tone.
        new("big_five_openness", "personality", FieldKind.Choice, "Big Five openness band.") { Choices = Bands },
        new("big_five_conscientiousness", "personality", FieldKind.Choice, "Big Five conscientiousness band.") { Choices = Bands },
        new("big_five_extraversion", "personality", FieldKind.Choice, "Big Five extraversion band.") { Choices = Bands },
        new("big_five_agreeableness", "personality", FieldKind.Choice, "Big Five agreeableness band.") { Choices = Bands },
        new("big_five_neuroticism", "personality", FieldKind.Choice, "Big Five neuroticism band.") { Choices = Bands },
        new("values_top", "personality", FieldKind.ChoiceList, "Up to three Schwartz values that move them.") { Choices = SchwartzValues, MaxItems = 3 },
        new("attachment_anxiety", "personality", FieldKind.Choice, "Attachment anxiety band, only as stated.") { Choices = Bands, Inferable = false },
        new("attachment_avoidance", "personality", FieldKind.Choice, "Attachment avoidance band, only as stated.") { Choices = Bands, Inferable = false },
        new("label_mbti", "personality", FieldKind.Text, "An MBTI type they identify with. A label, not a measurement.") { Inferable = false },
        new("label_enneagram", "personality", FieldKind.Text, "An Enneagram type they identify with.") { Inferable = false },
        new("label_disc", "personality", FieldKind.Text, "A DISC style they identify with.") { Inferable = false },
        new("label_love_language", "personality", FieldKind.Text, "A love language they identify with.") { Inferable = false },

        // Context: goes stale, so it lapses.
        new("current_situation", "context", FieldKind.Text, "What is going on for them right now.") { Lifetime = TimeSpan.FromDays(30) },
        new("follow_ups", "context", FieldKind.Json, "Things to ask about later: [{\"text\": \"ask how the interview went\", \"due\": \"2026-10-05\"}]."),
        new("gift_ideas", "context", FieldKind.TextList, "Gift ideas."),
        new("important_dates", "context", FieldKind.Json, "Other dates: [{\"what\": \"exam\", \"date\": \"2026-11-02\"}]."),

        // Sensitive: special personal information, only ever as the user states it.
        new("sensitive_notes", "sensitive", FieldKind.Text, "Health, beliefs or anything similar the user chooses to record. Hidden unless asked for.")
        {
            Inferable = false,
            Sensitive = true,
        },
    ];
}
