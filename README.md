# ReaStage

Pódiový ovladač [REAPERu](https://www.reaper.fm/). Každý **region** v projektu REAPERu
je jedna písnička, z regionů se skládají **playlisty** a celý koncert se pak ovládá
třemi klávesami.

Zdrojem pravdy o pozici přehrávání je vždy REAPER. ReaStage si ji nedrží ani
nedopočítává — poslouchá, co REAPER posílá.

## Obsah

- [Jak to funguje](#jak-to-funguje)
- [Požadavky](#požadavky)
- [Instalace na Windows](#instalace-na-windows)
- [Instalace na Linux](#instalace-na-linux)
- [Nastavení REAPERu](#nastavení-reaperu) ← **bez tohoto kroku aplikace nefunguje**
- [Nastavení ReaStage](#nastavení-reastage)
- [Požadavky na projekt v REAPERu](#požadavky-na-projekt-v-reaperu)
- [Ovládání](#ovládání)
- [Ovládání z mobilu](#ovládání-z-mobilu)
- [Kde se ukládají data](#kde-se-ukládají-data)
- [Sestavení ze zdrojů](#sestavení-ze-zdrojů)
- [Řešení potíží](#řešení-potíží)

## Jak to funguje

ReaStage mluví s REAPERem dvěma kanály současně a **oba jsou potřeba**:

| Kanál | Směr | Port | K čemu |
|---|---|---|---|
| Web interface (HTTP) | ReaStage → REAPER | 9123 | seznam regionů, příkazy (play/pauza, skok na pozici), načtení stavu při startu |
| OSC | REAPER → ReaStage | 9124 | průběžné hlášení pozice, taktu, tempa a stavu transportu |

Když běží jen HTTP, aplikace vidí písničky, ale pozice se nehýbe. Když běží jen OSC,
nenačte se playlist.

## Požadavky

- **REAPER** (testováno na v7)
- **Windows 10/11 x64** nebo **Linux x64**
- Nic dalšího — ReaStage se publikuje jako self-contained, .NET runtime není potřeba
  instalovat

## Instalace na Windows

1. Rozbal balíček ReaStage do libovolné složky.
2. Spusť `ReaStage.exe`.
3. Pokračuj sekcí [Nastavení REAPERu](#nastavení-reaperu).

## Instalace na Linux

### 1. Systémové závislosti

Skript doinstaluje knihovny, které potřebuje Avalonia (X11, OpenGL, fonty) a .NET:

```bash
sudo ./install-deps.sh
```

Skript je psaný pro distribuce s `apt` (Debian, Ubuntu, Mint). Na jiných distribucích
nainstaluj ekvivalentní balíčky ručně — jejich seznam je přímo ve skriptu.

### 2. REAPER

REAPER se na Linuxu neinstaluje z balíčkovacího systému, stáhni ho z
[reaper.fm/download.php](https://www.reaper.fm/download.php) (položka *Linux x86_64*):

```bash
tar -xf reaper*_linux_x86_64.tar.xz
cd reaper_linux_x86_64
./install-reaper.sh --install ~/opt --integrate-desktop
```

Instalátor je součástí archivu od Cockos a vytvoří i položku v nabídce aplikací.

### 3. ReaStage

Z rozbaleného balíčku ReaStage spusť:

```bash
./install-ReaStage.sh
```

Skript nainstaluje aplikaci do `~/.local/share/ReaStage`, vytvoří položku v nabídce
aplikací a symlink `~/.local/bin/reastage`, takže ji jde spustit i z terminálu příkazem
`reastage`.

Odinstalace:

```bash
./install-ReaStage.sh --uninstall
```

> Pokud `reastage` z terminálu nejde spustit, chybí ti `~/.local/bin` v `PATH`.

## Nastavení REAPERu

Tohle je jediná část, kterou musíš proklikat ručně. Otevři
**Options → Preferences → Control/OSC/web**.

### 1. Zkopíruj OSC pattern

Soubor `ReaStage.ReaperOSC` (je v balíčku ReaStage) patří do OSC složky REAPERu:

| Systém | Cesta |
|---|---|
| Windows | `%AppData%\REAPER\OSC` |
| Linux | `~/.config/REAPER/OSC` |

Nejjistější cesta: v dialogu OSC zařízení rozbal **Pattern config** a zvol
**(open config directory)** — REAPER otevře přesně tu správnou složku. Po zkopírování
souboru rozbal seznam znovu a dej **(refresh list)**, jinak se `ReaStage` v nabídce
neobjeví.

### 2. Přidej OSC zařízení

Tlačítko **Add** → *Control surface mode:* **OSC (Open Sound Control)**:

| Položka | Hodnota |
|---|---|
| Device name | `ReaStage` |
| Pattern config | `ReaStage` |
| Mode | `Device IP/port [send only]` |
| Device port | `9124` |
| Device IP | `127.0.0.1` |
| Allow binding messages to REAPER actions and FX learn | zaškrtnuto |
| Outgoing max packet size | `1024` |
| Wait between packets | `10` ms |

`Device IP` nech na `127.0.0.1`, pokud běží REAPER i ReaStage na stejném počítači.
Když je ReaStage jinde (nebo ve WSL), vyplň jeho IP adresu.

### 3. Přidej webové rozhraní

Znovu **Add** → *Control surface mode:* **Web browser interface**:

| Položka | Hodnota |
|---|---|
| Run web server on port | `9123` |
| Username:password | prázdné |
| Default interface | `basic.html` |

Heslo nech prázdné — ReaStage se autentizovat neumí.

### 4. Zvyš frekvenci aktualizací

Dole v **Control/OSC/web** je **Control surface display update frequency**, výchozí
**15 Hz**. To znamená, že se pozice hlásí jen ~15× za sekundu, tedy s prodlevou až 67 ms.
Pro plynulý progress a hlavně pro **vizuální metronom** to nestačí — nastav **30 až 50 Hz**.

> Ovlivňuje to i přesnost detekce konce písničky. Hodnota `Wait between packets`
> u OSC zařízení je něco jiného (rozestup odesílaných paketů) a na 10 ms ji můžeš nechat.

### Kontrola

V seznamu v Preferences musí být obě zařízení:

```
OSC: ReaStage
Web browser interface: http://<ip>:9123
```

Spusť ReaStage. Když je vše správně, uvidíš písničky z projektu a po stisku mezerníku
se rozjede progress. Když se přes celé okno objeví **REAPER nedostupný**, nefunguje
HTTP kanál — viz [Řešení potíží](#řešení-potíží).

## Nastavení ReaStage

Hamburger vlevo nahoře → **Nastavení**.

| Volba | Význam |
|---|---|
| Klávesy | Play/pauza, předchozí, další. Klikni na tlačítko a stiskni klávesu; Esc zruší. Dvě akce nesmí mít stejnou klávesu. |
| Práh šipky zpět | Za kolik sekund od začátku písně skočí šipka doleva na začátek **aktuální** písně místo na předchozí (výchozí 3 s). |
| Velikost písma | 50–200 %, projeví se jen ve stage view. Pro čitelnost z dálky. |
| Vizuální metronom | Zapne/vypne tečky ve stavovém řádku. |
| REAPER: host a porty | Musí sedět s nastavením v REAPERu. **Projeví se až po restartu aplikace.** |
| Aktualizace regionů | Jak často se na pozadí načítá seznam regionů (výchozí 10 s). |
| Webové ovládání | Zapnutí a port serveru pro mobil (výchozí vypnuto, port 9125). Projeví se po restartu. |

Počet zobrazených písní se nenastavuje — stage view vypíše tolik, kolik se vejde na
obrazovku.

## Požadavky na projekt v REAPERu

- **Jedna písnička = jeden region.** Marker nestačí.
- **Mezi regiony musí být chvíle ticha.** Konec písně se hlídá podle hlášené pozice,
  takže detekce má drobné zpoždění. Kdyby regiony navazovaly natěsno, stihl by se
  ozvat začátek další písně, než ReaStage pošle Stop.
- **Nepřečíslovávej regiony.** Playlisty se na regiony vážou přes jejich ID.
  Chybějící region editor playlistů zvýrazní, ale spárovat ho zpátky neumí.

## Ovládání

| Akce | Klávesa | Myš |
|---|---|---|
| Play / pauza | Mezerník | klik na kartu aktuální písně nebo ⏯ dole |
| Předchozí píseň | ← | ⏮ dole |
| Další píseň | → | ⏭ dole |
| Skok na jinou viditelnou píseň | — | první klik ji odjistí, druhý do 4 s potvrdí |
| Zavřít panel / zrušit odjištění | Esc | — |

Šipka doleva se řídí prahem: na začátku písně skočí na předchozí, dál v písni na její
začátek. Dvoukrokový skok je pojistka proti překliku na pódiu.

Transportní klávesy fungují jen na hlavní obrazovce — v editoru playlistů a v nastavení
jsou vypnuté, aby nekolidovaly s psaním.

## Ovládání z mobilu

V nastavení zapni **Webové ovládání**, restartuj aplikaci a na telefonu ve stejné síti
otevři `http://<ip-počítače>:9125`.

Stránka ukazuje jen stage view. Gesta:

| Gesto | Akce |
|---|---|
| tap na aktuální píseň | play / pauza |
| tap na jinou píseň | první odjistí, druhý potvrdí skok |
| swipe doleva / doprava | další / předchozí píseň |
| swipe nahoru / dolů | scroll playlistu (po chvíli se vrátí na aktuální píseň) |

> **Web nemá žádné heslo.** Kdokoli v síti, kdo zná adresu, ti může ovládat přehrávání.
> Používej ho jen na důvěryhodné síti.

## Kde se ukládají data

| Systém | Složka |
|---|---|
| Windows | `%AppData%\ReaStage\` |
| Linux | `~/.config/ReaStage/` |

Obsahuje `settings.json` (nastavení), `playlists.json` (playlisty) a `logs/`
(denní logy — první místo, kam se podívat, když něco nefunguje).

## Sestavení ze zdrojů

Potřebuješ .NET 10 SDK.

```bash
dotnet build ReaStage/ReaStage.csproj
dotnet test ReaStage.Tests/ReaStage.Tests.csproj
```

Publikace (self-contained, runtime je součástí balíčku):

```bash
dotnet publish ReaStage/ReaStage.csproj -c Release -r win-x64
dotnet publish ReaStage/ReaStage.csproj -c Release -r linux-x64
```

Pro Linux je připravený profil `Properties/PublishProfiles/Linux.pubxml` (single-file).
Do výstupní složky se vedle spustitelného souboru kopírují i `install-deps.sh`,
`install-ReaStage.sh` a `ReaStage.ReaperOSC`, takže publikovaná složka je rovnou
instalační balíček.

Trimming je vypnutý záměrně — Avalonia i webový server stojí na reflexi.

## Řešení potíží

**Přes celé okno svítí „REAPER nedostupný"**
Nejede HTTP kanál. Zkontroluj, že je v REAPERu přidané *Web browser interface* na portu
9123 a že port v nastavení ReaStage sedí. Rychlý test v prohlížeči:
`http://localhost:9123/_/TRANSPORT` musí vrátit řádek začínající `TRANSPORT`.

**Písničky se načtou, ale pozice stojí**
Nejede OSC. Ověř port 9124 na obou stranách, `Mode` musí být *Device IP/port [send only]*
a `Device IP` musí mířit na počítač s ReaStage. Napoví i log v `logs/`.

**Nezobrazuje se BPM ani metronom**
V OSC zařízení není zvolený `Pattern config: ReaStage`, nebo se pattern soubor nezkopíroval
do správné složky. Po nakopírování je potřeba **(refresh list)**.

**Metronom se rozchází s hudbou**
Zvyš *Control surface display update frequency* (viz [krok 4](#4-zvyš-frekvenci-aktualizací)).
Při výchozích 15 Hz může tečka naskočit až o 67 ms později.

**Seznam písní neodpovídá projektu**
Regiony se načítají periodicky (výchozí 10 s). V editoru playlistů je tlačítko pro
okamžitou aktualizaci.

**Změnil jsem porty a nic se nestalo**
Host a porty se čtou při startu — restartuj aplikaci.
