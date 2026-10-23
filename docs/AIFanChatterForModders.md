# AI Fan Chatter: fan posts about your mod's events

Your mod can ask AI Fan Chatter for fan posts about something it adds, such as a new event or building. Call `AIFanChatter.ModMoments.Trigger` where the thing happens. The posts appear in the game's social media feed over the next two in-game days, like posts about a new single.

## Example

```csharp
using HarmonyLib;
using System.Reflection;

public static class FanChatter
{
    // Null when AI Fan Chatter isn't installed, so your mod still runs without it
    private static readonly MethodInfo trigger = AccessTools.Method("AIFanChatter.ModMoments:Trigger");

    public static string Post(string kind, string label, string[] facts, int[] idolIds) =>
        trigger?.Invoke(null, new object[] { kind, label, facts, idolIds }) as string;
}

// Where the park opens, in a Harmony patch or other code the game runs:
FanChatter.Post(
    "the group opened a theme park",
    "the theme park opening",
    new[] { "Park name: Starlight Land", "Visitors on the first day: 12,000" },
    new[] { girl.id });
```

## Arguments

| Argument | What to pass |
|---|---|
| `kind` | What happened, in plain words. Required. |
| `label` | The name the player's notices use, such as "No fan posts for the theme park opening". `null` uses the kind. |
| `facts` | Details, each written as `Name: value`. `null` for none. |
| `idolIds` | The game's IDs (`data_girls.girls.id`) of the idols involved. `null` for none. |

The kind and facts can be in any language. The posts are written in the player's game language either way. The kind, the label and each fact can be at most 200 characters, with at most 20 facts. The AI also gets each idol's usual details and the group's details: the first idol's group, or the main group when no idol is named. Everything is sent to the player's AI service, so leave out anything private.

## When no posts are made

`Trigger` returns `null` when the request started. Otherwise it sends nothing and returns the reason, such as `this week's requests are used up`. It never throws.

Your events count toward the player's limit of requests per week, like every other event. An event that hits a limit is dropped, not saved for later. So call `Trigger` only for events fans would talk about, not routine ones.

If your mod can't work without AI Fan Chatter, you can instead reference its DLL and call `AIFanChatter.ModMoments.Trigger` directly. Your mod then fails to run without AI Fan Chatter installed.
