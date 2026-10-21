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
| `{fan_types}` | The list of the 12 fan accounts, one per line, from your game. Each line gives the account's code (such as `F_HC_T`), what kind of fan it is (such as devoted teen girl), its name in the game's feed, and that kind of fan's current opinion of your group as a percentage. | Your text must still give the AI the 12 codes. A post with any other code gets a random one, and the feed picks each fan's picture from the code. |
| `{format}` | The reply format the mod can read: an example of posts, each with a fan code, a text and replies. | Keep it. If it's missing, the mod adds it at the end. |

## Placeholders in the user message

The mod fills each of these in with information from your game, about the moment the posts are about. If you type your own text in place of one, that text is sent instead of the game's information.

The group these parts call "the group" is the group the moment is about:
- for a single, the group that released it;
- for a concert, the main group;
- for a graduation, the graduating idol's group;
- for a random event, the group of the idol it's mainly about, or the main group when no idol is involved.

### `{event}`

What kind of moment it is, such as `Event: the group released a new single`.

### `{facts}`

The moment's own details, one per line.

- **A single:**
  - its title, center and members, and its genre, lyrics and choreography;
  - its sales, and its quality and fan satisfaction, both as percentages;
  - its marketing: each promotion by the game's name for it, such as Group Handshake Event. The game rolls the outcome of a risky promotion (a campaign, a fake scandal or a daring PV), and that outcome follows its name in parentheses: success, great success, failure or major failure.
- **A concert:**
  - its venue;
  - its attendance, as the people who came out of the venue's seats, such as `9200 out of 10000 seats`;
  - its hype, as a percentage that can go up to 200%;
  - its ticket price;
  - its set list, in order, one item per line. Each song comes with its genre, lyrics, choreography and the idol at its center, and each talk break with its idols.
- **A graduation:**
  - the idol, and what she did after graduating, when the game says;
  - the idols she was bullied by (`Suspected to be bullied by`) and the idols she bullied (`Suspected to have bullied`). Only bullying that the player had found out about is named, and idols who had stopped bullying her are left out. Each line is left out when it names no one;
  - her singles as center, up to her 5 best-selling ones, best-selling first. The list is left out when she was never a center.
- **A random event:**
  - what happened, the details, the agency's response, the outcome, and who was involved.
  - The game writes its events to the player. When the details, the response or the outcome say "you", a line `You: the group's producer (name)` tells the AI who that is, with the player's name as the game shows it.

### `{agency}`

The group and the agency, one fact per line:

- the group's name, and the main group's name when it's a sister group;
- the number of members, or for a graduation the members left after she graduated;
- when the group was formed, how many singles it has released, and the three members with the most fans. For a graduation, the graduating idol's friends (the other members of her clique) take the place of the three members with the most fans, on one line per group, such as `Friends in Aozora Chapter`, her own group first. They're left out when she wasn't in a clique or the player hadn't found out about it;
- the reputation from scandals, in words;
- the concept, from the agency's policies;
- the policies that matter to this kind of moment:
  - handshake event security, for a single promoted with a handshake event;
  - stage costumes and performance style, for a concert;
  - the dating rule, for a random event;
- the date, with its day of the week.

### `{members}`

One line for each idol the moment is about, most involved first: up to 5 for a single, 5 for a concert and 3 for a random event, and the graduating idol for a graduation.

Each line gives, in this order:

1. her trait;
2. up to 2 trivia facts, only for the first 2 idols;
3. her scandal record in words, when she has one;
4. her age;
5. her mental stamina and her stamina, each only when it's 50 or lower;
6. the skills where she alone is the best or the worst in her group, such as `Top dance skill in the group`;
7. her time in the group, her place in the last election, her place by fans in the group, and how many of the group's singles she's in;
8. `Member of sister group (name)` or `Member of main group (name)`, when she isn't in the group the moment is about.

**Places:**
- **What's named:** a place in the election or by fans is only named when it's first, second, second from the bottom or the bottom.
- **Small groups:** second and second from the bottom are only named in a group of 5 or more, because in a smaller group they're the middle places. A group of 2 to 4 gets only first and last, and an idol alone gets no place by fans.
- **Another group:** an idol from another group has her time and her place by fans given in her own group, by name.

The group's own totals are in `{agency}`, so the lines leave them out.

### `{history}`

Earlier results to compare with. Empty when there's nothing earlier.

- **A single:** the group's last 3 singles, each with its genre, lyrics, choreography, sales, quality, fan satisfaction and release date, then how many singles came before and the best-selling one.
- **A concert:** the previous concert's venue, attendance and date, how many concerts came before, and the biggest audience so far.
- **A graduation:** the previous graduate from the same group and when she left, and how many of the group's idols graduated before her. When she's the group's first graduate, it says so.
- **A random event:** how often the same event happened before, and when it last did.

### `{feed}`

The latest 8 posts on the game's feed, oldest first, without their replies. It includes posts the mod has queued that haven't appeared yet. Empty when the feed has no posts.

## Example

This is a prompt the mod built for a concert. The agency in it is made up: it's the one the mod's report tool uses, so its names and numbers aren't from a real save. The posts in the feed part are posts the agency starts with.

### The system message

The text the mod ships with, `AIFANCHATTER__PROMPT__SYSTEM`, is shown here with its `\n` line breaks as new lines:

```text
You write fan posts for the social media feed of an idol management game. The agency, its idols and its fans are fictional.

Fan accounts. Use these exact codes as "user":
{fan_types}

Rules:
- Write {min} to {max} posts reacting to the event the user describes. Each post can have 0 to {replies} replies from other fans but not themselves.
- Keep every post and reply under {length} characters, in the voice of a fan online.
- Mix the kinds of fans, and let them react to each other in the replies.
- Every post reacts to the new event. The background and the earlier posts are history, given only for context: fans can refer back to them or compare with them, but they are never the subject of a post, and earlier posts are never repeated.
- Use plain text without emoji: the game can't show them.
- Write in {language}.
- Match each fan's tone to their opinion of the group: low is critical or disappointed, around 50% is mixed, high is enthusiastic.
- Don't quote stats or percentages, but approximate sales and attendance numbers can be mentioned where it fits.
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

- `{fan_types}` became the fan accounts:

  ```text
  - M_C_T: casual teen boy (shown as @kazubuta), opinion of the group: 33%
  - M_C_YA: casual young adult man (shown as @satoshi_cars), opinion of the group: 50%
  - M_C_A: casual adult man (shown as @takuya_nozawa_72), opinion of the group: 67%
  - M_HC_T: devoted teen boy (shown as @pizzakk), opinion of the group: 67%
  - M_HC_YA: devoted young adult man (shown as @shinji1602), opinion of the group: 83%
  - M_HC_A: devoted adult man (shown as @heihachiro1960), opinion of the group: 100%
  - F_C_T: casual teen girl (shown as @ICECREAMNAKAI), opinion of the group: 0%
  - F_C_YA: casual young adult woman (shown as @machi_panpkn), opinion of the group: 17%
  - F_C_A: casual adult woman (shown as @wanitamaru), opinion of the group: 33%
  - F_HC_T: devoted teen girl (shown as @kazunyan_dm), opinion of the group: 33%
  - F_HC_YA: devoted young adult woman (shown as @sakisuki69), opinion of the group: 50%
  - F_HC_A: devoted adult woman (shown as @wakana_1967), opinion of the group: 67%
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
Venue: Open Air Stage
Attendance: 9200 out of 10000 seats
Hype: 156%
Ticket price: 4800 yen
Set list, in order:
- "Signal Fire" (Rock, Friends lyrics, Energetic choreography), center Takeda Rin
- "Platform 7 Promise" (Pop, Love lyrics, Cute choreography), center Hoshino Mirai
- "Neon Timetable" (EDM, Partying lyrics, Energetic choreography), center Hina (Aoki Hinata)
- Talk break with Fujii Mao, Shimizu Kaede and Nishida Koharu
- "Cloudline" (EDM, Love lyrics, Cool choreography), center Kato Hikari
- "Goodbye, Ticket Gate" (Ballad, Regret lyrics, Elegant choreography), center Mori Sayaka
- "Under the Overpass" (Funk, Anxiety lyrics, Cool choreography), center Ogawa Rena
- "Spring Express" (Pop, Happiness lyrics, Cute choreography), center Kudo Nanami
- Talk break with Hina (Aoki Hinata) and Ishikawa Yui
- "Rush Hour Rebellion" (Rock, Rebelling lyrics, Cool choreography), center Takeda Rin
- "Midnight Platform" (Pop, Loneliness lyrics, Elegant choreography), center Hoshino Mirai
- "Departure Bell" (Pop, Friends lyrics, Cute choreography), center Hoshino Mirai
Group: Kirameki Station
Members in the group: 14
Group formed: 2 years ago
Singles released by the group: 12
Most popular members: Hoshino Mirai, Hina (Aoki Hinata), Takeda Rin
Group reputation: a few scandals
Group concept: cute, traditional idol image
Stage costumes: short skirts with safety shorts
Performance style: energy and enthusiasm
Date: Sunday 14 September 2025

The idols in the new event:
Hoshino Mirai: Trait: Prodigy. Practises scales in the stairwell before every show. Owns more than forty pairs of headphones. Age 19. Top vocal skill in the group. A member for 2 years, 1st in the last election, the most fans in the group, in 11 of the group's singles (center of 4).
Takeda Rin: Trait: Steadfast. Captained her high school's volleyball team. Writes every fan letter's sender a short reply. Age 21. Top dance skill in the group. A member for 2 years, in 9 of the group's singles (center of 2).
Hina (Aoki Hinata): Trait: Photogenic. Age 17. Cutest and prettiest in the group. A member for 2 years, 2nd in the last election, the second most fans in the group, in 8 of the group's singles (center of 3).
Kato Hikari: Trait: Shameless. Age 20. Sexiest in Aozora Chapter. A member of Aozora Chapter for 11 of its 11 months, the most fans of 5 in Aozora Chapter, in 0 of the group's singles. Member of sister group Aozora Chapter.
Mori Sayaka: Trait: Perfectionist. A minor scandal in her past. Age 23. Smartest in the group. A member for 2 years, in 8 of the group's singles (center of 1).

History, for context only (not what the posts are about):
Previous concert: Concert Hall, attendance 1500 out of 1500 seats, held 6 months ago
Concerts held before this one: 4
Biggest audience at an earlier concert: 7600

History: earlier posts on the feed, oldest first, for context only. Don't reply to them or repeat them:
- M_HC_YA: Handshake tickets for Midnight Platform secured. Row 3 at the open air show too, best week ever
- F_C_T: Hina's photo book preview is so cute I can't
- M_C_A: Kirameki Station on the morning radio. Mirai sings live better than on the CD
- F_HC_A: Missed the Aozora Chapter stream. Did Kaho say anything about her break?
- M_C_T: why are the open air tickets so expensive lol
```

Each placeholder was filled in as follows:

- **`{event}`:** the line `Event: the group held a concert`.
- **`{facts}`:** the lines from `Venue` to the end of the set list.
- **`{agency}`:** the lines from `Group` to `Date`.
- **`{members}`:** the header `The idols in the new event:` from `AIFANCHATTER__PROMPT__MEMBERS`, then one line per idol.
  - Hina has a nickname, so her full name follows it.
  - Kato Hikari is from the sister group, so her time and her place by fans are given in that group, by name, and her line ends by naming it.
  - Takeda Rin was 3rd in the election and 3rd by fans, so neither place is named.
- **`{history}`:** the header from `AIFANCHATTER__PROMPT__HISTORY`, then the group's earlier concerts.
- **`{feed}`:** the header from `AIFANCHATTER__PROMPT__FEED`, then the earlier posts.

## What the mod does whatever the prompt says

- **Fan codes:** it uses only the 12 fan codes, and gives a random one to a post with another code.
- **Limits:** it keeps at most 6 posts and 3 replies per post, and cuts texts over 200 characters.
- **Replies to themselves:** it drops a reply from the fan who wrote the post.
- **One post:** when the prompt asks for several posts through `{min}` and the reply has only one, it sends the request once more. If that reply has only one post too, it shows it, and doesn't send one-post replies again until you change a setting.
- **Emoji:** it takes them out, because the feed can't show them.
- **Timing:** it spreads the posts over the two in-game days after the moment, in order, each at a random time in its share of the two days. Each reply comes with its post or up to 6 hours after it. Posts whose time has already passed when the AI's reply arrives appear at once.
- **Reply format:** when your AI service supports it, it asks for replies in the reply format, so the replies always come back in that shape.
- **Reasoning:** it asks models that reason before answering to reason only a little (low effort), which makes requests cheaper. A service ignores the setting if it doesn't know it. A service or model that rejects the request gets it again without the low effort, and if it rejects that too, without the reply format. What it rejected isn't asked for again until you change a setting.
