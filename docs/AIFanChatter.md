# AI Fan Chatter: changing the prompt

The prompt is the text AI Fan Chatter sends to your AI service with each request. It tells the AI who the fans are, what happened in your game and how to write the posts. You don't need to change it to play. This guide is for players and modders who want to reword it, change its rules or leave parts out.

The prompt is a set of texts in the mod's `JSON/Constants/constants.json`. Each text has an id that starts with `AIFANCHATTER__PROMPT__`. You can see the texts the mod ships with in [constants.json](../mods/AI%20Fan%20Chatter/assets/JSON/Constants/constants.json).

## Where to make your changes

Make your changes in a small mod of your own, not in AI Fan Chatter's files. Steam replaces AI Fan Chatter's files whenever the mod updates, so changes made there are lost. Your own mod keeps them.

1. In the game's local mods folder (`%USERPROFILE%\AppData\LocalLow\Glitch Pitch\Idol Manager\Mods`), make a folder for your mod, for example `My Fan Chatter Prompt`.
2. In it, add an `info.json`. Give it a `JSONLoadOrder` higher than 0, so its texts load after AI Fan Chatter's and replace them. This needs the [JSON Load Order](JSONLoadOrder.md) mod.

   ```json
   {
     "Title": "My Fan Chatter Prompt",
     "Description": "My changes to AI Fan Chatter's prompt.",
     "Author": "Me",
     "Version": "1.0",
     "Tags": ["modding"],
     "JSONLoadOrder": 100
   }
   ```

3. Add `JSON/Constants/constants.json` with only the texts you change. A text you leave out keeps AI Fan Chatter's version. This example makes the feed section's header shorter:

   ```json
   [
     {
       "id": "AIFANCHATTER__PROMPT__FEED",
       "text": "Earlier posts, for context only:\n{lines}"
     }
   ]
   ```

4. Turn your mod on in the game's mod list, and restart the game.

## How the texts work

- **Placeholders:** text in curly braces, such as `{max}` or `{feed}`, is a placeholder. Before sending the prompt, the mod fills it in. Everything else is sent as you wrote it.
- **What the mod fills in:** a placeholder isn't a setting. It is filled in with either the mod's default value, such as 3 for `{min}`, or information from your game, such as the recent posts for `{feed}`. The tables below give what each one is filled in with.
- **Using your own value:** type it in place of the placeholder. For example, `Write 2 to 4 posts` in place of `Write {min} to {max} posts` asks for 2 to 4 posts instead of the default 3 to 6. To leave a part out, delete its placeholder.
- **Unknown braces:** braces the mod doesn't know, such as `{something}`, are sent as typed.
- **Empty parts:** a line that holds only placeholders with nothing to show is left out, such as `{history}` for a group's first single. Runs of blank lines are reduced to one.
- **Line breaks and quotes:** inside a text, write a line break as `\n` and a double quote as `\"`. The game's JSON reader drops real line breaks.
- **Empty texts:** the game's JSON reader can't read an empty text, so the mod treats one as missing (see "Missing texts" below).

## The texts

| Id | What it is |
|---|---|
| `AIFANCHATTER__PROMPT__SYSTEM` | The system message: the AI's instructions, the same for every request. |
| `AIFANCHATTER__PROMPT__USER` | The user message: what happened, sent fresh for each request. |
| `AIFANCHATTER__PROMPT__MEMBERS` | The header for the idols' lines. `{lines}` marks where the lines go. |
| `AIFANCHATTER__PROMPT__HISTORY` | The header for earlier results. `{lines}` marks where the lines go. |
| `AIFANCHATTER__PROMPT__FEED` | The header for the recent posts on the feed. `{lines}` marks where the posts go. |

**Headers:**
- **When a header is used:** only when its part has something to show, so a header never appears on its own.
- **Without `{lines}`:** if a header has no `{lines}`, the lines go after it on a new line.
- **No header at all:** to send a part without a header, set its header text to just `{lines}`. A missing or empty header has the same effect.
- **Headers for other parts:** the `{facts}` and `{agency}` parts can have headers too, with the ids `AIFANCHATTER__PROMPT__FACTS` and `AIFANCHATTER__PROMPT__AGENCY`. The mod ships without them.

### Missing texts

- **System or user message:** if no mod has either one, the mod uses a plain version with only the parts the replies depend on, and writes a line to the game's log.
- **The reply format:** if the system message has no `{format}`, the mod adds the reply format at the end, because it can't read the replies without it.

## Placeholders in the system message

| Placeholder | The mod fills in | If you type your own value instead |
|---|---|---|
| `{min}` | The default fewest posts per request: 3. | Any number. While `{min}` is in the text, a reply with only one post is taken as a failed reply and the mod sends the request once more. If you type your own number, it doesn't. |
| `{max}` | The default most posts per request: 6. | A smaller number works. The mod keeps at most 6 posts, so a larger number doesn't add any. |
| `{replies}` | The default most replies per post: 3. | A smaller number works. The mod keeps at most 3 replies per post. |
| `{length}` | The default most characters per post or reply: 100. | Any number up to 200. The mod cuts any text over 200 characters, so a larger number only gets texts cut short. |
| `{language}` | The game's language setting: English, Japanese, Simplified Chinese, Brazilian Portuguese or Russian, or a translation mod's language code. | A language name, to always get posts in that language. |
| `{fan_types}` | The list of the 12 fan accounts, one per line, from your game. Each line gives the account's code (such as `F_HC_T`), what kind of fan it is (such as devoted teen girl), its name in the game's feed, and that kind of fan's current opinion of your group out of 100. | Your text must still give the AI the 12 codes. A post with any other code gets a random one, and the feed picks each fan's picture from the code. |
| `{format}` | The reply format the mod can read: an example of posts, each with a fan code, a text and replies. | Keep it. If it's missing, the mod adds it at the end. |

## Placeholders in the user message

The mod fills each of these in with information from your game, about the moment the posts are about. If you type your own text in place of one, that text is sent instead of the game's information.

The group these parts call "the group" is the group the moment is about:
- for a single, the group that released it;
- for a concert, the main group;
- for a graduation, the graduating idol's group;
- for a random event, the group of the idol it's mainly about, or the main group when no idol is involved.

| Placeholder | The mod fills in |
|---|---|
| `{event}` | What kind of moment it is, such as `Event: the group released a new single`. |
| `{facts}` | The moment's own details, one per line. For a single: its title, center, members, genre, lyrics, choreography, sales, quality, fan satisfaction, new fans and marketing. For a concert: its title, venue, audience, attendance, hype, whether it made a profit, its performers, and its set list in order: each song with its genre, lyrics, choreography and the idol at its center, and each talk break with its idols. For a graduation: the idol and what she did after graduating, when the game says. For a random event: what happened, the details, the agency's response, the outcome and who was involved. |
| `{agency}` | The group and the agency. The group's name, and the main group's name when it's a sister group. The number of members, and the three with the most fans. The agency's fame level and total fans, its reputation from scandals in words, and the date. |
| `{members}` | One line for each idol the moment is about, most involved first: up to 7 for a single, 5 for a concert and 3 for a random event, plus the graduating idol for a graduation. Each line gives her trait, up to 2 trivia facts (only for the first 2 idols), her scandal record in words, her age, her mental stamina and stamina, her time in the group, her place in the last election, her place by fans in the group, and how many of the group's singles she's in. An idol from another group has her time and place given in her own group, by name. |
| `{history}` | Earlier results to compare with: the group's last 3 singles, each with its genre, lyrics, choreography and results, and its best-selling one, the previous concert and the biggest audience so far, the previous graduation, or how often the same random event happened before. Empty when there's nothing earlier. |
| `{feed}` | The latest 15 posts on the game's feed, oldest first, with up to 3 replies each. It includes posts the mod has queued that haven't appeared yet. Empty when the feed has no posts. |

## Example

This is a real prompt the mod sent, for a concert in a test save.

- **The idols' names** are in kanji because that save uses the Japanese Name Generator mod.
- **The earlier posts** in the feed part are ones the AI wrote for a single released just before.

### The system message

The text the mod ships with, `AIFANCHATTER__PROMPT__SYSTEM`, is shown here with its `\n` line breaks as new lines:

```text
You write fan posts for the social media feed of an idol management game. The agency, its idols and its fans are fictional.

Fan accounts. Use these exact codes as "user":
{fan_types}

Rules:
- Write {min} to {max} posts reacting to the event the user describes. Each post can have 0 to {replies} replies from other fans.
- Keep every post and reply under {length} characters, in the voice of a fan online.
- Mix the kinds of fans, and let them react to each other in the replies.
- Every post reacts to the new event. The background and the earlier posts are history, given only for context: fans can refer back to them or compare with them, but they are never the subject of a post, and earlier posts are never repeated.
- Use plain text without emoji: the game can't show them.
- Write in {language}.
- Match each fan's tone to their opinion of the group: low is critical or disappointed, around 50 is mixed, high is enthusiastic.
- Don't quote exact stats. Exact sales and attendance can be quoted where it fits.
- Stamina and mental stamina aren't public: fans only notice signs, such as an idol looking tired. Scandals are public, and fans know about them.

Reply with only this JSON and nothing else:
{format}
```

When the mod sent it, it filled in these values:

- `{min}` became 3, `{max}` became 6, `{replies}` became 3 and `{length}` became 100.
- `{language}` became English.
- `{format}` became the reply format:

  ```text
  {"posts":[{"user":"F_HC_T","text":"...","replies":[{"user":"M_C_A","text":"..."}]}]}
  ```

- `{fan_types}` became the fan accounts from the save:

  ```text
  - M_C_T: casual teen boy (shown as @kazubuta), opinion of the group: 0/100
  - M_C_YA: casual young adult man (shown as @satoshi_cars), opinion of the group: 50/100
  - M_C_A: casual adult man (shown as @takuya_nozawa_72), opinion of the group: 100/100
  - M_HC_T: devoted teen boy (shown as @pizzakk), opinion of the group: 33/100
  - M_HC_YA: devoted young adult man (shown as @shinji1602), opinion of the group: 60/100
  - M_HC_A: devoted adult man (shown as @heihachiro1960), opinion of the group: 67/100
  - F_C_T: casual teen girl (shown as @ICECREAMNAKAI), opinion of the group: 0/100
  - F_C_YA: casual young adult woman (shown as @machi_panpkn), opinion of the group: 50/100
  - F_C_A: casual adult woman (shown as @wanitamaru), opinion of the group: 100/100
  - F_HC_T: devoted teen girl (shown as @kazunyan_dm), opinion of the group: 33/100
  - F_HC_YA: devoted young adult woman (shown as @sakisuki69), opinion of the group: 60/100
  - F_HC_A: devoted adult woman (shown as @wakana_1967), opinion of the group: 67/100
  ```

### The user message

The text the mod ships with, `AIFANCHATTER__PROMPT__USER`:

```text
The new event, which every post reacts to:
{event}
{facts}
{agency}

{members}

{history}

{feed}
```

What the mod sent:

```text
The new event, which every post reacts to:
Event: the group held a concert
Title: Concert [Stadium]
Venue: Stadium
Audience: 15000
Attendance: 100%
Hype (0 to 200): 199
Made a profit: yes
Performers: 中澤 実香, 村上 映子, 望月 有紗, 小澤 のぞみ, 今井 康子, 吉川 心
Set list 1: "私 を巡って" (Metal, Regret lyrics, Cute choreography), center 中澤 実香
Set list 2: "Woman Of A Fool" (Metal, Regret lyrics, Cute choreography), center 村上 映子
Set list 3: "He knows I Like To Party" (Metal, Regret lyrics, Cute choreography), center 望月 有紗
Set list 4: "Honey, Put Your Hands Up" (Metal, Regret lyrics, Cute choreography), center 村上 映子
Set list 5: "Music Of A Promise" (Metal, Regret lyrics, Cute choreography), center 小澤 のぞみ
Set list 6: "Night Of Days" (Metal, Regret lyrics, Cute choreography), center 村上 映子
Set list 7: "Loved By Your Voice" (Metal, Regret lyrics, Cute choreography), center 今井 康子
Set list 8: "Darling, Get Out" (Metal, Regret lyrics, Cute choreography), center 小澤 のぞみ
Set list 9: "Respect For Tomorrow" (Metal, Regret lyrics, Cute choreography), center 吉川 心
Set list 10: "Magic Of Ice" (Metal, Regret lyrics, Cute choreography), center 吉川 心
Group: Sakura Drops
Members in the group: 11
Most popular members: 吉川 心, 望月 有紗, 中澤 実香
Agency fame level (0 to 10): 10
Total fans: 678748
Agency reputation: no scandals
Date: 24 March 2024

The idols in the new event:
村上 映子: Trait: Snitch. Doesn't like carbonated drinks. No scandals. Age 25. Mental stamina 100/100, stamina 92/100. A member of Hoshizora Lane for 18 of its 18 months, 1st of 9 in the last election, the most fans of 4 in Hoshizora Lane, in 8 of the group's 17 singles.
小澤 のぞみ: Trait: Hypochondriac. No scandals. Age 17. Mental stamina 100/100, stamina 92/100. A member for 19 months of the group's 2 years, 7th of 9 in the last election, 5th most fans of 11 in the group, in 7 of the group's 17 singles (center of 3).
吉川 心: Trait: Steadfast. No scandals. Age 15. Mental stamina 100/100, stamina 92/100. A member for 2 of the group's 2 years, 8th of 9 in the last election, the most fans of 11 in the group, in 14 of the group's 17 singles (center of 3).
中澤 実香: Trait: Secretive. No scandals. Age 22. Mental stamina 100/100, stamina 92/100. A member for 20 months of the group's 2 years, 2nd of 9 in the last election, 3rd most fans of 11 in the group, in 9 of the group's 17 singles (center of 2).
望月 有紗: Trait: Asexual. No scandals. Age 18. Mental stamina 100/100, stamina 92/100. A member for 2 of the group's 2 years, 4th of 9 in the last election, 2nd most fans of 11 in the group, in 15 of the group's 17 singles (center of 4).

History, for context only (not what the posts are about):
Previous concert: Concert [Open Air Stage]
Previous concert venue: Open Air Stage
Previous concert audience: 5000
Previous concert attendance: 100%
Previous concert held: 21 months ago
Concerts held before this one: 3
Biggest audience at an earlier concert: 5000

History: earlier posts on the feed, oldest first, for context only. Don't reply to them or repeat them:
- F_HC_A: DJ, Never Let Me Go is out! Metal sound, cute choreo, and Sakura Drops never misses. So proud.
  - reply from M_C_A: Agree, the choreo is so cute. Sales are lower than before though, hope it picks up.
- M_C_T: 10000 sales? Same metal and regret lyrics again. No marketing at all, why would anyone buy it?
  - reply from F_HC_T: Sales are low but the song is fine. Stop being so negative kazubuta.
- F_C_YA: DJ, Never Let Me Go is decent. The cute choreo is fun, but I wish the lyrics would change a bit.
- M_HC_YA: 望月 有紗's center part in DJ, Never Let Me Go is great. 吉川 心's vocals shine too.
  - reply from F_C_A: Agreed, 望月 有紗 looks so good on stage. The sales are a surprise though.
```

Each placeholder was filled in as follows:

- **`{event}`:** the line `Event: the group held a concert`.
- **`{facts}`:** the lines from `Title` to the last `Set list` line.
- **`{agency}`:** the lines from `Group` to `Date`.
- **`{members}`:** the header `The idols in the new event:` from `AIFANCHATTER__PROMPT__MEMBERS`, then one line per idol.
  - 村上 映子 is from the sister group, so her time and her place by fans are given in that group, by name.
- **`{history}`:** the header from `AIFANCHATTER__PROMPT__HISTORY`, then the group's earlier concerts.
- **`{feed}`:** the header from `AIFANCHATTER__PROMPT__FEED`, then the earlier posts with their replies.

## What the mod does whatever the prompt says

- **Fan codes:** it uses only the 12 fan codes, and gives a random one to a post with another code.
- **Limits:** it keeps at most 6 posts and 3 replies per post, and cuts texts over 200 characters.
- **One post:** when the prompt asks for several posts through `{min}` and the reply has only one, it sends the request once more. If that reply has only one post too, it shows it, and doesn't send one-post replies again until you change a setting.
- **Emoji:** it takes them out, because the feed can't show them.
- **Timing:** it releases the posts into the feed over the next in-game day or two.
- **Reply format:** when your AI service supports it, it asks for replies in the reply format, so the replies always come back in that shape.
