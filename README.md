# Reakcióidő-mérés szemkövetéssel

Windows-alkalmazás vizuális reakcióidő mérésére, két módban:

- **Normál reakcióidő:** a résztvevő ránéz a megjelenő ingerre, és reagál.
- **Periférikus reakcióidő:** a résztvevő a középső fixációs keresztet nézi,
  és úgy reagál a periférián megjelenő ingerre, hogy nem néz oda.

A reakcióidőt hardver méri. Egy ESP mikrokontroller egy fotodiódával érzékeli,
mikor jelenik meg ténylegesen az inger a kijelzőn, és ehhez képest méri a
választ. A PC a kijelző soronkénti kirajzolásából adódó eltérést
(scanout-korrekció) számolja ki és küldi el. Egy Tobii Pro szemkövető a próba
teljes ideje alatt rögzíti a tekintetet. Ebből dől el, hogy a résztvevő
betartotta-e az utasítást (a normál módban odanézett, a periférikus módban nem).

## Fő jellemzők

- Az inger és a fotodióda-jelző ugyanabban a képkockában jelenik meg. A
  scanout-korrekció a kijelző pontos képidőzítéséből számolódik: tört
  frekvencia, képkockák közti holtidő.
- Az inger mérete, excentricitása és a tekintet-ellenőrzés sugara látószögben
  (fokban) van megadva. A kijelző fizikai méretét a Tobii display area adja, a
  nézési távolságot a szemkövető méri.
- A kísérlet menete: gyakorló próbák, az érvénytelen próbák ismétlése,
  kiszámíthatatlan (csonkolt exponenciális) előperiódus, teljes képernyő,
  megszakítás Esc-kel.
- A kísérleti paraméterek egy JSON-beállításfájlban vannak, újrafordítás nélkül
  módosíthatók.
- A mentés próbánként történik, metaadatokkal (kijelző, időzítés, távolság,
  beállítások, programverzió), és külön fájlba kerülnek az időbélyeges
  tekintetminták is.

## Követelmények

| | |
|---|---|
| Operációs rendszer | Windows 10/11, 64 bit |
| Fordításhoz | .NET 9 SDK (vagy Visual Studio 2022, .NET asztali fejlesztés munkaterhelés) |
| Szemkövető | Tobii Pro SDK-val kompatibilis Tobii szemkövető |
| Kalibrációhoz | Tobii Pro Eye Tracker Manager (a Tobii honlapjáról tölthető le) |
| Mérőhardver | ESP mikrokontroller fotodiódával és válaszgombbal (lásd lent) |

A Tobii Pro SDK a fordításkor a nuget.org-ról töltődik le (`Tobii.Research.x64`).
A licence csak nem kereskedelmi, kutatási célú felhasználást enged.

## Hardver-összeállítás

> **TODO:** az ESP firmware forráskódja, a kapcsolási rajz (fotodióda-áramkör,
> válaszgomb/egér bekötése) és az alkatrészlista még nincs a repóban. A mérés
> reprodukálhatóságához ezeket ide kell pótolni, például egy `firmware/` és egy
> `hardware/` mappába.

A szoftver oldaláról a hardvernek ennyit kell teljesítenie:

1. **ESP kapcsolat:** USB soros port, alapértelmezetten 115200 baud. A program a
   portot az USB-azonosítók alapján maga keresi meg (ESP32 natív USB, CP210x,
   CH340/CH9102, FTDI). Több eszköz esetén a portot a beállításfájlban kell
   megadni (`Hardware.EspPortName`).
2. **Protokoll:** próbánként a PC egy előjeles, µs-ban kifejezett
   scanout-korrekciót küld, az ESP pedig a korrigált reakcióidőt válaszolja
   µs-ban. Lásd: [docs/SCANOUT_PROTOCOL.md](docs/SCANOUT_PROTOCOL.md).
3. **Fotodióda:** a mérési nézet teljes képernyőn fut. A fotodiódát a mérőfelület
   jobb alsó sarkában lévő 56 × 56 képpontos négyzetre, középre kell
   felhelyezni, a mérési nézet megnyitása után. A négyzet fekete, amíg nincs
   inger, és fehér, amíg az inger látszik.
4. **Tobii:** a szemkövető display area-ját a Tobii Pro Eye Tracker Managerben a
   mérést futtató monitorra kell beállítani. Enélkül a mérés nem indul el,
   mert a kijelző fizikai mérete nem ismert.

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
| `--dev` vagy `REACTIONTIME_DEV_MODE=1` | fejlesztői mód: tekintet-overlay a főablakon, scanout-oszlop a statisztikában |
| `REACTIONTIME_DATA_DIR=<mappa>` | az adatmappa helye (alapértelmezetten `Dokumentumok\ReakcioidoMeres`) |

## Használat röviden

1. **Kalibráció:** a meglévő kalibráció használata vagy újrakalibrálás (Tobii
   Pro Eye Tracker Manager). A program ellenőrzi, hogy érkezik-e érvényes
   tekintetadat.
2. **Mérés:** mód kiválasztása, a résztvevő elolvassa az instrukciót, majd
   „Mérés indítása”. A program először 1 másodpercig méri a nézési távolságot,
   utána jönnek a gyakorló és a tesztpróbák.
3. **Statisztika:** a korábbi mérések listája, próbánkénti eredménnyel és
   érvénytelenségi okkal. Az összesítés (átlag, medián, szórás) csak az
   érvényes tesztpróbákból készül.

Részletes leírás: [docs/FELHASZNALOI_UTMUTATO.md](docs/FELHASZNALOI_UTMUTATO.md).

## Adatok és beállítások

Minden fájl a `Dokumentumok\ReakcioidoMeres` mappába kerül:

- `beallitasok.json`: kísérleti paraméterek;
- `measurements\`: mérésenként egy CSV;
- `measurements\gaze\`: tekintetminták;
- `logs\`: napló.

A mezők és az adatformátum leírása:
[docs/MERESI_BEALLITASOK.md](docs/MERESI_BEALLITASOK.md).

## Architektúra

Rétegzett felépítés, a mérési folyamatra MVP (Model–View–Presenter) mintával,
konstruktoros függőséginjektálással:

| Mappa | Réteg | Tartalom |
|---|---|---|
| `Domain/` | modell | eredmények, beállítások, próbaterv, látószög, scanout-képlet; WinForms nélkül |
| `Measurement/` | alkalmazáslogika | `MeasurementPresenter`, `IMeasurementView`, mérési módok, a külső függőségek interfészei |
| `Infrastructure/` | megvalósítások | Tobii, ESP/soros port, CSV-tárolás, JSON-beállítások, képidőzítés |
| `UI/` | megjelenítés | WinForms nézetek (passzív nézetek) |
| `Diagnostics/` | napló | fájlba író napló |
| `Program.cs` | composition root | a függőségek létrehozása és összekötése |

Részletek, diagramok, szálkezelés és bővítési pontok:
[docs/FEJLESZTOI_DOKUMENTACIO.md](docs/FEJLESZTOI_DOKUMENTACIO.md).

## Ismert korlátok

- A WinForms/GDI rajzolás nincs a monitor frissítéséhez kötve. Az inger és a
  jelző egy képkockába kerül, de a megjelenés pontos idejét a fotodióda méri,
  nem a szoftver.
- A scanout-korrekció fentről lefelé haladó, állandó sorfrekvenciájú
  kirajzolást feltételez, változó frissítés (VRR) nélkül. Lásd:
  [docs/SCANOUT_PROTOCOL.md](docs/SCANOUT_PROTOCOL.md).
- A látószög-átváltás feltételezi, hogy a szem a fixációs kereszttel szemben van.
- A kalibráció pontosságát (fokban) a program nem méri.

## Mesterséges intelligencia használata

> **TODO:** igazítsd az egyetemed előírásaihoz.

A fejlesztés során AI-alapú kódolási asszisztensek is közreműködtek (OpenAI
Codex, Anthropic Claude Code). Az általuk írt vagy módosított részeket tartalmazó
commitokban a `Co-Authored-By` sor ezt jelzi.

## Licenc

> **TODO:** licenc még nincs megadva. A Tobii Pro SDK saját licence (csak nem
> kereskedelmi kutatási célra) a projekt licencétől függetlenül érvényes.
