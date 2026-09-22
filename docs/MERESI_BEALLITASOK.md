# Mérési beállítások és adatformátum

## Az adatok helye

Minden fájl a felhasználó **Dokumentumok\ReakcioidoMeres** mappájába kerül.
Más mappa a `REACTIONTIME_DATA_DIR` környezeti változóval adható meg.

| Útvonal | Tartalom |
|---|---|
| `beallitasok.json` | a kísérleti paraméterek (lásd lent) |
| `measurements\<kezdés>_<mód>.csv` | egy mérés metaadatai és próbái |
| `measurements\gaze\<kezdés>_<mód>_gaze.csv` | a mérés összes tekintetmintája |
| `logs\log-<dátum>.txt` | napló |

A korábbi verziók az exe mellé mentettek (`measurements` mappa). A
statisztika ezeket a fájlokat is beolvassa.

## Beállításfájl (`beallitasok.json`)

Az első indításkor a program létrehozza az alapértékekkel. A módosítások a
következő mérésindításkor érvényesülnek, újrafordítás nélkül. Ha a fájl hibás,
a mérés nem indul el, és a hibaüzenet megmondja, melyik érték a rossz.
Megjegyzés (`//`) és hiányzó mező is megengedett: a hiányzó mező az adott mód
alapértékét kapja.

Általános mezők:

| Mező | Jelentés | Alapérték |
|---|---|---|
| `DefaultViewingDistanceCm` | nézési távolság, ha a Tobii nem tudja megmérni | 60 |
| `MinValidGazeSampleRatio` | ha a válaszablak tekintetmintáinak ennél kisebb hányada érvényes, a próba érvénytelen (`InsufficientGazeData`) | 0,5 |
| `Hardware.EspPortName` | az ESP soros portja (pl. `"COM5"`); üresen a program az USB-azonosítók alapján keresi meg | üres |
| `Hardware.EspBaudRate` | a soros port sebessége, egyezzen az ESP firmware-rel | 115200 |

A `NormalMode` és a `PeripheralMode` szakasz mezői:

| Mező | Jelentés | Normál / periférikus alapérték |
|---|---|---|
| `TestTrials` | ennyi **érvényes** tesztpróba a cél | 10 / 10 |
| `PracticeTrials` | gyakorló próbák a teszt előtt (mentődnek, de nem számítanak bele az eredménybe) | 3 / 3 |
| `RepeatInvalidTrials` | az érvénytelen próbák helyett új próba jön | true / true |
| `MaxTestAttempts` | a tesztpróbák legnagyobb száma | 20 / 20 |
| `ForeperiodDistribution` | az inger előtti várakozás eloszlása: `Exponential` vagy `Uniform` | Exponential |
| `ForeperiodMinMs`, `ForeperiodMaxMs` | a várakozás határai | 1000, 3000 |
| `ForeperiodExponentialMeanMs` | exponenciális eloszlásnál a minimum feletti rész várható értéke | 700 |
| `StimulusDiameterDeg` | az inger átmérője, fokban | 2 / 1 |
| `MinEccentricityDeg`, `MaxEccentricityDeg` | az inger távolsága a fixációs kereszttől, fokban | 3–15 / 8–20 |
| `GazeValidityRadiusDeg` | a tekintet-ellenőrzés sugara, fokban | 2 / 4 |
| `ReactionTimeoutMs` | ennyi idő után a próba időtúllépés | 3000 |

A csonkolt exponenciális várakozásnál az inger megjelenésének feltételes
valószínűsége a várakozás során közel állandó, így a résztvevő nem tudja
kiszámítani, mikor jön az inger.

Periférikus módban a `MinEccentricityDeg` értékének nagyobbnak kell lennie,
mint `GazeValidityRadiusDeg + StimulusDiameterDeg / 2`. Különben az ingerre
nézés is a fixációs ablakon belül maradhatna.

## Látószög-átváltás

- A kijelző fizikai mérete a **Tobii display area**-ból jön (Tobii Pro Eye
  Tracker Manager). Ha ez nincs beállítva, vagy a méretaránya nem egyezik a
  mérést futtató kijelzőével, a mérés nem indul el.
- A nézési távolságot a mérés elején a Tobii méri 1 másodpercig: a szem
  merőleges távolsága a kijelző síkjától, medián. Ha ez nem sikerül, a
  `DefaultViewingDistanceCm` érvényes. A forrást a `ViewingDistanceSource`
  metaadat rögzíti.
- Az átváltás feltételezi, hogy a szem a fixációs kereszttel szemben van. Az
  inger méretét a program a látótengelyen számolja (mint a PsychoPy „deg”
  egysége).
- Az excentricitás egyenletes eloszlású a tartományban, az irány véletlen. Ami
  nem fér el a képernyőn, azt a program újrasorsolja. Ha a legnagyobb érték
  sehol nem fér el, a még elférőt használja (`EffectiveMaxEccentricityDeg`).

## A mérési fájl (`measurements\*.csv`)

A fájl elején `# Kulcs=Érték` metaadat-sorok állnak: programverzió,
szemkövető, mintavételi frekvencia, kijelző felbontása és fizikai mérete,
pixelsűrűség, nézési távolság, pontos képfrissítési frekvencia és sorszámok,
a fotodióda-jelző helye, valamint az összes beállítás. Ezt a próbák sorai
követik, a végén pedig a `# EndedAt=` és a `# Status=` sor.

A mentés **próbánként** történik. Megszakítás után az eddigi próbák
megmaradnak. Ha a program a mérés közben leáll, a hiányzó `# Status=` sorból
látszik, hogy a mérés nem zárult le.

| `Status` | Jelentés |
|---|---|
| `Completed` | összegyűlt a kívánt számú érvényes próba |
| `AttemptLimitReached` | elfogyott a `MaxTestAttempts` |
| `Aborted` | megszakítás: Esc, visszalépés, ESP- vagy mentési hiba |
| (nincs sor) | a program a mérés közben leállt |

Próbaoszlopok:

| Oszlop | Jelentés |
|---|---|
| `Phase` | `Practice` vagy `Test` |
| `TrialNumber` | a próba sorszáma a fázisán belül |
| `TargetValidTrials` | a cél érvényes próbaszám |
| `EspReactionTimeMs` | a hardveresen (ESP + fotodióda) mért reakcióidő |
| `LocalReactionTimeMs` | a PC-n mért idő (a soros válasz beérkezéséig) |
| `Valid`, `InvalidReason` | `None`, `Timeout`, `TrackerUnavailable`, `InsufficientGazeData` (az érvényes minták aránya a `MinValidGazeSampleRatio` alatt van) vagy `GazeViolation` |
| `RecordedAt` | időbélyeg |
| `ForeperiodMs` | az inger előtti várakozás |
| `ScanoutCorrectionUs` | az ESP-nek küldött korrekció |
| `StimulusCenterX`, `StimulusCenterY`, `StimulusDiameterPx` | az inger helye a mérőfelületen, képpontban |
| `StimulusEccentricityDeg` | az inger távolsága a fixációs kereszttől |
| `ValidGazeRatio`, `GazeSamplesInWindow` | a válaszablak tekintetadatainak minősége |
| `MeanEyeDistanceMm` | a szem átlagos távolsága a kijelzőtől a próba alatt |

A tekintetminták fájlja (`gaze\*_gaze.csv`) mintánként egy sort tartalmaz. A
`Phase` + `TrialNumber` oszloppár köti a próbához, és minden sorban szerepel a
Tobii időbélyeg, az inger megjelenéséhez mért idő, a válaszablak-jelző, a
normalizált és a mérőfelületi koordináta, valamint a szem távolsága.
