# ModMenus modding guide

ModMenus adds a Mod Settings menu that other mods can put their settings in. To add yours, include `JSON/Mod Menu/modmenu.json` in your mod.

The file is an array of UI elements, shown in order:

```json
[
	{
		"type": "text",
		"labelID": "MYMOD__MODMENU__EXPLAIN"
	},
	{
		"type": "slider",
		"varID": "MyMod_Count",
		"labelID": "MYMOD__MODMENU__COUNT",
		"minValue": 1,
		"maxValue": 25,
		"defaultValue": 5
	}
]
```

Supported element types are text, sliders, checkboxes and dropdowns. Each one's `labelID` is the ID of a text entry defined in `JSON/Constants/constants.json`.

## type: text

```json
{
	"type": "text",
	"labelID": "CUSTOMAUDITION__MODMENU__EXPLAIN"
}
```

- **labelID**: ID for text defined in `JSON/Constants/constants.json`

## type: slider

```json
{
	"type": "slider",
	"varID": "CustomAudition_Count",
	"labelID": "CUSTOMAUDITION__MODMENU__COUNT",
	"minValue": 1,
	"maxValue": 25,
	"defaultValue": 5
}
```

Slider elements output an integer as a string to an in-game variable.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **minValue** and **maxValue**: Optional integers. Default range: 0 to 100
- **defaultValue**: Optional integer. Default: midpoint of range

## type: checkbox

```json
{
	"type": "checkbox",
	"varID": "AuditionAgeLimit_TogglePopup",
	"labelID": "AUDITIONAGELIMIT__MODMENU__TOGGLE",
	"defaultValue": false
}
```

Checkbox elements output `"1"` or `"0"` as a string to an in-game variable, representing true or false.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **defaultValue**: Optional boolean. Default: false

## type: dropdown

```json
{
	"type": "dropdown",
	"varID": "AuditionAgeLimit_TogglePopup",
	"labelID": "AUDITIONAGELIMIT__MODMENU__TOGGLE",
	"itemIDList": ["YES", "NO"],
	"defaultValue": 0
}
```

Dropdown elements output an index (0, 1, 2, etc.) as a string to an in-game variable, corresponding to the selected item's position.

- **varID**: ID for the in-game variable storing the value
- **labelID**: ID for text defined in `JSON/Constants/constants.json`
- **itemIDList**: Required array of strings. Each string is the ID for text defined in `JSON/Constants/constants.json`
- **defaultValue**: Optional integer. Default: 0

## Tips

- Retrieve values using `variables.Get(varID)`, where `varID` is the element's varID.
- Implement null checks for `variables.Get(varID)` to handle cases where ModMenus is not installed.
- To add vertical spacing, use a text element with an empty labelID (`""`).
- Always define text in `constants.json` for translation support. If absolutely necessary, ModMenus can handle raw text from labelID or itemIDList fields.
- For testing, add `"ignore": true` to any element to exclude it from parsing.
