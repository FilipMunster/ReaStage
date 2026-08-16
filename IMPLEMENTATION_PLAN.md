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

## 5. Etapa 2 — ovládání myší, scrubbing, statistiky, web

### 5.1 Rozhodnutí (odsouhlaseno)

| Téma | Rozhodnutí |
|---|---|
| Transportní tlačítka | Ve spodní liště okna, **na stejném řádku** jako statistiky (ne overlay nad aktuální písní, ani na okrajích karty, ani ve vlastním řádku — vše vyzkoušeno a zavrženo). Tři tlačítka ⏮ / ⏯ / ⏭, zašedlá a poloprůhledná; při najetí myší se zvýrazní. Volají tutéž logiku koordinátoru jako klávesy ← / → / Mezerník (rewind včetně prahu `previousThresholdSeconds`). |
| Klik na kartu aktuální písně | Play/pauza — stejně jako tap na webu. Zůstává i vedle tlačítka ⏯ ve spodní liště. |
| Stavový řádek | Jeden řádek: vlevo statistiky s metronomem, uprostřed transportní tlačítka, vpravo zbývající čas a odhad konce. Sekce vlevo jsou oddělené **svislou linkou** (`1/7 │ 150 BPM │ 4/4 ●●○○`) a každá má **pevnou šířku** (škálovanou `fontScalePercent`), aby metronom neposkakoval, když se změní počet číslic. Tlačítka jsou překryvná (`Panel`), takže sedí na středu okna, ne mezi skupinami. |
| Dlouhé takty vs. tlačítka | U vyšších čitatelů (7/8, 12/8) je řada teček širší než místo nalevo od vystředěných tlačítek. **Řešeno překryvem:** tečky prostě pokračují pod tlačítky, která jsou poloprůhledná (`Opacity 0.35`, na hover 1 nad 12% bílou), takže zůstanou čitelné. Žádné zmenšování ani seskupování teček. |
| Scrubbing pozice | **Zamítnuto** (implementováno a následně odstraněno). Tažení myší nad kartou aktuální písně ani akord Mezerník + šipka se v praxi neosvědčily; Mezerník tak zůstává u chování z kapitoly 1 (play/pause na stisk, žádná vlastní logika). |
| Klik na jinou píseň | Dvoukrokově: první klik píseň „odjistí" (zvýraznění), druhý klik potvrdí skok (Stop + SetPosition na začátek písně). Pojistka proti překliku na pódiu. |
| Časy v kartě aktuální písně | Čas od začátku i zbývající do konce, každý **současně ve dvou formátech**: `mm:ss` i `bar:beat`. Nahrazují dosavadní jediný údaj pozice ve formátu REAPERu. |
| BPM | Ve statistikách se zobrazuje aktuální tempo (BPM). |
| Taktový rozměr | Ve spodní liště vedle BPM (`4/4`, `6/8`). Zdroj je HTTP `BEATPOS` — OSC taktový rozměr neposílá, takže se obnovuje při startu a při každém skoku na píseň (změna uprostřed písně se projeví až u další). |
| Vizuální metronom | Ve spodní liště tolik teček, kolik je dob v taktu. Plní se kumulativně 1→N a na jedničce se vynulují; první doba zlatá (`#FBBF24`), ostatní zelené (akcent). Zarovnáno vlevo, aby tečky při změně taktu nepřeskakovaly. **Bez animací a bez vlastního časovače** — kreslí se přímo doba hlášená REAPERem (`/beat/str`), aby latence proti audio metronomu byla co nejmenší. Lze vypnout v nastavení (`showMetronome`). |
| Latence metronomu | Dána frekvencí OSC feedbacku REAPERu (`Update frequency` u OSC zařízení, výchozí nízká — u testovaného stroje 10 Hz = až 100 ms). Pro použitelný metronom je potřeba ji zvednout (~30–50 Hz). Pokud by ani to nestačilo, metronom se zruší. |
| Zdroj tempa | Ověřeno: web API REAPERu tempo nevystavuje vůbec a OSC `/tempo/raw` chodí **jen při změně tempa** — aplikace připojená k už načtenému projektu se BPM nikdy nedozví. Proto se tempo za přehrávání **měří** z rychlosti postupu beatů (`/beat/str` + `/time`, okno 1,5 s, krok 0,5 BPM); `/tempo/raw` má přednost, když ho REAPER pošle. |
| Zbývající čas playlistu | Čistý hudební čas: zbytek aktuální písně + součet délek následujících písní playlistu. Čekání mezi písněmi se nepredikuje — odhad času konce se při stání posouvá. |
| Webové ovládání | HTTP server v ReaStage (**ASP.NET Core minimal API / Kestrel**), port v nastavení, bez autentizace, jen StageView, optimalizováno pro mobil. Stránka aktualizuje stav **pollingem** (à la REAPER `reaper_www_root`). |

### 5.2 Fáze 7 — Tempo z REAPERu (základ pro BPM a bar:beat)

- Rozšířit `ReaStage.ReaperOSC` o řádek `TEMPO f/tempo/raw` (uživatel poté
  re-importuje OSC config v REAPERu).
- Nové pole `double TempoBpm` v `ReaperPosition` + handler pro OSC adresu
  `/tempo/raw`.
- `GetPosition()` (HTTP): doplnit tempo do dotazu a parsování. Pokud REAPER web API
  tempo přímo nevystaví, HTTP fetch tempo vynechá a hodnota se doplní z první OSC
  zprávy `/tempo/raw` (ověřit při implementaci).
- Marshaling OSC událostí na UI vlákno je už vyřešen z fáze 1.

### 5.3 Fáze 8 — Ovládání myší ve stage view

- **Transportní lišta dole (ne overlay):** tři tlačítka (⏮ rewind, ⏯ play/pauza,
  ⏭ forward) ve spodní části okna, zašedlá a poloprůhledná; při najetí myší se
  zvýrazní. Volají `GoToPreviousAsync` / `TogglePlayPauseAsync` / `GoToNextAsync`
  na koordinátoru — identické chování jako klávesy. Mezikrok, kdy tlačítka seděla
  na okrajích karty aktuální písně, se neosvědčil a byl vrácen zpět.
- **Klik na kartu aktuální písně** = play/pauza (`TogglePlayPauseAsync`), stejně
  jako tap na webu.
- **Scrubbing byl zrušen.** Původně navržené tažení myší nad kartou aktuální písně
  a akord Mezerník + šipka se neosvědčily a byly odstraněny včetně příkazů
  `SeekByBarsAsync` / `SeekWithinCurrentAsync`. Mezerník opět posílá play/pause
  na stisk klávesy.
- Nový příkaz koordinátoru: `JumpToItemAtAsync(int index)` (zobecnění interní
  `JumpToItemAsync`) pro two-click skok.
- **Klik na jinou viditelnou píseň:** první klik kartu odjistí (akcentový rámeček),
  druhý klik do ~4 s provede Stop + SetPosition na začátek té písně. Escape nebo
  timeout odjištění zruší. Odjištění je čistě UI stav ve `StageViewModel`.
  *Vědomá odchylka:* „klik jinam" odjištění neruší (ponecháno, stačí Escape a timeout).
- **Seznamy okolních písní se nesmí přestavovat při každé změně pozice.** Za
  přehrávání chodí OSC několikrát za sekundu; pokud se kolekce položek pokaždé
  vytvoří znovu, `ItemsControl` zahodí a znovu vyrobí kontejnery — kurzor nad
  písní kmitá mezi ručičkou a šipkou a klik se nikdy nedokončí. Kolekce se proto
  mění jen tehdy, když se skutečně změní zobrazené písně.

### 5.4 Fáze 9 — Statistiky a časy

- **V kartě aktuální písně:** vlevo dole čas od začátku písně (`pozice − start`),
  vpravo dole zbývající čas s prefixem minus (`−(end − pozice)`). Každý údaj
  **současně ve dvou formátech**: `mm:ss` i `bar:beat` (počítáno z `TempoBpm`
  a taktového rozměru — REAPER hlásí bar:beat jen absolutně od začátku projektu,
  ne v rámci písně). Nahrazuje dosavadní centrovaný údaj pozice. Čas od začátku je
  pozice v písni (1-based), zbývající čas je délka úseku (počítá se od nuly).
- **Aktuální BPM** (z `TempoBpm`) — ve statistikách (spodní lišta nebo karta).
- **Spodní stavová lišta stage view:**
  - pořadí: `N / M` (číslo aktuální písně / počet písní playlistu);
    ve stavu mezi písničkami `– / M`,
  - zbývající hudební čas do konce playlistu,
  - odhad času konce playlistu (hodiny, formát `HH:mm`) = teď + zbývající čas.
- Formát časů: `m:ss`, nad hodinu `h:mm:ss`; bar:beat dle formátu REAPERu.
- Odhad času konce se přepočítává na 1s časovači (posouvá se i při zastaveném
  transportu), ostatní hodnoty při změně pozice/playlistu.
- Výpočty jako čisté funkce ve `StageViewModel` (nebo pomocná třída) + testy.

### 5.5 Fáze 10 — UX nastavení

- **Zachytávání kláves stiskem:** místo textového pole tlačítko zobrazující
  aktuální klávesu; klik přepne do režimu „Stiskni klávesu…", následující
  KeyDown se uloží jako `KeyGesture` (včetně případných modifikátorů),
  Escape zachytávání zruší. Duplicitní přiřazení téže klávesy dvěma akcím
  validace odmítne.
- **Velikost písma v procentech:** `fontScalePercent` (50–200, výchozí 100),
  slider s číselnou hodnotou v nastavení. Multiplikátor se aplikuje na velikosti
  písma **pouze ve stage view** (názvy písní, časy, stavová lišta) — ostatní
  obrazovky beze změny. Projeví se okamžitě po uložení (bez restartu).
- **Nastavení webového serveru:** povolení (on/off) a port webu (`web.enabled`,
  `web.port` v `settings.json`, default např. 9125).
- **Zobrazení metronomu:** přepínač `showMetronome` (default zapnuto) — skryje
  tečky metronomu ve stavovém řádku. Taktový rozměr zůstává zobrazen.

### 5.6 Fáze 11 — Webové ovládání (mobil)

- **Server:** HTTP server běžící v ReaStage; povolení a port z nastavení, **bez
  autentizace** (určeno pro LAN). Návrh API: stavový endpoint (JSON — položky
  playlistu, index aktuální písně, pozice/progress, play state, BPM, časy) +
  command endpointy (play-pause, prev, next, jump na index). Web i desktop sdílí
  logiku přes koordinátor / `StageViewModel`.
- **Stránka:** jedna statická HTML5 stránka (bez frameworku, bez build stepu),
  přiložená jako embedded resource. Tmavý vzhled koherentní s ReaStage (sdílená
  paleta a typografie v CSS). Optimalizováno pro mobil (viewport, velké dotykové
  cíle). Obsah = **pouze StageView**: celý playlist se zvýrazněnou aktuální písní
  (její box dostatečně vysoký pro bezpečný tap).
- **Gesta:**
  - swipe nahoru / dolů = scroll seznamu (celý playlist, neomezeně písní před i za);
    po skončení scrollu se po timeoutu seznam zacentruje na aktuální píseň,
  - swipe doleva / doprava = jako klávesy ← / → (Předchozí / Další),
  - tap na aktuální píseň = jako Mezerník (play/pause),
  - tap na jinou píseň = jako klik myší v desktopu: první tap odjistí, druhý potvrdí
    skok. Odjištění se řeší na straně stránky (klient), aby web nezasahoval do
    odjišťovacího stavu desktopu.
- **Stack (rozhodnuto):** server = **ASP.NET Core minimal API** (Kestrel) — bind na
  `http://0.0.0.0:<port>` bez adminu/urlacl, zabudované servírování statické stránky
  + JSON, integrace se stávajícím `Microsoft.Extensions.*` (DI, logging). Přidá se
  `FrameworkReference Microsoft.AspNetCore.App`; publikovat self-contained. Hostování
  na pozadí (IHostedService / vlastní vlákno), start/stop dle `web.enabled`.
- **Aktualizace (rozhodnuto):** **polling** — stránka tahá stavový endpoint á
  ~150–250 ms a po každém příkazu si stav hned vyžádá (svižná odezva bez čekání na
  další tik). Bez SSE. Referenční statické stránky REAPERu: `…/Plugins/reaper_www_root/`
  (`basic.html`, `click.html`, `index.html`) — stejný princip (polling `/_/COMMAND`
  přes XHR).
- **Publikace self-contained:** kvůli `FrameworkReference Microsoft.AspNetCore.App`
  vyžaduje framework-dependent build nainstalovaný ASP.NET Core runtime, ne jen
  desktopový. V `.csproj` proto `RuntimeIdentifiers` (`win-x64`, `linux-x64`)
  a `PublishSelfContained`; běžný `dotnet build` zůstává beze změny. Bez trimmingu —
  Avalonia i minimal API stojí na reflexi.

### 5.7 Fáze 12 — Taktový rozměr a vizuální metronom

- **Taktový rozměr** ve stavovém řádku vedle BPM (`FormatTimeSignature`).
  Zdroj `TimeSigNumerator`/`Denominator` z HTTP `BEATPOS`.
- **Tečky metronomu:** jedna na dobu taktu, kumulativní plnění, zlatá jednička.
  Instance teček se recyklují — při změně doby se přepíná jen `IsLit`, seznam se
  přestavuje pouze při změně délky taktu (latence a plynulost).
- Doba v taktu z `PositionStringBeats` (`StageStats.BeatInBar`) — nepotřebuje tempo.
- Odvození tempa přepočítat na čtvrťky (`× 4/jmenovatel`), jinak x/8 hlásí
  dvojnásobné BPM.
- Vypínatelné nastavením `showMetronome`.

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
- **OSC pattern soubor.** Stávající `ReaStage.ReaperOSC` posílá čas, beat, tempo
  a transport; regiony se čtou přes HTTP. Po změně souboru je nutný re-import
  OSC configu v REAPERu.
- **REAPER posílá i zprávy mimo bundle.** Sdružuje jen hodnoty, které vzniknou
  naráz; osamocenou hodnotu (typicky `/tempo/raw`) pošle jako holou OSC zprávu.
  Příjem musí zvládat obě podoby paketu, jinak se taková hodnota tiše ztrácí.
- **Frekvence OSC feedbacku.** `Update frequency` u OSC zařízení v REAPERu určuje
  latenci všeho, co se veze na pozici (metronom, progress, detekce konce písně).
  Výchozích ~10 Hz je pro metronom málo, doporučeno 30–50 Hz.
- **Stav „mezi písničkami".** Kapitola 3.1 počítá s tím, že rámeček ukáže
  připravenou píseň bez progressu. Implementace zobrazuje pomlčku a připravenou
  píseň až v seznamu následujících — *vědomě ponecháno*.
