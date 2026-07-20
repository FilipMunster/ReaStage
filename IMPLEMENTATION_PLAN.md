# ReaStage — Plán implementace

ReaStage je pódiový ovladač REAPERu. Region v REAPERu představuje jednu písničku,
aplikace umožňuje sestavovat z regionů playlisty a ovládat přehrávání klávesnicí.
Zdrojem pravdy o pozici přehrávání je REAPER (push přes OSC), playlisty jsou lokální
JSON databáze.

## 1. Klíčová rozhodnutí (odsouhlaseno)

| Téma | Rozhodnutí |
|---|---|
| Platforma | Aplikace poběží na Windows i Linuxu. Žádná Windows-only API; cesty přes `Environment.SpecialFolder.ApplicationData` (na Linuxu `~/.config`). |
| Konec písničky | Aplikace hlídá překročení konce regionu. Poté pošle Stop a nastaví pozici na začátek další písničky v playlistu. Mezerník pak spustí další píseň. Přesné načasování není kritické — v projektu REAPERu je mezi písněmi vždy chvíle ticha, přeslech do další písně nehrozí. |
| Aktualizace regionů | Polling na pozadí v konfigurovatelném intervalu (default ~10 s) + refresh při otevření editoru playlistů + manuální tlačítko v editoru. |
| Mezerník | Volá přímo `IReaperClient.SendPlayPause()` — chování 1:1 jako mezerník v REAPERu, žádná vlastní logika. |
| Šipka doleva | Práh podle pozice: je-li pozice > *N* sekund (default 3, konfigurovatelné) od začátku aktuální písně → Stop + skok na její začátek. Jinak → Stop + skok na začátek předchozí písně v playlistu. |
| Šipka doprava | Stop + skok na začátek další písně v playlistu. |
| Pořadí navigace | Šipky se řídí pořadím v playlistu, nikoli pořadím regionů na timeline. |
| Identita písně | Playlist odkazuje na region pouze přes region ID. Nespárované položky (region v REAPERu už neexistuje) se v UI viditelně označí jako chybějící. |
| Klávesy | Uživatelsky nastavitelné, defaultně Mezerník / ← / →. |

## 2. Architektura

```
Views (Avalonia, dark theme)
  MainWindow ── StageView | SettingsView | PlaylistEditorView
ViewModels (CommunityToolkit.Mvvm)
  MainWindowViewModel, StageViewModel, SettingsViewModel, PlaylistEditorViewModel
Domain / Services
  PlaybackCoordinator   ← jádro logiky (pozice → píseň, hlídání konce, navigace)
  IPlaylistService      ← CRUD playlistů + JSON persistence (autosave)
  ISettingsService      ← uživatelská konfigurace + JSON persistence
  IReaperClient         ← existuje (HTTP příkazy + OSC listener)
```

### 2.1 PlaybackCoordinator (nová služba, srdce aplikace)

Odebírá `IReaperClient.PositionChanged` a drží aplikační stav:

- **Mapování pozice → aktuální píseň:** pozice v sekundách se hledá v intervalech
  `[Start, End)` regionů namapovaných na položky aktivního playlistu.
  Pozice mimo všechny položky playlistu = stav „mezi písničkami".
- **Hlídání konce písně:** když `PlayState == Playing` a pozice překročí konec
  regionu aktuální písně, pošle `SendStop()` + `SetPosition(začátek další písně)`.
  Přesnost načasování neřešíme — ticho mezi písněmi v projektu REAPERu kryje
  zpoždění detekce (jeden až dva OSC updaty).
- **Navigační příkazy:** `GoToNext()`, `GoToPrevious()` (s prahem), `TogglePlayPause()`.
  Když není známa aktuální píseň (mezera, stop před prvním spuštěním),
  šipky navigují na první píseň playlistu.
- Vystavuje observable stav pro UI: aktuální píseň, progress (0–1), okolní písně,
  play state.

Čistá, na UI nezávislá třída → jednotkově testovatelná (pozici lze simulovat).

### 2.2 Playlisty — model a persistence

```json
// %AppData%/ReaStage/playlists.json
{
  "version": 1,
  "activePlaylistId": "guid",
  "playlists": [
    {
      "id": "guid",
      "name": "Sobotní koncert",
      "items": [
        { "regionId": 3, "deleted": false },
        { "regionId": 1, "deleted": false },
        { "regionId": 7, "deleted": true }
      ]
    }
  ]
}
```

- `deleted: true` = soft delete — položka se v editoru zobrazuje oddělená na konci
  sloupce a lze ji vrátit zpět. Ve stage view a navigaci se ignoruje.
- Virtuální „REAPER playlist" (regiony v pořadí timeline) se do souboru neukládá,
  generuje se za běhu z `GetRegions()`.
- Každá změna v editoru se okamžitě uloží (autosave, žádné tlačítko Uložit).

### 2.3 Nastavení

```json
// %AppData%/ReaStage/settings.json
{
  "keys": { "playPause": "Space", "previous": "Left", "next": "Right" },
  "previousSongsShown": 2,
  "nextSongsShown": 3,
  "previousThresholdSeconds": 3.0,
  "regionsPollSeconds": 10,
  "fontScalePercent": 100,
  "reaper": { "host": "localhost", "httpPort": 9123, "oscPort": 9124 }
}
```

- Řeší zároveň stávající `// TODO config` v `ReaperClient` (porty).
- `appsettings.json` v repu zůstává jen pro NLog; uživatelská konfigurace žije
  v `%AppData%/ReaStage/` vedle playlistů a logů.
- Klávesy se ukládají jako Avalonia `KeyGesture` řetězce.

## 3. UI

Tmavé téma (Fluent Dark + vlastní styly), velké písmo čitelné z dálky na malém
monitoru. Hlavní okno = stage view, vlevo nahoře hamburger ikona otevírající
`SplitView` postranní panel.

### 3.1 Stage view

- Vertikální pás playlistu přes celou plochu okna, aktuální píseň uprostřed.
- **Aktuální píseň:** velké písmo, barevný rámeček. Pozadí rámečku se zleva plní
  (šedý progress) podle pozice v písni — `Border` + `Rectangle` s šířkou
  úměrnou progressu.
- **Předchozí písně** nad ní: menší písmo, nejméně výrazné. Počet dle nastavení.
- **Následující písně** pod ní: výraznější než předchozí, méně než aktuální.
  Počet dle nastavení.
- Stav „mezi písničkami": aktuální rámeček ukazuje připravenou (další) píseň
  bez progressu, vizuálně odlišený stav (např. slabší rámeček).
- Chybějící písně (region ID nenalezen) se ve stage view nezobrazují — koordinátor
  je z přehrávání vyřazuje. Zřetelné označení mají v editoru playlistů.

### 3.2 Postranní panel (hamburger)

Tři tlačítka:
1. **Nastavení** — klávesy, počty zobrazených písní, práh šipky ←, porty/host.
2. **Editor playlistů**
3. **Volba aktuálního playlistu** — seznam playlistů; před přepnutím potvrzovací
   dialog.

### 3.3 Editor playlistů

- Playlisty jako sloupce vedle sebe, písně ve sloupci pod sebou.
- **První sloupec = REAPER playlist** (živý stav regionů z `GetRegions()`),
  read-only, lze jej pouze zvolit jako aktivní. Aktualizuje se při otevření
  editoru, průběžně pollingem a manuálním tlačítkem.
- **Nový playlist** = kopie REAPER playlistu.
- Drag & drop přesun písní v rámci playlistu.
- Smazání písně = přesun do vizuálně odděleného úseku na konci sloupce;
  tlačítkem lze vrátit zpět.
- Playlist lze přejmenovat a duplikovat.
- Autosave při každé změně.
- Po dobu otevřeného editoru transportní klávesy neovládají REAPER
  (kolize s psaním názvu, drag & drop).

## 4. Fáze implementace

> Fáze 1–6 jsou implementované (viz git historie). Navazuje etapa 2, kapitola 5.

### Fáze 1 — Základy a úklid
- `ISettingsService` + JSON persistence v `%AppData%/ReaStage/`.
- Porty a host v `ReaperClient` z nastavení (odstranit `TODO config`).
- Marshaling OSC událostí na UI vlákno (dnes se `[ObservableProperty]` zapisuje
  z background tasku).
- Oprava pádu na prázdném seznamu regionů (`Aggregate` v `MainWindowViewModel`).

### Fáze 2 — Doména playlistů
- Modely `Playlist`, `PlaylistItem`, služba `IPlaylistService` + JSON repo.
- Mapování položek na regiony (region ID), detekce chybějících.
- Periodická aktualizace regionů na pozadí (konfigurovatelný interval).
- Jednotkové testy persistence a mapování.

### Fáze 3 — PlaybackCoordinator + klávesy (funkční MVP)
- Mapování pozice → píseň, watchdog konce regionu, navigace ← / → s prahem.
- Klávesové vstupy (zatím default klávesy), mezerník → `SendPlayPause`.
- MVP test proti běžícímu REAPERu: playlist = pořadí regionů.
- Jednotkové testy koordinátoru (simulované pozice).

### Fáze 4 — Stage UI
- Tmavý vzhled, pás playlistu, zvýrazněná aktuální píseň s progress výplní,
  konfigurovatelné počty předchozích/následujících.
- Stavy: mezi písničkami, chybějící region, REAPER nedostupný.

### Fáze 5 — Panel, volba playlistu, nastavení
- Hamburger + SplitView, přepínání obsahu okna.
- Volba aktivního playlistu s potvrzovacím dialogem.
- Obrazovka nastavení včetně editace kláves.

### Fáze 6 — Editor playlistů
- Sloupcové zobrazení, REAPER sloupec read-only + refresh.
- Nový/duplikovat/přejmenovat, drag & drop, soft delete + obnova, autosave.
- Blokace transportních kláves při otevřeném editoru.

## 5. Etapa 2 — ovládání myší, statistiky, UX nastavení

### 5.1 Rozhodnutí (odsouhlaseno)

| Téma | Rozhodnutí |
|---|---|
| Hover tlačítka | Rewind/forward volají tutéž logiku koordinátoru jako klávesy ← / → (rewind včetně prahu `previousThresholdSeconds`). Jedna sdílená cesta, žádné odlišné chování myši vs. klávesnice. |
| Klik na jinou píseň | Dvoukrokově: první klik píseň „odjistí" (zvýraznění), druhý klik potvrdí skok (Stop + SetPosition na začátek písně). Pojistka proti překliku na pódiu. |
| Údaje v kartě aktuální písně | Časy od začátku / do konce **nahrazují** dosavadní pozici ve formátu REAPERu (beats) — v kartě zůstane jen název + nové časy. |
| Zbývající čas playlistu | Čistý hudební čas: zbytek aktuální písně + součet délek následujících písní playlistu. Čekání mezi písněmi se nepredikuje — odhad času konce se při stání posouvá. |

### 5.2 Fáze 7 — Ovládání myší ve stage view

- **Hover overlay nad aktuální písní:** při najetí myší na kartu aktuální písně
  se zobrazí tři poloprůhledná tlačítka (⏮ rewind, ⏯ play/pauza, ⏭ forward)
  překrývající kartu; zmizí po opuštění kurzorem. Volají `GoToPreviousAsync` /
  `TogglePlayPauseAsync` / `GoToNextAsync` na koordinátoru — identické chování
  jako klávesy.
- **Klik na jinou viditelnou píseň** (předchozí i následující): první klik kartu
  odjistí — zvýrazní se (akcentový rámeček) a čeká na potvrzení; druhý klik do
  ~4 s provede Stop + SetPosition na začátek té písně. Klik jinam, Escape nebo
  timeout odjištění zruší. Odjištění je čistě UI stav ve `StageViewModel`.
- Nový příkaz koordinátoru: `JumpToItemAtAsync(int index)` (zobecnění stávající
  interní `JumpToItemAsync`) pro skok na konkrétní položku playlistu.

### 5.3 Fáze 8 — Statistiky

- **V kartě aktuální písně:** vlevo dole čas od začátku písně
  (`pozice − start`), vpravo dole zbývající čas s prefixem minus
  (`−(end − pozice)`). Nahrazuje dosavadní centrovaný údaj pozice.
- **Spodní stavová lišta stage view:**
  - pořadí: `N / M` (číslo aktuální písně / počet písní playlistu);
    ve stavu mezi písničkami `– / M`,
  - zbývající hudební čas do konce playlistu,
  - odhad času konce playlistu (hodiny, formát `HH:mm`) = teď + zbývající čas.
- Formát časů: `m:ss`, nad hodinu `h:mm:ss`.
- Odhad času konce se přepočítává na 1s časovači (posouvá se i při zastaveném
  transportu), ostatní hodnoty při změně pozice/playlistu.
- Výpočty jako čisté funkce ve `StageViewModel` (nebo pomocná třída) + testy.

### 5.4 Fáze 9 — UX nastavení

- **Zachytávání kláves stiskem:** místo textového pole tlačítko zobrazující
  aktuální klávesu; klik přepne do režimu „Stiskni klávesu…", následující
  KeyDown se uloží jako `KeyGesture` (včetně případných modifikátorů),
  Escape zachytávání zruší. Duplicitní přiřazení téže klávesy dvěma akcím
  validace odmítne.
- **Velikost písma v procentech:** `fontScalePercent` (50–200, výchozí 100),
  slider s číselnou hodnotou v nastavení. Multiplikátor se aplikuje na velikosti
  písma **pouze ve stage view** (názvy písní, časy, stavová lišta) — ostatní
  obrazovky beze změny. Projeví se okamžitě po uložení (bez restartu).

## 6. Rizika a poznámky

- **Ticho mezi písněmi je předpoklad, ne garance.** Detekce konce písně stojí na
  tom, že projekt REAPERu má mezi regiony vždy chvíli ticha. Pokud by regiony
  navazovaly natěsno, zpoždění detekce (OSC interval + HTTP latence) by pustilo
  začátek další písně na timeline. Odpovědnost je na autorovi projektu.
- **Linux.** Avalonia i všechny použité knihovny jsou cross-platform; cesty jdou
  přes `SpecialFolder.ApplicationData`. Průběžně ověřovat na Linuxu (klávesové
  vstupy, fonty, chování oken).
- **Stabilita region ID.** Přečíslování regionů v REAPERu playlist tiše rozbije
  (odsouhlaseno: párujeme jen přes ID). Editor chybějící položky zvýrazní,
  víc neřešíme.
- **Klávesové vstupy vs. fokus.** `Window.KeyBindings` nefunguje, když má fokus
  editovatelný prvek — vstup řešit globálním handlerem na úrovni okna
  s explicitním vypnutím v editoru/nastavení.
- **Latence HTTP příkazů.** Stop + SetPosition jsou dva HTTP požadavky; REAPER
  web API umožňuje řetězit příkazy do jednoho požadavku středníkem
  (`SET/POS/x;1016`) — použít pro atomičtější chování.
- **OSC pattern soubor.** Stávající `ReaStage.ReaperOSC` posílá čas, beat
  a transport — pro plán dostačuje; regiony se čtou přes HTTP.
