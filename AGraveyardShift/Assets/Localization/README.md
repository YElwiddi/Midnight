# Localization

The game ships in 10 languages: English (source), Simplified Chinese, Russian, Spanish,
Brazilian Portuguese, German, Japanese, French, Polish and Korean. The language is picked in
**Settings > Language** (main menu); the first launch follows the OS language, the choice is
saved in PlayerPrefs (`Locale`).

## How it works

* **Runtime:** `Scripts/Localization/Loc.cs` loads `Resources/Localization/Tables/<code>.json`
  and exposes two lookups:
  * `Loc.Get("ui.menu.play")` – explicit keys for UI chrome and code strings.
  * `Loc.Tr("Where am I?")` – gettext style: the **English source text is the key**. Every
    string authored in the inspector (dialogue prompts, notes, confirm messages, intro pages)
    and every Ink line/choice is translated this way, so designers keep writing plain English
    and nothing has to be re-keyed. Unknown strings fall back to the English text.
* **Where translation happens:** `DialogueUI.SetDialogueText/DisplayChoices` (all dialogue,
  Ink choices, Yes/No confirms), `ReadableUI.Open` (notes/books), `IntroTextUI`,
  `EndingScreenUI`, `StaircaseDialogueController`, `KillerNPC` jumpscare text, `CCTVMonitor`
  labels, `EndingInfo`/`CryptFiendInfo` (keys). Static labels carry a `LocalizedText`
  component (Key / SourceText / FontOnly modes); the baked-word sprite buttons carry a
  `LocalizedSprite` (English art, text-free plate + label for other languages).
* **Fonts:** `Resources/Localization/LocalizationSettings.asset` maps each locale to font
  replacements (the game's horror fonts are ASCII-only). Latin locales keep the look via dynamic
  twins / Rubik Distressed, Russian & Polish use PT Serif for body text, CJK uses Noto Sans
  SC/JP/KR. The Noto fonts are also global TMP fallbacks, so no text can ever show a missing
  glyph box. All bundled fonts are SIL OFL (licenses in `Fonts/`).

## Workflow (Tools > Graveyard > Localization)

1. **Extract Source Strings** – scans the build scenes, prefabs, ScriptableObjects and the Ink
   JSON referenced by GameEvents and writes `source_strings.json` (the translation template,
   with a context line per string). Run it after adding or editing any player-facing text.
2. **Report Missing Translations** – per locale, lists source strings without a translation
   (`missing_report.txt`). A missing string is not an error at runtime: it shows in English.
3. **Export CSV** – `strings.csv` (key, context, en, zh-Hans, …) for translators (UTF-8, opens
   in Excel/Sheets).
4. **Import CSV** – writes edited columns back into the per-locale JSON tables. Empty cells
   never overwrite an existing translation.

Adding text the extractor should know about: player-facing string fields are whitelisted per
component in `Editor/Localization/LocalizationExtractor.Fields`; code strings go in
`CodeStrings()`.

Editing a source string in the inspector/Ink changes its key: re-run Extract + Report and
translate the new line (the old translation is listed as UNUSED).

## Rebuilding the menu

`MainMenuBuilder.Build()` (Tools > Graveyard > Build Main Menu) calls
`LocalizationSetup.SetupMainMenu()`, so a rebuilt menu keeps the language dropdown and the
localized labels. `Tools > Graveyard > Localization > Setup` re-applies everything on demand
(font assets, settings asset, both scenes); it is idempotent.
