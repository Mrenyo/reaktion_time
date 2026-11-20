# Reakcióidő-mérés szemkövetéssel

Windows-alkalmazás vizuális reakcióidő mérésére (verzió: **2.0.0**). A mérés
minden paramétere a programban állítható; a tekintet-ellenőrzésnek három módja
van:

- **Normál reakcióidő:** a résztvevő ránéz a megjelenő ingerre, és reagál.
- **Periférikus reakcióidő:** a résztvevő a fixációs keresztet nézi (középen
  vagy egy kijelölt helyen), és úgy reagál a máshol megjelenő ingerre, hogy nem
  néz oda.
- **Tekintet-ellenőrzés nélkül:** minden időben érkező válasz érvényes; ehhez
  szemkövető és kalibráció sem kell.

A válasz kétféle lehet: bal gombos kattintás bárhol, amint megjelenik az inger, vagy
kattintás magára az ingerre (ilyenkor a mellékattintás külön okkal,
„Mellékattintás”-ként érvénytelen).

A reakcióidőt hardver méri. Egy ESP32-S3 mikrokontroller (a továbbiakban: ESP)
egy fotodiódával érzékeli, mikor jelenik meg ténylegesen az inger a kijelzőn,
és ehhez képest méri a választ. A PC a kijelző soronkénti kirajzolásából adódó eltérést
(scanout-korrekció) számolja ki és küldi el. Ha van csatlakoztatott Tobii Pro
szemkövető, az a próba teljes ideje alatt rögzíti a tekintetet. Ebből dől el,
hogy a résztvevő betartotta-e az utasítást (a normál módban odanézett, a
periférikus módban nem).

## Fő jellemzők

- Az inger és a fotodióda-jelző ugyanabban a képkockában jelenik meg. A
  scanout-korrekció a kijelző pontos képidőzítéséből számolódik: tört
  frekvencia, képkockák közti holtidő.
- Minden paraméter a **Testreszabás** ablakban állítható: a mért és a gyakorló
  ingerek száma, az időzítés, az inger és a háttér színe, az inger mérete és
  távolsága, a tekintet-ellenőrzés módja, a fixációs pont helye (a
  mérőfelületen kijelölhető) és az elfogadási rádiusz. Az ablak előnézetet és
  valós méretű ingert is mutat. A beállítások egy JSON-fájlba kerülnek, amely
  kézzel is szerkeszthető.
- Minden méret és távolság képpontban van megadva. A fizikai méret a
  monitortól függ; ha van szemkövető, a metaadatok a kijelző fizikai méretét és
  próbánként a szem távolságát is rögzítik, így a látószög utólag kiszámítható.
- A kísérlet menete: előtér a résztvevőnek szóló instrukcióval, majd teljes
  képernyős mérési jelenet (csak a háttér, a fehér-fekete kereszt, a
  fotodióda-jelző és az inger), rövid várakozás, gyakorló, majd pontosan a
  beállított számú mért inger. Az érvénytelen próbák nem pótlódnak. Az
  előperiódus kiszámíthatatlan (csonkolt exponenciális), a mérés Esc-kel
  megszakítható. Az elsietett válasz (kattintás még az inger előtt, vagy
  100 ms-nál rövidebb reakcióidő) külön okkal érvénytelen.
- A mentés próbánként történik, metaadatokkal (kijelző, időzítés, beállítások,
  véletlenmag, programverzió), és külön fájlba kerülnek az időbélyeges
  tekintetminták is.

## Követelmények

| | |
|---|---|
| Operációs rendszer | Windows 10/11, 64 bit |
| Fordításhoz | .NET 10 SDK (vagy a .NET 10-et támogató Visual Studio, .NET asztali fejlesztés munkaterheléssel) |
| Szemkövető | Tobii Pro SDK-val kompatibilis Tobii szemkövető; csak a tekintet-ellenőrzéses méréshez kötelező |
| Kalibrációhoz | Tobii Pro Eye Tracker Manager (a Tobii honlapjáról tölthető le) |
| Mérőhardver | ESP32-S3 mikrokontroller fotodiódával és válaszgombbal (lásd lent) |
| Firmware fordításához | ESP-IDF (lásd [Firmware és hardver](#firmware-és-hardver)) |

A Tobii Pro SDK a fordításkor a nuget.org-ról töltődik le (`Tobii.Research.x64`).
A licence csak nem kereskedelmi, kutatási célú felhasználást enged.

## Firmware és hardver

Az ESP firmware-e a [`firmware/`](firmware) mappában van (ESP-IDF projekt:
`firmware/main/main.c`, `CMakeLists.txt`, `sdkconfig.defaults`).

| | |
|---|---|
| Lap | ESP32-S3 (N16R8: 16 MB flash, 8 MB PSRAM), a `sdkconfig.defaults` szerint |
| Keretrendszer | ESP-IDF |
| Soros port | UART0, 115200 baud, soronkénti szöveges protokoll |
| Válaszgomb | GPIO5, aktív alacsony, belső felhúzó ellenállással; megszakítás a lefutó élre; egy alacsony jel után 30 ms-ig lenyomottnak számít (pergésmentesítés) |
| Fotodióda | GPIO4 (ADC1, 3-as csatorna), 12 bit, 0 dB csillapítás; a küszöb 475 (sötét ≈ 0, világos ≈ 950) |
| Időmérés | `esp_timer`, mikroszekundumban, 64 bites időbélyegekkel |
| Processzor-órajel | 240 MHz (`sdkconfig.defaults`) |
| Verzió | 1.1.0 (`FW_VERSION`); a PC `VERSION` kéréssel kérdezi le |
| Diagnosztika | `DIAG` (a mérő ciklus periódusideje, ADC-szintek) és `ADC` (egy nyers olvasás) parancs soros monitorból, lásd [docs/SCANOUT_PROTOCOL.md](docs/SCANOUT_PROTOCOL.md#diagnosztikai-parancsok) |

A firmware állapotgépe próbánként: `TETLEN` → `VAR_FENY` (a scanout-korrekció
megérkezése után a fotodióda fekete → fehér átmenetére vár) → `VAR_GOMB` (a
fényjel utáni első gomblenyomásra vár) → `KESZ` (a reakcióidő lekérhető). A
fényjel előtti gomblenyomást az ESP nem veszi figyelembe. A gombot és a
fotodiódát egy külön feladat (task) figyeli folyamatosan, az 1-es processzormagra
rögzítve; a soros parancsokat a fő feladat dolgozza fel.

Fordítás és feltöltés a `firmware` mappából, telepített ESP-IDF környezetben:

```bash
idf.py build flash
```

A kapcsolási rajz (fotodióda-áramkör, a válaszgomb, illetve az egér bekötése) és
az alkatrészlista nincs a repóban.

A szoftver oldaláról a hardvernek ennyit kell teljesítenie:

1. **ESP kapcsolat:** USB soros port, alapértelmezetten 115200 baud (8 adatbit,
   paritás nélkül, 1 stopbit, áramlásvezérlés nélkül). A program a
   portot az USB-azonosítók alapján maga keresi meg (ESP32 natív USB, CP210x,
   CH340/CH9102, FTDI). Több eszköz esetén a portot a Testreszabás Hardver
   fülén kell kiválasztani (a fájlban: `Hardware.EspPortName`).
2. **Protokoll:** a mérés indításakor a PC `CHECK` üzenetet küld, amire az
   ESP-nek `OK`-val kell válaszolnia (enélkül a mérés nem indul), majd `VERSION`
   üzenettel lekéri a firmware verzióját (ha a régebbi firmware nem válaszol, a
   mérés ettől még indul). Próbánként a
   PC egy előjeles, µs-ban kifejezett scanout-korrekciót küld; a próba végét a
   PC által érzékelt kattintás jelenti, ekkor a PC `RTIME` üzenettel lekéri, az
   ESP pedig µs-ban válaszolja a korrigált reakcióidőt, vagy `NA`-t, ha a
   próbában nem mért (nem érzékelte a fényjelet vagy a gomblenyomást). Ha az
   ESP `NA`-t küld, nem válaszol, vagy mérés közben kiesik, a mérés ESP-adat
   nélkül, a PC-n mért időkkel folytatódik. Lásd: [docs/SCANOUT_PROTOCOL.md](docs/SCANOUT_PROTOCOL.md).
3. **Fotodióda:** a mérési nézet teljes képernyőn fut. A fotodiódát a képernyő
   jobb alsó sarkában lévő 56 × 56 képpontos négyzetre (a jobb és az alsó
   széltől 20 képpontra), középre kell felhelyezni, a mérési nézet megnyitása
   után. Ha a fotodióda máshová került, a négyzet a Testreszabás Hardver fülén
   a szenzor alá helyezhető. A négyzetet egy 16 képpontos fekete sáv veszi
   körül, bármilyen háttérszínnél; a mérés indítása előtt egy vékony szürke
   keret is segíti a felhelyezést. A négyzet fekete, amíg nincs inger, és fehér, amíg az inger
   látszik. Az ESP reakcióideje a négyzet fekete → fehér váltásához van kötve;
   az alapértelmezettől eltérő színeknél ellenőrző mérés ajánlott (lásd
   [docs/SCANOUT_PROTOCOL.md](docs/SCANOUT_PROTOCOL.md)).
4. **Tobii (csak tekintet-ellenőrzéshez):** a szemkövető display area-ját a Tobii
   Pro Eye Tracker Managerben a mérést futtató monitorra kell beállítani.
   Enélkül a tekintet-ellenőrzéses mérés nem indul el, mert a display area
   vetíti a tekintetet a képernyőre. Tekintet-ellenőrzés nélkül a szemkövető
   elhagyható.

## Fordítás és futtatás

```bash
dotnet build ReactionTimeMeasurementApp.sln -c Release
```

```bash
dotnet run --project ReactionTimeMeasurementApp.csproj -c Release
```

Visual Studióban a `ReactionTimeMeasurementApp.sln` megnyitása után az F5 indítja.

Futásidejű kapcsolók:

| Kapcsoló | Hatás |
|---|---|
| `--dev` vagy `REACTIONTIME_DEV_MODE=1` | fejlesztői mód: tekintet-overlay a főablakon (a mérési nézeten rejtve), scanout-oszlop a statisztikában |
| `REACTIONTIME_DATA_DIR=<mappa>` | az adatmappa helye (alapértelmezetten `Dokumentumok\ReakcioidoMeres`) |

## Használat röviden

1. **Testreszabás** (Mérés kártya): a mérés beállításai, előnézettel; a
   fixációs pont a teljes képernyős mérőfelületen jelölhető ki.
2. **Kalibráció** (csak tekintet-ellenőrzéshez): a meglévő kalibráció használata
   vagy újrakalibrálás (Tobii Pro Eye Tracker Manager). A program ellenőrzi,
   hogy érkezik-e érvényes tekintetadat.
3. **Mérés** (Mérés kártya): a teljes képernyős nézet először az előteret
   mutatja; a résztvevő elolvassa az instrukciót, majd „Mérés indítása”. A
   mérési jelenet megjelenése után rövid várakozás következik (alapértelmezetten
   2 másodperc), utána jönnek a gyakorló és a mért ingerek. A mérés végén a
   nézet visszatér az előtérbe az eredménnyel.
4. **Statisztika:** a korábbi mérések listája, próbánkénti eredménnyel és
   érvénytelenségi okkal (időtúllépés, elsietett válasz, mellékattintás,
   tekintethiba, kevés tekintetadat, nincs szemkövető). Az összesítés (átlag,
   medián, szórás) csak az érvényes mért próbákból készül.

Részletes leírás: [docs/FELHASZNALOI_UTMUTATO.md](docs/FELHASZNALOI_UTMUTATO.md).

## Adatok és beállítások

Minden fájl a `Dokumentumok\ReakcioidoMeres` mappába kerül:

- `beallitasok.json`: kísérleti paraméterek (az előző változat:
  `beallitasok.json.bak`);
- `measurements\`: mérésenként egy CSV;
- `measurements\gaze\`: tekintetminták;
- `logs\`: napló.

A mezők és az adatformátum leírása:
[docs/MERESI_BEALLITASOK.md](docs/MERESI_BEALLITASOK.md).

## Architektúra

Rétegzett felépítés, MVP (Model–View–Presenter) mintával (mérés, kalibráció,
statisztika, beállítások), konstruktoros függőséginjektálással:

| Mappa | Réteg | Tartalom |
|---|---|---|
| `Domain/` | modell | eredmények, beállítások, tartományaik, hibáik és figyelmeztetéseik, próbaterv, ingerelhelyezés (képpont-geometria), scanout-képlet; WinForms nélkül |
| `Measurement/` | alkalmazáslogika | a presenterek és nézetinterfészeik (`MeasurementPresenter` + `TrialRunner`, `CalibrationPresenter`, `StatisticsPresenter`, a beállítási ablaké), a próbák kiértékelése (`TrialEvaluator`), tekintetszabályok (`IGazeValidationRule`), a külső függőségek interfészei |
| `Infrastructure/` | megvalósítások | Tobii, ESP/soros port, CSV-tárolás, JSON-beállítások, képidőzítés |
| `UI/` | megjelenítés | WinForms nézetek, a Testreszabás ablak, a fixációs pont és a fotodióda-jelző kijelölője |
| `firmware/` | mérőhardver | az ESP32-S3 firmware-e (ESP-IDF, C) |
| `Diagnostics/` | napló | háttérszálon fájlba író napló |
| `Program.cs` | composition root | a függőségek létrehozása és összekötése |

Részletek, diagramok, szálkezelés és bővítési pontok:
[docs/FEJLESZTOI_DOKUMENTACIO.md](docs/FEJLESZTOI_DOKUMENTACIO.md).

## Ismert korlátok

- A WinForms/GDI rajzolás nincs a monitor frissítéséhez kötve. Az inger és a
  jelző egy képkockába kerül, de a megjelenés pontos idejét a fotodióda méri,
  nem a szoftver.
- A scanout-korrekció fentről lefelé haladó, állandó sorfrekvenciájú
  kirajzolást feltételez. A változó frissítési frekvencia (G-Sync, FreeSync,
  VRR) érvényteleníti ezt a modellt, és a program nem ismeri fel: a méréshez ki
  kell kapcsolni. Lásd: [docs/SCANOUT_PROTOCOL.md](docs/SCANOUT_PROTOCOL.md).
- A tekintet-ellenőrzés válaszablaka akkor kezdődik, amikor az inger
  kirajzolásának hívása visszatér. Ez 1–2 képkockával megelőzi az inger
  tényleges megjelenését a kijelzőn.
- A helyi (PC-n mért) reakcióidő időbélyege az egérüzenet feldolgozásakor
  készül, nem a gomb lenyomásakor. A referencia az ESP által mért idő.
- Az `RTIME` kérésre adott válasz nem tartalmaz próbaazonosítót: a PC a választ
  az éppen futó próbához rendeli.
- A méretek képpontban vannak megadva, ezért a fizikai méret és a látószög a
  monitortól és az ülési távolságtól függ. A látószög csak szemkövetővel
  végzett mérésnél számítható ki utólag (a kijelző mérete és a szem távolsága a
  metaadatokban), és ez is feltételezi, hogy a szem a fixációs kereszttel
  szemben van.
- Az ESP reakcióideje a fotodióda-jelző fekete → fehér váltásához van kötve. Az
  inger saját színátmenetének válaszideje ettől eltérhet, ezért nem
  alapértelmezett színeknél ellenőrző mérés ajánlott.
- A kalibráció pontosságát (fokban) a program nem méri.

## Verzió

A jelenlegi verzió a **2.0.0** (`ReactionTimeMeasurementApp.csproj`,
`<Version>`; a mérési fájlokba `AppVersion` metaadatként kerül). A korábbi
1.x verziókhoz képest két formátum nem visszafelé kompatibilisen változott:

- **A beállításfájl 2-es formátumú** (`SettingsVersion: 2`): képpontos értékek,
  egyetlen mérési beállításkészlet a tekintet-ellenőrzés módjával, ismétlés
  nélküli próbaszám. Az 1.x beállításfájlját a program nem olvassa be: a mérést a
  Testreszabásban újra be kell állítani (mentéskor a régi fájl `.bak`
  kiterjesztéssel megmarad).
- **A mérési fájlok 3-as formátumúak** (`FormatVersion=3`): a
  `PlannedTestTrials` oszlop a pontosan lefutó mért próbák száma, a
  `StimulusEccentricityPx` képpontban adja az inger távolságát. A régebbi
  (1.x) mérési fájlokat a statisztika nem olvassa be:
  [docs/MERESI_BEALLITASOK.md](docs/MERESI_BEALLITASOK.md#formátumverziók).

A változások listája: [CHANGELOG.md](CHANGELOG.md).

## Mesterséges intelligencia használata

A fejlesztés során AI-alapú kódolási asszisztensek is közreműködtek (OpenAI
Codex, Anthropic Claude Code). Az általuk írt vagy módosított részeket tartalmazó
commitokban a `Co-Authored-By` sor ezt jelzi.

## Licenc

A projekthez még nincs licenc választva, a repó nem tartalmaz licencfájlt. A
Tobii Pro SDK saját licence (csak nem kereskedelmi, kutatási célú felhasználás)
a projekt licencétől függetlenül érvényes; a lefordított program mellé kerülő
Tobii-könyvtárakra is ez vonatkozik.
