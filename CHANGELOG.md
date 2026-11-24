# Változásnapló

A formátum a [Keep a Changelog](https://keepachangelog.com/hu/1.1.0/) ajánlását
követi. A főverzió akkor nő, ha a beállításfájl vagy a mérési fájl formátuma
nem visszafelé kompatibilisen változik. A verziókhoz nincs git-címke, ezért a
dátumok hiányoznak.

## [2.0.0] – kiadás előtt

A beállításfájl 2-es (`SettingsVersion: 2`), a mérési fájl 3-as
(`FormatVersion=3`) formátumú. A részletek:
[docs/MERESI_BEALLITASOK.md](docs/MERESI_BEALLITASOK.md).

### Hozzáadva

- **Scanout-validáció** (`scanout_validacio/`): önálló eszköz a scanout-korrekció
  ellenőrzésére két fotodiódával, saját firmware-rel és PC-programmal. A
  mérőprogram fordításából ki van zárva.
- **Testreszabás** ablak: minden mérési beállítás a programban állítható,
  előnézettel és valós méretű ingerrel; a hibákat és a figyelmeztetéseket az
  ablak fülenként jelzi.
- A fixációs pont és a fotodióda-jelző helye a teljes képernyős mérőfelületen
  kijelölhető (`Gaze.FixationPointX/Y`, `Hardware.PhotodiodeMarkerX/Y`).
- Mérés tekintet-ellenőrzés nélkül (`Gaze.Validation: None`): szemkövető és
  kalibráció nélkül is futtatható.
- Ingerre kattintós válaszmód (`Trials.ResponseMode: ClickOnStimulus`) és a
  `MissedStimulus` (mellékattintás) érvénytelenségi ok; a kattintás helye a
  `ClickX`, `ClickY` oszlopban.
- Elsietett válasz (`Anticipation`): kattintás az előperiódus alatt, vagy
  100 ms-nál rövidebb reakcióidő. Új oszlopok: `ActualForeperiodMs`,
  `PrematureClicks`; új metaadat: `AnticipationThresholdMs`.
- Állítható inger- és háttérszín, a jelenet utáni várakozás
  (`Trials.SceneSettleDelayMs`).
- Soros protokoll: `CHECK` → `OK` kapcsolatellenőrzés a mérés indításakor; a
  reakcióidőt a PC `RTIME` üzenettel kéri le, az ESP számmal vagy `NA`-val
  válaszol.
- Az ESP32-S3 firmware-e a repóban (`firmware/`, ESP-IDF).
- A mérés végén az előtér jelzi, hány mért próbánál nincs ESP-reakcióidő, és
  hány próbában nem érkezett tekintetadat.
- Áttűnés a nézetek között.
- Statisztika: **PC − ESP** oszlop (a rendszer késleltetése próbánként) és
  összesítés (átlag, medián, szórás); az idők egy tizedesjegyre kerekítve.
  A beolvashatatlan fájlokat és a kihagyott hibás sorokat a nézet jelzi.
- A fotodióda-jelző kijelölőjén célzóvonalak a jelző közepén át, keresztjellel
  a középtől 2,5 mm-re (a kijelző fizikai mérete a monitor EDID-adataiból).
- Firmware-verzió: `VERSION` → pl. `1.1.0`; a mérési fájlba
  `EspFirmwareVersion` metaadatként kerül.
- Firmware: `DIAG` és `ADC` diagnosztikai parancs (a mérő ciklus periódusideje, a
  fotodióda ADC-szintjei) a mérőlánc ellenőrzéséhez; a processzor-órajel 240 MHz.
- A szemkövető kiesésének kezelése mérés közben: tekintet-ellenőrzéses
  mérésnél a mérés leáll, anélkül tekintetadat nélkül folytatódik; a mérés
  végén figyelmeztetés jelzi.

### Megváltozott

- **Nem visszafelé kompatibilis:** a beállításfájl képpontos értékeket
  tartalmaz a látószög (fok) helyett, és egyetlen beállításkészletet a mérési
  módonkénti szakaszok helyett. Az 1.x fájlját a program nem olvassa be: a mérést
  a Testreszabásban újra be kell állítani (mentéskor a régi fájl `.bak`
  kiterjesztéssel megmarad).
- **Nem visszafelé kompatibilis:** a mérés pontosan a beállított számú mért
  próbából áll, az érvénytelen próbák nem pótlódnak. A `PlannedTestTrials`
  oszlop és a `MeasuredTrials` metaadat ezt a számot adja; az inger távolsága
  képpontban van (`StimulusEccentricityPx`).
- Ha az ESP egy próbában nem ad reakcióidőt, vagy a mérés közben kiesik, a
  mérés a PC-n mért idővel folytatódik.
- Az a próba, amelyben egyetlen tekintetminta sem érkezett, `TrackerUnavailable`.
- A mérési fájl minden írása a lemezre ürül.
- A mérési nézet elhagyásakor az előző mérés kimenetele törlődik az előtérről.
- A statisztika a méréseket háttérszálon olvassa be; az újrakalibrálást az
  érvényes tekintetadat ellenőrzése követi.
- A beállítások hibái és figyelmeztetései a Domain rétegben vannak
  (`SettingsIssue`, `ExperimentSettings.Validate`, `GetWarnings`); a
  `MeasurementPresenter` részei külön osztályokba kerültek (`SessionContext`,
  `SessionMetadata`, `LobbyText`, `TrialEvaluator`); a soros réteg három fájlra
  oszlik (`Serial`, `SerialPortConnection`, `EspPortFinder`); a főablak neve
  `MainForm`.
- Célkeretrendszer: `net10.0-windows`, `System.IO.Ports` 10.0.0.
- Válasznak csak a **bal** egérgomb lenyomása számít (az ESP is csak ezt
  érzékeli).
- A mérési és a tekintetfájl a mérés alatt nyitva marad (más program, pl. az
  Excel olvashatja); átmeneti zárolásnál a program újrapróbálkozik. A
  tekintetfájl sem írhat felül meglévő fájlt.
- A beolvasás a hibás vagy csonka próbasorokat kihagyja, a mérés többi része
  megmarad.
- A napló háttérszálon ír, így nem lassítja a soros olvasást és a
  kattintáskezelést.
- MVP a kalibrációs és a statisztika nézetre is (`CalibrationPresenter`,
  `StatisticsPresenter`); a próbák futtatása a `TrialRunner`-be, a mérési
  geometria a `SessionGeometryBuilder`-be került; a tartós fájlírás közös
  segédje a `DurableFile`.

### Javítva

- Kihúzott ESP után nyitva maradt a COM-port, így a következő mérés „Access
  denied” hibát kaphatott.
- Firmware: a 32 bites időbélyeg kb. 35,8 perc gombnyomás nélküli idő után
  túlcsordult, és a következő lenyomás elveszett; az időbélyegek 64 bitesek.
  Az ADC hibás olvasása nem ad hamis mintát.
- A mérési nézet `InvokeAsync`-ja ablakkezelő nélkül a háttérszálon futtatta a
  műveletet; a mérés megszakítása és a `CancellationTokenSource`
  felszabadítása versenyezhetett.
- A Tobii kalibrációs program megakadása nem akasztja meg a programot (10
  perces időkorlát); a Tobii-szolgáltatás felszabadítása megvárja a futó
  natív hívásokat; a képidőzítés lekérdezése topológiaváltáskor újrapróbál.

### Eltávolítva

- A látószög alapú beállítások és a nézési távolság
  (`StimulusDiameterDeg`, `MinEccentricityDeg`, `MaxEccentricityDeg`,
  `GazeValidityRadiusDeg`, `DefaultViewingDistanceCm`).
- Az érvénytelen próbák ismétlése (`RepeatInvalidTrials`, `MaxTestAttempts`) és
  az `AttemptLimitReached` állapot.
- A régi (1-es és 2-es formátumú, 1.x programmal készült) mérési fájlok
  beolvasása: a statisztika csak a 3-as formátumot olvassa, a régebbi fájlokat
  beolvashatatlanként jelzi. Az exe melletti `measurements` mappát sem olvassa
  már.

## [1.x]

A mérési fájl 2-es formátumú; a beállításfájl 1-es formátumú. A mérési fájlok
`AppVersion` metaadata alapján ismert változat: 1.1.0.

- Mérési módonként külön beállításszakasz (`NormalMode`, `PeripheralMode`),
  látószögben megadott méretekkel.
- A `TestTrials` a kívánt **érvényes** próbák száma: az érvénytelen próbák
  helyett újabb próba jön, legfeljebb `MaxTestAttempts`-ig.
- Gyakorló próbák, próbánkénti mentés metaadatokkal, külön fájl a
  tekintetmintáknak.
- Rétegzett felépítés, MVP a mérési folyamatra, konstruktoros
  függőséginjektálás; a Tobii Pro SDK NuGet-csomagból.

## Első változat

A mérési fájl 1-es formátumú (`FormatVersion` sor nélkül).

- Rögzített számú próba, gyakorlás nélkül (`TotalTrials`).
- Csak a befejezett mérés mentődik, az exe melletti `measurements` mappába.
