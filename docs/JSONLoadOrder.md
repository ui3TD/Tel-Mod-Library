# JSON Load Order modding guide

JSON Load Order lets your mod's JSON files load before or after other mods' files. Add a `JSONLoadOrder` number to your mod's `info.json`:

- The default is `0`.
- Lower numbers load early, and higher numbers load late.

To change or translate another mod's JSON files, give your mod a higher number than that mod so your files load after its files.

Example `info.json` for a mod that loads late:

```json
{
  "Title": "JSON Load Order Tool",
  "Description": "Allows modders to adjust the JSON load order of their mod. Just add a JSONLoadOrder attribute to info.json. Default JSONLoadOrder number is 0. Lower numbers load early, and higher numbers load late.",
  "Author": "Tel",
  "Version": "1.0",
  "Tags": ["modding"],
  "HarmonyID": "com.tel.jsonloadorder",
  "JSONLoadOrder": 100
}
```
