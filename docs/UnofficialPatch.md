# Unofficial Patch: every fix

Unofficial Patch fixes bugs in Idol Manager. This page lists every fix, grouped by the part of the game it changes. Each entry says what the game did wrong and what happens now. The Steam page lists only the fixes that change how the game plays.

A fix marked **(changes gameplay)** changes the balance of the game, not just a display or a rare error. A fix marked **(can be turned off)** has a checkbox in ModMenus; see [Settings](#settings).

## Settings

With [ModMenus](https://steamcommunity.com/sharedfiles/filedetails/?id=3283314791) installed, the Unofficial Patch tab in Settings > Mod Settings has a checkbox for each fix below. They are all ticked at first. Unticking one makes that part of the game work as in the original game. A change applies straight away, and each save keeps its own choices unless the tab's "Use in all saves" box is ticked. Without ModMenus, every fix is on.

- **Theater shows cost stamina.** Off: theater shows cost no stamina.
- **Tour attendance needs fame.** Off: fame never lowers world tour attendance.
- **Best Employer award can be lost.** Off: the award is won every year.
- **Salary wishes follow recent pay.** Off: an idol's expected salary is based on everything she has earned since she joined, so it rises until it reaches the game's limit of twice her base salary.
- **Dating scandals cost mental stamina.** Off: scandal responses cost no mental stamina.

The Best Employer and salary checkboxes both come from one fix: the game never ran its monthly update. Unofficial Patch always runs the monthly update now, and an unticked box skips only its own part of it. If you tick the salary box again after a while, the earnings from the months it was off count as one month.

A checkbox turns off only Unofficial Patch's own fix. If another mod fixes the same bug, unticking the box doesn't undo that mod's fix.

## Calendar

- An error during the day change no longer stops the calendar for good. The game called every daily, weekly and per-tick update in one go, so one failing update skipped the rest and stopped the date until a save was loaded. Now each update runs on its own: one that fails is skipped for that day, the game keeps running, and a red notification names it. The full error is in Player.log.

## Theater

- Theater shows now use stamina. In the game they cost the idols nothing. **(changes gameplay)** **(can be turned off)**
- The theater money tooltip now adds subscriptions, skips days off, and covers 7 days instead of 6.
- Theater revenue statistics are no longer shifted by one day.
- Theater averages now leave out days off.
- Theater revenue now counts towards each idol's earnings.

## Cafe

- Weekly cafe revenue now covers 7 days instead of 6.
- A new dish no longer always starts at 100% novelty when the idol's other dishes are stale.
- Working a cafe shift no longer cures a depressed waitress.

## Concerts, shows and world tours

- The concert revenue estimate now matches what the concert earns, including the FUJI ticket bonus.
- Concert attendance and hype no longer round up, so 99.5% attendance no longer shows as a sell-out.
- Right after opening the show or concert editor, you can no longer pick an idol who is already in the cast for a second slot. Idols already in a show or concert cast are greyed out.
- Cancel in the concert editor now throws away your changes.
- On the show's weekday, the show tooltip no longer shows today as the next episode.
- When an idol leaves a show, her slot is no longer refilled with an idol who is already in the cast.
- A show cast with the entire group now gives every idol the same opinion vote, a quarter of the episode's. In the game, the first idol in roster order got a quarter, the second a sixteenth, the third a sixty-fourth, and the rest almost nothing. **(changes gameplay)**
- The world tour revenue text colour now takes savings into account, and turns red when the tour would lose money.
- World tour savings: the Most Prolific Group award and cheaper events no longer make extra stops free.
- World tour attendance now depends on your fame level, so big venues need high fame. **(changes gameplay)** **(can be turned off)**

## Singles

- Senbatsu stats no longer punish you for rows you don't have enough idols to fill, and use the stat asked for instead of always Cute. **(changes gameplay)**
- Edgy and Artsy PVs now change sales and new fans, as their tooltips say. In the game they did nothing. **(changes gameplay)**
- A single's song quality now counts towards its fans' opinion votes for the genre, as it already does for sales. A weak song lowers the votes and a strong one raises them. **(changes gameplay)**
- The first single now gives +1 opinion to its two strongest fan types and -1 to its two weakest. In the game it always gave +1 to male and female fans and -1 to young adult and adult fans, whatever the single was. **(changes gameplay)**

## Idols

- The Best Employer award can now be lost: months with injured, depressed, exhausted or underpaid idols count against it. Idols' expected salaries now follow their recent pay. **(changes gameplay)** **(can be turned off)**
- A promoted male producer's small portrait is no longer blank.
- The English birthday title no longer says "21th birthday". It now reads: Mio turns 21!
- When two idols dating each other break up, their relationship status is no longer shown as known.
- Dating status is now shown for underage idols too, so you can keep track of scandal risks.
- An idol no longer gossips about herself.
- The stamina cost shown for performances is now correct under the Energetic performance policy.

## Clinics and Break Room

- Clinics no longer take idols who are busy in another room.
- Idols no longer get stuck after hanging out in the Break Room, and a Break Room chat no longer cures an injury or brings back a graduated idol.

## Fans

- The fan age pie charts now show the right proportions.
- Concerts, show and SSK cancellations and random events now change the fan opinion of every active idol. In the game they changed only an agency-wide opinion that nothing uses. **(changes gameplay)**
- Fans of one type (from business deals, cafes, and fans switching to another idol) now land in that type and are no longer lost. In the game about a quarter landed in other types, and any amount above about 24,000 fans per idol partly disappeared. **(changes gameplay)**
- After a scandal or an event, fans who switch to other idols are moved once. In the game, a dating scandal moved its hardcore switchers twice, so it could raise your agency's total fans, and a later event the same day could move them again. **(changes gameplay)**
- Without Fan Attrition, the fans tooltip now shows a fan segment's real opinion when all its votes are positive (or all negative), instead of 50%. Opinion is shown on the same -100 to +100 scale as the idol profile. Fan Attrition draws its own fans tooltip, so this doesn't show with it.
- A fan segment with no opinion votes now counts as neutral. This doesn't happen in a normal game; it protects against other mods.

## Events and dialogues

- Dating scandal responses now cost the idol the mental stamina the game lists: 25, or 50 for making the relationship public after a damaging report. Thick Skin and Shameless idols are spared. In the game no idol ever lost mental stamina from a scandal response. **(changes gameplay)** **(can be turned off)**
- Event and dialogue checks for Influence now check Influence instead of Friendship.
- Event and dialogue checks for "not" conditions on variables no longer always pass.
- Event and dialogue checks for having a staffer of a specific type now work.

## Auditions and new idols

- Generating some platinum and gold idols (in auditions, Aya, or hiring a rival) no longer freezes the game.
- Auditions no longer wait forever for portraits: cards unlock after 2 seconds, and a slow portrait no longer holds up the others.

## Saving and loading

- Saving and loading no longer pushes back the bankruptcy deadline.
- Concerts in production no longer lose their place in the Special Events list after loading.
- A failed load of a missing or corrupt save no longer breaks autosave or lets it overwrite your saves.

## Fixes other mods found first

These mods found these bugs first. Unofficial Patch includes these fixes, so you don't need these mods for them. Each entry says what Unofficial Patch does differently.

- [BestEmployerFix](https://steamcommunity.com/sharedfiles/filedetails/?id=3158389459) by magier: the Best Employer award. Unofficial Patch uses the game's own monthly check, which also fixes expected salaries.
- [Male Player Thumbnail Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3781484403) by madxis: the blank small portrait of a promoted male producer. Unofficial Patch lets mods that change the producer's portrait show theirs.
- [Save n Load Fixes](https://steamcommunity.com/sharedfiles/filedetails/?id=3810566069) by Cosmo: expected salaries, concerts' place in the Special Events list, the bankruptcy deadline after loading, and a show's next episode date. Unofficial Patch doesn't change how your game is saved.
- [Unavailable Idols Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3760864808) by Cosmo: Cancel in the concert editor. Unofficial Patch works better with other mods that change concerts.
- [Birthday Text Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3807347510) by Cosmo: the birthday popup title (21th birthday). Unofficial Patch's text can be changed by other mods.
- [Clinic Recovery Priority Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3760864574) by Cosmo: idols left stuck after hanging out in the Break Room. Unofficial Patch stops idols getting stuck instead of freeing them later, and a Break Room chat can no longer cure an injury or bring back a graduated idol.
- [Room Assignment Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3750618476) by Cosmo: clinics taking idols who are busy in another room. Unofficial Patch leaves every other room as in the base game.
- [Show Cast Assignment Fix](https://steamcommunity.com/sharedfiles/filedetails/?id=3750618658) by Cosmo: an idol in two spots of the same show. Unofficial Patch gives the empty spot a new idol instead of leaving it empty, and greys out idols already in a show or concert cast.
