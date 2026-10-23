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
| `{fan_types}` | The fan accounts, one per line, from your game: one for each of the 12 kinds of fans that your agency has any fans of. Each line gives the account's code (such as `F_HC_T`), what kind of fan it is (such as hardcore teen girl), its name in the game's feed, and that kind of fan's current opinion of your group as a percentage. | Your text must still give the AI these codes. A post with any other code gets a random one of them, and the feed picks each fan's picture from the code. |
| `{format}` | The reply format the mod can read: an example of posts, each with a fan code, a text and replies. Its fan codes are ones your agency has. | Keep it. If it's missing, the mod adds it at the end. |

## Placeholders in the user message

The mod fills each of these in with information from your game, about the moment the posts are about. If you type your own text in place of one, that text is sent instead of the game's information.

The group these parts call "the group" is the group the moment is about:
- for a single, the group that released it;
- for a concert, a tour, an election or new members from an audition, the main group. A tour takes every active idol and an election ranks every idol, from every group;
- for a graduation or a graduation announcement, the graduating idol's group;
- for a show starting or ending, the group of the first idol in its cast, or the main group when the whole group is the cast;
- for a business contract or a hiatus, the idol's group;
- for a cancelled election, the group that released its voting single;
- for a random event, the group of the idol it's mainly about, or the main group when no idol is involved;
- for a dating scandal, the idol's group;
- for a policy change or a staff firing, the group of the first idol whose scandal points paid for it, or the main group.

### `{event}`

What kind of moment it is, such as `Event: the group released a new single`. The Refresh button's request, with no posts waiting, is `Event: everyday fan chatter about the group`, and its `{facts}` is one line, `What fans talk about`, listing topics a post can pick from.

### `{facts}`

The moment's own details, one per line.

- **A single:**
  - its title, center and members, and its genre, lyrics and choreography;
  - its sales, and its quality and fan satisfaction, both as percentages;
  - for an election's voting single, which election it's for (each CD comes with a ballot) and the concert its results follow; for the single the game makes from an election's results, which election set its lineup and who won it, plus the ranked idols who graduated since and so aren't in it;
  - its marketing: each promotion by the game's name for it, such as Group Handshake Event. The game rolls the outcome of a risky promotion (a campaign, a fake scandal or a daring PV), and that outcome follows its name in parentheses: success, great success, failure or major failure.
- **A concert:**
  - its venue;
  - its attendance, as the people who came out of the venue's seats, such as `9200 out of 10000 seats`;
  - its hype, as a percentage that can go up to 200%;
  - its ticket price;
  - its set list, in order, one item per line. Each song comes with its genre, lyrics, choreography and the idol at its center, and each talk break with its idols.
- **A tour** (posted once, after the player closes the tour's results):
  - the groups that went, main group first. The game sends every idol who isn't on hiatus, injured or leaving;
  - the idols who stayed home, each with the reason: `on hiatus`, `injured` or `announced her graduation`. Left out when everyone went;
  - its stops, in order, each with the place's name as the game shows it and the people who came out of the venue's seats, such as `Osaka: 7600 out of 8000 seats`. A place no earlier tour went to ends with `first visit`.
  - Places come from the game's text, so a mod that renames them (such as National Tour) changes them here too. New fans and money aren't sent: fans can't see them.
- **New members from an audition** (posted when the audition's window closes, only for auditions you launch and pay for; story auditions get no posts):
  - the new members, up to 10 names, then how many more;
  - the audition's scale as the game names it: Local, Regional or Nationwide.
  - How rare a find each idol was (her card's colour) isn't sent: only the player sees it.
- **A show starting** (posted when the player launches it and its first episode airs): its title, medium, genre and host as the game names them, and its cast (the whole group, a rotating cast, or the idols). Its viewers, budget, revenue and new fans aren't sent.
- **A show ending** (posted only for a show that aired for a year or more, counted from its first episode, with concept updates included; posted when the cancellation really ends it: at once for an internet show, at its next 12th episode for a TV or radio show): its title, medium, genre and host, its cast (for a rotating cast, the idols in its last episode), and how long it ran, such as `Ran for: 13 months, 60 episodes`. A show whose whole cast left gets no posts. Its viewers, budget, revenue and new fans aren't sent.
- **A business contract starting** (an ad or a TV drama, the 3-month deals; one-off photoshoots and variety appearances get no posts): the idol, the kind of contract, the brand (for an ad) or the studio or channel and the drama's title (for a TV drama), the image it uses, and its length. What it pays and costs her isn't sent.
- **The yearly awards show** (posted when the show's dialogue ends, only in years the agency was nominated):
  - the awards it won, by the game's titles, with the idol or single each is for, such as `Best Debut Idol: Kondo Tsubasa`;
  - the awards it was nominated for and lost, with the winner the show announced, such as `Gravure Queen: Ogawa Rena, lost to Saito Yuna of Starlight Ribbon`;
  - each win's acceptance speech, quoted as the show gave it, with who gave it. When the words weren't recorded, it says whom she thanked instead (the producer, Fujimoto, a staffer or another idol).
  - The awards' gameplay bonuses aren't sent: fans can't see them.
- **A concert with its election** (the game announces an election's results right after its concert): one moment, posted once the winner is revealed, with the concert's facts, then the election's. If the election never finishes, the concert goes out alone a day later.
- **An election** (posted once the winner is revealed):
  - its number, its broadcast, the voting single and the concert it followed;
  - how many idols it ranked;
  - its results, up to the first 10 places, one per line, each with the idol, her votes and how she moved since the last election (`up from 3rd`, `down from 1st`, `same place as last time` or `not ranked last time`). An idol from a sister group has her group's name after hers, such as `Kato Hikari of Aozora Chapter`. An idol who announced her graduation still takes part, and her line ends with `announced her graduation`.
  - **The biggest drop outside the top 10:** the idol ranked last time who fell furthest and isn't in the first 10, such as `Aoi, no longer ranked (was 3rd)`. The game keeps only the first 10 places, so an idol below them is no longer ranked. Graduated idols are left out.
  - **The biggest rise outside the top 10:** the idol below the first 10 who climbed furthest since last time. Only a mod that keeps more than 10 places (such as Extended SSK) makes this possible.
  - The first election has no movement and neither of these lines.
- **A graduation** (a regular graduation, on her graduation date, posts when her goodbye scene ends, so the choices made there are included; other graduations post at once):
  - the idol, and what she did after graduating, when the game says;
  - her staff job, when she stays at the agency as staff (`Stays at the agency as`), by title and what she does, as for a staff firing, such as `Choreographer (production staff)`;
  - who joined in her place, when she brought in her younger sister or a successor she chose (`Joined in her place`), such as `Uchida Hana, her younger sister`. The game puts her in the main group, which is named when the graduate was in a sister group;
  - the idols she was bullied by (`Suspected to be bullied by`) and the idols she bullied (`Suspected to have bullied`). Only bullying that the player had found out about is named, and idols who had stopped bullying her are left out. Each line is left out when it names no one;
  - her singles as center, up to her 5 best-selling ones, best-selling first. The list is left out when she was never a center.
- **A graduation announcement** (posted when the player confirms it; she can still be talked into staying before that): the idol, her graduation date and how far ahead it is, such as `Monday 24 June 2024, 3 months from now`, the bullying fans suspect and her singles as center, as for a graduation.
- **A hiatus starting** (posted when an idol goes on hiatus: from her menu, after a failed treatment, or through a dialogue):
  - the idol, and the reason announced, by her status before it: `an injury`, `health reasons, no details given` for depression (mental health is never named), or `no reason given`;
  - the business contracts the hiatus cancelled, and the shows on air she leaves. An injured idol lost both when she got hurt, so her hiatus lists neither;
  - when her last hiatus ended, when she had one before;
  - the bullying fans suspect and her singles as center, as for a graduation.
  - When she's expected back isn't sent: fans don't know.
- **A hiatus ending** (posted when she comes back, on the planned date or called back early; both read the same): the idol, the reason announced at the start, the bullying fans suspect and her singles as center.
- **A cancelled election** (posted only once its voting single is out: before that, fans haven't heard of it):
  - the election's number and the broadcast it was to have;
  - the voting single, how long ago it came out, its sales in CDs, and the money fans spent on it (the CDs sold times the price per CD);
  - its results concert: `still on without the results`, or `also cancelled` when cancelling the concert cancelled the election.
  - The game lowers hardcore and casual fans' opinion as it cancels, and the prompt's opinions already include that.
- **A random event:**
  - what happened, the details, the agency's response, the outcome, and who was involved;
  - the business contracts the answer cancelled (`Contracts cancelled because of this`), each by kind, client and idol. The game cancels them when the answer brings damages, or through the event itself. Left out when none were;
  - The game writes its events to the player. When the details, the response or the outcome say "you", a line `You: the group's producer (name)` tells the AI who that is, with the player's name as the game shows it.
  - Fans only post when the answer changed something the public sees: fans, fans' opinion, buzz, scandal points or fame. Answers that only cost money, stamina or relationships, or only lead to a dialogue or a later event, get no posts.
- **A paid policy change** (only changes that cost scandal points; a free change gets no posts): the policy, its old and new option, and the reason for the change, naming whose scandal points the player spent (idols, the group itself, or both). For each of those idols, her known friends (`Friends in <group>`) and known bullying (`Suspected to be bullied by`, `Suspected to have bullied`), labelled with her name when there are several.
- **A staff firing** (through the game's firing, which always costs scandal points; staff fired by story scenes get no posts): the staff member, their role with what they mostly did by their strongest skill (production staff, trainer, dealmaker, physical or mental health care, styling), their time at the agency, and the announced reason: blamed for the scandals of the idols or the group whose points the player spent, or "budget problems (ostensibly)" when the player accepted the staffer's deal to announce that. Production staff come with the agency's latest 3 singles released while they worked there; dealmakers with the agency's 3 biggest running business contracts, without their pay. The idols whose points were spent get the same lines as for a policy change, budget firings included, so fans can doubt the announcement.
- **A dating scandal** (the game's dating scandals option): what happened, without the game's question to the player, the agency's response, the outcome, and who was involved, with the same `You` line, and the business contracts the scandal cancelled (`Contracts cancelled because of this`). Fans only post when the outcome reached the public: paying off the blackmailer or the publication, the police catching the blackmailer, a blackmailer who was bluffing, and an editor who drops the story after a denial all get no posts.

### `{agency}`

The group and the agency, one fact per line:

- the group's name, and the main group's name when it's a sister group;
- the size of each group the moment changed, one line each, with its size now and before, such as `Members in Aozora Chapter: 4 now, down from 5`: after a graduation, new members from an audition, or a sister or successor joining. Moments that change no group's size leave it out;
- when the group was formed, how many singles it has released, and the three members with the most fans. For a graduation or a graduation announcement, the graduating idol's friends (the other members of her clique) take the place of the three members with the most fans, on one line per group, such as `Friends in Aozora Chapter`, her own group first. A hiatus gives the idol's friends the same way, after the three members with the most fans. Friends are left out when she wasn't in a clique or the player hadn't found out about it;
- the reputation from scandals, in words, except for a graduation or a graduation announcement, which are about one idol;
- the concept, from the agency's policies;
- the policies that matter to this kind of moment:
  - handshake event security, for a single promoted with a handshake event;
  - stage costumes and performance style, for a concert or a tour;
  - the dating rule, for a random event or a dating scandal;
- the date, with its day of the week.

### `{members}`

One line for each idol the moment is about, most involved first: up to 5 for a single, 5 for a concert and 3 for a random event, the graduating idol for a graduation (and the idol who joined in her place, with a new member's line), the idol for a dating scandal, and up to 5 idols whose scandal points paid for a policy change or a staff firing. A tour gets the 5 active idols with the most fans, from any group. New members from an audition get up to 5 lines, in the order they were hired; their lines leave out the time in the group, the election place, the place by fans and the singles, which are all empty on an idol's first day, and add her highest potential, such as `Highest potential: dance`: the skill she could grow furthest in, or every one tied for it. The awards show gets up to 5: the winners, then the idols who gave the agency's speeches, then the nominees who lost. A show gets up to 5 of its cast (the 5 with the most fans when the whole group is the cast), a business contract and a hiatus the idol, and a cancelled election the voting single's lineup. An election gets up to 5: the first 3 places, then the idols with the biggest drop and the biggest rise outside the top 10, then the 4th and 5th places. Their lines leave out the election place, which the results give.

Each line gives, in this order:

1. her trait;
2. up to 2 trivia facts, only for the first 2 idols;
3. her scandal record in words, when she has one;
4. her age, and `youngest in the group` or `oldest in the group` when she alone is, by birthday, in a group of 3 or more;
5. her mental stamina and her stamina, each only when it's 50 or lower. A hiatus leaves both out, so they can't hint at more than the announced reason;
6. the skills where she alone is the best or the worst in her group, such as `Top dance skill in the group`;
7. her time in the group, her place in the last election, her place by fans in the group, and how many of the group's singles she's in;
8. `Member of sister group (name)` or `Member of main group (name)`, when she isn't in the group the moment is about.

**Places:**
- **What's named:** a place in the election or by fans is only named when it's first, second, second from the bottom or the bottom.
- **Small groups:** second and second from the bottom are only named in a group of 5 or more, because in a smaller group they're the middle places. A group of 2 to 4 gets only first and last, and an idol alone gets no place by fans.
- **Another group:** an idol from another group has her time and her place by fans given in her own group, by name.

The group's own totals are in `{agency}`, so the lines leave them out.

**The Refresh button's request:** 5 idols of the main group, picked at random each time: one of the 3 with the most fans and 4 from the rest.

### `{history}`

Earlier results to compare with. Empty when there's nothing earlier.

- **A single:** the group's last 3 singles, each with its genre, lyrics, choreography, sales, quality, fan satisfaction and release date, then how many singles came before, and the best-selling one with how long ago it came out.
- **A concert:** the previous concert's venue, attendance and date, how many concerts came before, and the biggest audience so far, with how long ago that concert was.
- **A tour:** every place earlier tours went to, once each, at its latest visit, newest first, with the seats filled and how long ago, such as `Osaka: 7600 out of 8000 seats, 4 months ago`. Then how many tours there have been, this one included.
- **New members from an audition:** the idols who joined last before them, all hired on the same day, and how long ago. Graduated idols count.
- **A show starting or ending:** the shows cancelled in the past year, newest first, up to 3, each with its medium, genre, cast (up to 3 names, the whole group, or a rotating cast), episodes and when its last episode aired; other shows on air now, up to 3, with their medium, genre, cast and when each started; and, for a show starting, how many shows started before.
- **A business contract starting:** the idol's other running contracts, and how many deals of the same kind she took before, with how long ago the latest was.
- **The yearly awards show:** how many awards the agency won in earlier years, last year's wins by title, and how many shows it has been in, this one included.
- **An election:** the previous election, its winner and how long ago, then how many elections there have been, this one included.
- **A cancelled election:** the previous election, its winner and how long ago. It isn't counted: it never happened.
- **A graduation or a graduation announcement:** the previous graduate from the same group and how long ago she left, on one line, and how many of the group's idols graduated before her. When she's the group's first graduate, it says so.
- **A hiatus:** nothing. The game keeps only the end of her last hiatus, which `{facts}` gives.
- **A random event:** how often the same event happened before, and when it last did. For an event in a storyline, the text of the storyline's earlier events that happened, oldest first. The game doesn't keep which answer was given, so an earlier event is only included when every answer of it that leads on in the storyline had a public outcome, or the game posted about it. The answers themselves aren't included.
- **A dating scandal:** nothing. The game gives each idol at most one.
- **The Refresh button's request:** 3 picked at random each time from the latest single, an earlier single, the latest finished concert and a show on air, such as `Latest single: "Midnight Platform" (Pop, Loneliness lyrics, Elegant choreography): 104000 sales, quality 80%, fan satisfaction 82%, released 3 weeks ago`.
- **A paid policy change or a staff firing:** the latest scandal of each idol whose points paid for it, and of the group when its own points did: the text of her most recent event that can add scandal points. Dating scandals aren't in the game's event log, so they're never named.

### `{feed}`

The latest 8 posts on the game's feed, oldest first, without their replies, or the latest 20 for the Refresh button's request. It includes posts the mod has queued that haven't appeared yet. Empty when the feed has no posts.

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
  {"posts":[{"user":"M_C_YA","text":"...","replies":[{"user":"M_HC_A","text":"..."}]}]}
  ```

- `{fan_types}` became the fan accounts. The agency's idols have fans of 3 kinds, so there are 3 accounts:

  ```text
  - M_C_YA: casual young adult man (shown as @satoshi_cars), opinion of the group: 50%
  - M_HC_A: hardcore adult man (shown as @heihachiro1960), opinion of the group: 100%
  - F_C_T: casual teen girl (shown as @ICECREAMNAKAI), opinion of the group: 0%
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
Biggest audience at an earlier concert: 7600, held 12 months ago

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

- **Fewer than 100 fans:** it sends no requests while your agency has fewer than 100 fans, counting only idols who haven't graduated, as the game does.
- **Fan codes:** it uses only the codes of the kinds of fans your agency has, and gives a random one of them to a post with another code.
- **Limits:** it keeps at most 6 posts and 3 replies per post, and cuts texts over 200 characters.
- **Replies to themselves:** it drops a reply from the fan who wrote the post.
- **One post:** when the prompt asks for several posts through `{min}` and the reply has only one, it sends the request once more. If that reply has only one post too, it shows it, and doesn't send one-post replies again until you change a setting.
- **Emoji:** it takes them out, because the feed can't show them.
- **Timing:** it spreads the posts over the two in-game days after the moment, in order, each at a random time in its share of the two days. Each reply comes with its post or up to 6 hours after it. Posts whose time has already passed when the AI's reply arrives appear at once.
- **The Refresh button:** its label says how many posts are waiting, such as `Refresh feed (12 waiting)`, or `Refresh feed (AI request)` when none are and a press will send a request. It shows the next 5 waiting posts at once, each with its replies. With no posts waiting, it sends a request for everyday chatter and shows the posts as soon as they arrive. Neither shows a notification. The request isn't counted toward requests per week, the 30 seconds between requests or the 20 an hour, and it's sent even when requests per week is 0. It isn't sent while another request is running, or while requests are stopped or paused; the notice at the top of the tab says why. With fewer than 100 fans, a notice says nobody is posting about the group yet. The feed scrolls to the top to show the new posts. Posts that arrive after you turn the mod off are dropped.
- **Reply format:** when your AI service supports it, it asks for replies in the reply format, so the replies always come back in that shape.
- **Reasoning:** it asks models that reason before answering to reason only a little (low effort), which makes requests cheaper. A service ignores the setting if it doesn't know it. A service or model that rejects the request gets it again without the low effort, and if it rejects that too, without the reply format. What it rejected isn't asked for again until you change a setting.
- **An answer in the wrong field:** some providers send the whole answer in the reply's reasoning field and leave the answer itself empty. The mod uses the posts it finds there.

## When a request fails

The mod never sends a failed request again. A notice at the top of the Social Media tab, under the Refresh feed bar, says what happened, which service and model it was, which moment got no posts, and what you can do, and the tab gets its red dot. While requests are stopped or paused, a red notice says so until they run again. Other notices stay until you close them; the latest 3 are kept, and the same notice again moves to the top instead of showing twice. Notices aren't saved. What happens next depends on whose fault it was:

- **The model can't do the job:** it ran out of room for its answer (by reasoning or writing too much), sent an empty answer, or sent an answer that isn't in the format the mod asked for. The same model would keep failing and costing money, so requests stop. Choose another model, then press Apply in Mod Settings to start them again.
- **The settings are wrong:** the service rejected the API key or refused access to the model, the account is out of credit, the service doesn't know the model, or the Base URL leads to something that isn't an AI service. Requests stop. Fix the setting, or add credit, then press Apply in Mod Settings to start them again.
- **The service is busy or can't be reached:** no internet connection, a timeout, too many requests, the service is down, or a provider behind the service failed or had no room for the request (common with free models). Requests pause for 10 minutes, then go on. Press Apply in Mod Settings to try again at once.
- **The model declined, or the service's content filter stopped the answer:** only that moment gets no posts, because it depends on what the moment is about. Requests go on.

Pressing Apply starts stopped or paused requests again even when you haven't changed anything, and tests the connection, so a notification tells you at once whether it works now. The connection test's result is the one notice that also pops up, since you're in Settings when you press Apply. When requests aren't stopped, pressing Apply sends nothing.
