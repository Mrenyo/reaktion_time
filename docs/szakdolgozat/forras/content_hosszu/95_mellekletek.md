#A A firmware forráskódja

Az időmérő egység teljes programja (`main/main.c`), 1.1.0-s változat. A program leírása az 5. fejezetben található.

!include main.c

#A Az alapértelmezett beállításfájl

A program az első indításkor ezt a fájlt hozza létre (`Dokumentumok\ReakcioidoMeres\beallitasok.json`). A mezők jelentését a 6.10. szakasz táblázata foglalja össze.

```
{
  "SettingsVersion": 2,
  "Trials": {
    "MeasuredTrials": 10,
    "PracticeTrials": 3,
    "ResponseMode": "ClickAnywhere",
    "ReactionTimeoutMs": 3000,
    "SceneSettleDelayMs": 2000,
    "ForeperiodDistribution": "Exponential",
    "ForeperiodMinMs": 1000,
    "ForeperiodMaxMs": 3000,
    "ForeperiodExponentialMeanMs": 700
  },
  "Stimulus": {
    "Color": "#FFFFFF",
    "BackgroundColor": "#1E1E1E",
    "DiameterPx": 60,
    "MinEccentricityPx": 150,
    "MaxEccentricityPx": 500
  },
  "Gaze": {
    "Validation": "LookAtStimulus",
    "FixationPlacement": "Center",
    "FixationPointX": null,
    "FixationPointY": null,
    "ValidityRadiusPx": 80,
    "MinValidGazeSampleRatio": 0.5
  },
  "Hardware": {
    "EspPortName": null,
    "EspBaudRate": 115200,
    "PhotodiodeMarkerX": null,
    "PhotodiodeMarkerY": null
  }
}
```

#A A mérési fájl szerkezete

A mérési fájl (`measurements\<kezdés>_<mérés neve>.csv`) elején `# Kulcs=Érték` alakú metaadat-sorok állnak, ezeket a próbák fejléce és sorai követik, a végén a befejezés ideje és állapota szerepel. A számok tizedesponttal, a logikai értékek `True`/`False` alakban szerepelnek.

!table A mérési fájl próbánkénti oszlopai || 5.0,11.0
Oszlop | Jelentés
`Phase` | `Practice` (gyakorló inger) vagy `Test` (mért inger)
`TrialNumber` | a próba sorszáma a fázisán belül
`PlannedTestTrials` | a tervezett mért próbák száma
`EspReactionTimeMs` | a hardveresen mért reakcióidő; üres, ha az ESP nem mért vagy nem válaszolt
`LocalReactionTimeMs` | a PC-n mért idő az inger kirajzolásától a kattintás feldolgozásáig
`Valid`, `InvalidReason` | az érvényesség és az érvénytelenség oka (6.9. szakasz)
`RecordedAt` | a rögzítés időpontja
`ForeperiodMs`, `ActualForeperiodMs` | a sorsolt és a ténylegesen eltelt előperiódus
`PrematureClicks` | az előperiódus alatti kattintások száma
`ScanoutCorrectionUs` | az ESP-nek küldött korrekció mikroszekundumban
`StimulusCenterX`, `StimulusCenterY`, `StimulusDiameterPx` | az inger középpontja és átmérője képpontban
`StimulusEccentricityPx` | az inger középpontjának távolsága a fixációs ponttól
`ValidGazeRatio`, `GazeSamplesInWindow` | a válaszablak tekintetadatainak minősége
`MeanEyeDistanceMm` | a szem átlagos távolsága a kijelző síkjától a válaszablakban
`ClickX`, `ClickY` | a válaszkattintás helye a mérőfelületen

!table A mérési fájl metaadatainak csoportjai || 4.2,11.8
Csoport | Kulcsok
Azonosítás | `FormatVersion`, `ModeName`, `StartedAt`, `AppVersion`
Eszközök | `Tracker`, `GazeOutputFrequencyHz`, `CalibrationConfirmed`, `SerialPort`, `SerialBaudRate`, `EspFirmwareVersion`
Kijelző és képidőzítés | `ScreenDevice`, `ScreenResolutionPx`, `DisplaySizeMm`, `RefreshRateHz`, `VerticalActiveLines`, `VerticalTotalLines`, `DesktopHeightPx`, `ScanoutTimingSource`, `ScanoutTimingExact`, `ScanoutReferenceRow`
A mérőfelület geometriája | `StageSizePx`, `StageScreenOriginPx`, `PhotodiodeMarkerStagePx`, `PhotodiodeGuardPx`, `StageSafeMarginPx`, `FixationCrossPx`, `FixationPointStagePx`
Próbák és időzítés | `MeasuredTrials`, `PracticeTrials`, `ResponseMode`, `ReactionTimeoutMs`, `AnticipationThresholdMs`, `SceneSettleDelayMs`, `ForeperiodDistribution`, `ForeperiodMinMs`, `ForeperiodMaxMs`, `ForeperiodExponentialMeanMs`, `RandomSeed`
Inger | `StimulusColor`, `BackgroundColor`, `StimulusDiameterPx`, `MinEccentricityPx`, `MaxEccentricityPx`, `EffectiveMaxEccentricityPx`, `StimulusPlacementSampling`
Tekintet | `GazeValidation`, `GazeDataRecorded`, `FixationPlacement`, `FixationPlacementSetting`, `GazeValidityRadiusPx`, `MinValidGazeSampleRatio`
Lezárás | `EndedAt`, `Status` (`Completed`, `Aborted`; hiányzó sor: a program a mérés közben leállt)

Egy mérési fájl kezdete és első próbái (a 2026. november 9-i mérések egyikéből, rövidítve):

!include sample.csv

#A A bizonyítandó állítások és a hiányzó adatok jegyzéke

Ez a melléklet összegyűjti a dolgozat szövegében megjelölt helyeket. A sorszámok megegyeznek a szövegbeli jelölések sorszámával; a zárójelben álló szám azt a szakaszt adja meg, ahol a jelölés szerepel.

## Bizonyítandó állítások

Az alábbi állításokat célzott méréssel vagy adatlapi adattal kell alátámasztani; a mérések eredménye a megjelölt helyre illesztendő.

!list_bizonyitas

## Hiányzó adatok

Az alábbi adatok a rendelkezésre álló forrásokból nem voltak megállapíthatók.

!list_hianyzo
