# Mérési beállítások és adatformátum

Ez a leírás a program **2.0.0**-s verziójára vonatkozik: a beállításfájl
formátuma 2-es (`SettingsVersion`), a mérési fájloké 3-as (`FormatVersion`). A
régebbi fájlok kezelését a [régi formátumú beállításfájl](#régi-1-es-formátumú-beállításfájl)
és a [formátumverziók](#formátumverziók) szakasz írja le. A leírásban használt
fogalmak (mérőfelület, előtér, mérési jelenet, tekintet-ellenőrzés, válaszablak)
magyarázata: [FELHASZNALOI_UTMUTATO.md](FELHASZNALOI_UTMUTATO.md#fogalmak).

## Az adatok helye

Minden fájl a felhasználó **Dokumentumok\ReakcioidoMeres** mappájába kerül.
Más mappa a `REACTIONTIME_DATA_DIR` környezeti változóval adható meg.

| Útvonal | Tartalom |
|---|---|
| `beallitasok.json` | a kísérleti paraméterek (lásd lent) |
| `beallitasok.json.bak` | a beállításfájl előző változata; minden programbeli mentés felülírja |
| `measurements\<kezdés>_<mérés neve>.csv` | egy mérés metaadatai és próbái, pl. `20261011_101500_Normal-meres.csv` |
| `measurements\gaze\<kezdés>_<mérés neve>_gaze.csv` | a mérés összes tekintetmintája |
| `logs\log-<dátum>.txt` | napló |

A legelső programváltozat az exe mellé mentett (`measurements` mappa). A
statisztika ezeket a fájlokat is beolvassa.

## Elnevezések: mért és gyakorló ingerek

A felület magyar szavai és a fájlok angol azonosítói így felelnek meg egymásnak:

| Felület (Testreszabás, előtér, napló) | Beállításfájl (JSON) | Metaadat (CSV fejléc) | CSV `Phase` oszlop | Statisztika |
|---|---|---|---|---|
| **mért inger** („Mért ingerek száma (mérésszám)”) | `Trials.MeasuredTrials` | `MeasuredTrials` | `Test` | sorszám előtag nélkül; az összesítés ezekből készül |
| **gyakorló (teszt) inger** („Gyakorló (teszt) ingerek száma”) | `Trials.PracticeTrials` | `PracticeTrials` | `Practice` | „Gy” előtag, „Gyakorló” felirat; nem számít bele az összesítésbe |

Figyelem: a „teszt” szó a felületen a **gyakorló** ingereket jelenti, a
fájlokban viszont a `Phase=Test` a **mért** próba. A régi fájlok `TestTrials`
mezője is a mért próbákra vonatkozott (lásd
[formátumverziók](#formátumverziók)).

Egy mérés mindig pontosan `PracticeTrials + MeasuredTrials` ingerből áll:
először a gyakorlók, utána a mértek. Az érvénytelen próbák **nem pótlódnak**,
csak az érvénytelenség okával együtt rögzülnek.

## A beállításfájl (`beallitasok.json`)

Az első indításkor a program létrehozza az alapértékekkel. A beállításokat a
főmenü **Testreszabás** ablaka szerkeszti és menti (lásd
[FELHASZNALOI_UTMUTATO.md](FELHASZNALOI_UTMUTATO.md)), de a fájl kézzel is
szerkeszthető. A módosítás a következő mérésindításkor érvényesül, újrafordítás
nélkül. A mérési nézet előtere a nézet megnyitásakor és minden mentés után
újratölti a beállításokat, és röviden összefoglalja őket.

Ha a beállítások hibásak, a mérés nem indul el. A hibaüzenet megnevezi a mezőt
(JSON-útvonallal) és a Testreszabás fülét, ahol javítható, pl.:
„Az inger átmérője (Stimulus.DiameterPx) 4 és 500 képpont közé essen.
(Megjelenés fül)”.

Az alapértelmezett fájl (így írja ki a program):

```json
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

Minden méret és távolság **képpontban** (px), a mérést futtató képernyő fizikai
képpontjaiban értendő (lásd [Képpontok és fizikai méret](#képpontok-és-fizikai-méret)).
A tartományok határai is megengedettek; ugyanezekből a határokból
(`Domain/SettingsLimits.cs`) dolgozik a Testreszabás ablak is.

`SettingsVersion`: a fájl formátumverziója, jelenleg `2`. Ha hiányzik, és a
fájlban nincs a régi formátumra jellemző mező, a program 2-esnek tekinti. A
2-nél nagyobb érték hiba: a fájl egy újabb programverzióhoz készült.

### `Trials`: a próbák száma és időzítése

| Mező | Testreszabás (Ingerek és időzítés fül) | Tartomány | Alapérték | Jelentés |
|---|---|---|---|---|
| `MeasuredTrials` | Mért ingerek száma (mérésszám) | 1–1000 | 10 | a mért ingerek pontos száma; az eredmény ezekből készül |
| `PracticeTrials` | Gyakorló (teszt) ingerek száma | 0–100 | 3 | a mért ingerek előtt jelennek meg; mentődnek (`Phase=Practice`), de nem számítanak bele az eredménybe |
| `ResponseMode` | Válasz módja | `ClickAnywhere` („Kattintás az inger megjelenésekor”), `ClickOnStimulus` („Kattintás az ingerre”) | `ClickAnywhere` | mi számít válasznak, lásd [A válasz módja](#a-válasz-módja) |
| `ReactionTimeoutMs` | Maximális várakozás a válaszra | 100–60 000 ms | 3000 | ennyi idő után az inger eltűnik, a próba érvénytelen (`Timeout`), és jön a következő |
| `SceneSettleDelayMs` | Várakozás a mérési képernyő megjelenése után | 0–10 000 ms | 2000 | a mérési jelenet megjelenése és az első inger közötti várakozás: a szem alkalmazkodik az új háttérhez, a szemkövető beáll |
| `ForeperiodDistribution` | Előperiódus: eloszlás | `Exponential` („Exponenciális (kiszámíthatatlan)”), `Uniform` („Egyenletes”) | `Exponential` | az inger előtti várakozás eloszlása |
| `ForeperiodMinMs`, `ForeperiodMaxMs` | legrövidebb / leghosszabb | 0–60 000 ms, és min ≤ max | 1000, 3000 | a várakozás határai |
| `ForeperiodExponentialMeanMs` | exponenciális várható érték | 1–60 000 ms | 700 | exponenciális eloszlásnál a minimum feletti rész várható értéke (a csonkolás előtt); egyenletes eloszlásnál nincs hatása, de akkor is a tartományban kell maradnia |

A csonkolt exponenciális várakozásnál az inger megjelenésének feltételes
valószínűsége a várakozás során közel állandó, így a résztvevő nem tudja
kiszámítani, mikor jön az inger. A mintavétel inverz eloszlásfüggvénnyel
történik, újrasorsolás nélkül.

### A válasz módja

- `ClickAnywhere`: a válasz az inger megjelenése utáni első kattintás bárhol a
  mérőfelületen. Az egérmutató mérés közben nem látszik.
- `ClickOnStimulus`: a résztvevőnek az ingerre kell kattintania. Az egérmutató
  mérés közben látszik; a program nem mozgatja, így minden próba onnan indul,
  ahol az előző véget ért (az előző próba kattintási helye a `ClickX`, `ClickY`
  oszlopban van). A kattintás akkor találat, ha a kattintás helye legfeljebb
  `DiameterPx / 2` távolságra van az inger középpontjától. A mellékattintás is
  válasz: az inger eltűnik, a reakcióidők (helyi és ESP) rögzülnek, de a próba
  érvénytelen (`MissedStimulus`). Ez külön ok: nem időtúllépés és nem
  tekintethiba.

Mindkét módban a kattintás helye a `ClickX`, `ClickY` oszlopba kerül. A
`ClickOnStimulus` a periférikus (`KeepFixation`) móddal együtt is beállítható,
de a Testreszabás figyelmeztet, mert a célzáshoz általában oda kell nézni.

### Elsietett válasz

A próba érvénytelen (`Anticipation`, a felületen „elsietett válasz”), ha a
válasz nem lehetett reakció az ingerre:

- a résztvevő már az **előperiódus alatt**, az inger megjelenése előtt
  kattintott. Ez a kattintás nem válasz (az inger ettől még megjelenik, és a
  program megvárja a választ), de a próbát érvényteleníti; az ilyen kattintások
  számát a `PrematureClicks` oszlop rögzíti;
- a reakcióidő rövidebb **100 ms**-nál (`AnticipationThresholdMs` metaadat).
  A program az ESP által mért időt nézi, ennek hiányában a helyit.

A határ rögzített, a beállításfájlban nem szerepel.

### `Stimulus`: az inger megjelenése és helye

| Mező | Testreszabás (Megjelenés fül) | Tartomány | Alapérték | Jelentés |
|---|---|---|---|---|
| `Color` | Inger színe | `#RRGGBB` | `#FFFFFF` | az inger (kitöltött kör) színe |
| `BackgroundColor` | Háttérszín | `#RRGGBB` | `#1E1E1E` | a mérési jelenet háttere (az előtér háttere rögzített, nem ez) |
| `DiameterPx` | Inger mérete (átmérő) | 4–500 px | 60 | az inger átmérője |
| `MinEccentricityPx`, `MaxEccentricityPx` | Az inger távolsága a fixációs ponttól: legalább / legfeljebb | 0–10 000 px | 150, 500 | az inger **középpontjának** távolsága a fixációs ponttól; próbánként sorsolva, lásd [Az inger elhelyezése](#az-inger-elhelyezése) |

A színek `#RRGGBB` alakúak (kis- és nagybetűvel is), csak átlátszatlanok
lehetnek. Mentéskor a program nagybetűs alakra hozza őket (`#1e1e1e` →
`#1E1E1E`).

### `Gaze`: tekintet-ellenőrzés és a fixációs kereszt helye

| Mező | Testreszabás (Szemmozgás fül) | Tartomány | Alapérték | Jelentés |
|---|---|---|---|---|
| `Validation` | „Szemmozgás-validáció szükséges” és „Mit kell néznie a résztvevőnek?” | `None`, `LookAtStimulus`, `KeepFixation` | `LookAtStimulus` | a tekintet-ellenőrzés módja, lásd [A tekintet-ellenőrzés](#a-tekintet-ellenőrzés) |
| `FixationPlacement` | A fixációs pont helye: „Középen” / „Egyéni hely” | `Center`, `Custom` | `Center` | csak `KeepFixation` módban érvényesül; minden más módban a kereszt középen van |
| `FixationPointX`, `FixationPointY` | „Kijelölés a mérőfelületen…” | 0–1, vagy mindkettő `null` | `null` | a kijelölt pont a képernyő szélességének, illetve magasságának arányában (pl. `0.25` = a szélesség negyede) |
| `ValidityRadiusPx` | Elfogadási rádiusz | 1–2000 px | 80 | `LookAtStimulus`: az inger szélén túl elfogadott tekintethiba; `KeepFixation`: a fixációs pont körüli megengedett eltérés |
| `MinValidGazeSampleRatio` | Érvényes tekintetminták minimális aránya | 0,01–1 (az ablakban 1–100 %) | 0,5 | ha a válaszablak tekintetmintáinak ennél kisebb hányada érvényes, a próba érvénytelen (`InsufficientGazeData`) |

A kijelölt pont arányszámként tárolódik, így más felbontású képernyőn arányosan
ugyanoda kerül. A pont akkor is megmarad, ha éppen nem érvényesül (pl. a mód
visszaváltásakor nem kell újra kijelölni). A mérésben a kereszt helye egész
képpontra kerekítve: `(round(W · X), round(H · Y))`, illetve középen
`(round(W / 2), round(H / 2))`.

### `Hardware`: az ESP és a fotodióda-jelző helye

| Mező | Testreszabás (Hardver fül) | Tartomány | Alapérték | Jelentés |
|---|---|---|---|---|
| `EspPortName` | ESP soros port | `"COM…"` vagy `null` | `null` („Automatikus felismerés”) | az ESP portja; üresen a program az USB-azonosítók alapján keresi meg |
| `EspBaudRate` | Átviteli sebesség (baud) | 1200–4 000 000 | 115200 | egyezzen az ESP firmware-ével |
| `PhotodiodeMarkerX`, `PhotodiodeMarkerY` | A fotodióda-jelző helye: **Áthelyezés a mérőfelületen…** / **Alaphelyzet** | 0–1 vagy `null` | `null` (jobb alsó sarok) | a jelzőnégyzet középpontja a képernyő szélességéhez, illetve magasságához viszonyítva; a kettő együtt `null` vagy együtt kitöltött |

A fotodióda-jelző helye egész képpontra kerekítve:
`x = round(W · X) − 28`, `y = round(H · Y) − 28` (a négyzet bal felső sarka),
úgy igazítva, hogy a négyzet ne lógjon ki a képernyőről (0 ≤ x ≤ W − 56,
0 ≤ y ≤ H − 56); a képernyő legszéle is megengedett, ilyenkor a fekete védősáv
szél felőli része kilóg, és nem rajzolódik ki.
`null` esetén a jobb alsó sarok: `(W − 76, H − 76)`. Az Alapértékek gomb a
többi hardverbeállítással együtt ezt sem állítja vissza.

### Összefüggő szabályok

Az egyes tartományokon túl a program ezeket is ellenőrzi
(`ExperimentSettings.Validate`):

1. Az inger színe nem lehet azonos a háttérszínnel, mert az inger nem
   látszana.
2. `ForeperiodMinMs` ≤ `ForeperiodMaxMs`, és `MinEccentricityPx` ≤
   `MaxEccentricityPx`.
3. Az inger sosem takarhatja a fixációs keresztet:
   `MinEccentricityPx` ≥ `DiameterPx / 2 + 14` (a 14 képpont a kereszt
   körülíró körének sugara). Az alapértékekkel: 150 ≥ 30 + 14.
4. Tekintet-ellenőrzésnél (mindkét módban) az inger legközelebbi helye is
   kívül esik az elfogadási tartományon:
   `MinEccentricityPx` > `DiameterPx / 2 + ValidityRadiusPx`. Különben normál
   módban a kereszt nézése is az inger nézésének számíthatna, periférikus
   módban pedig az ingerre nézés is a fixációs ablakon belül maradhatna. Az
   alapértékekkel: 150 > 30 + 80.
5. `FixationPointX` és `FixationPointY` együtt `null`, vagy együtt kitöltött,
   véges 0 és 1 közötti szám. Ha a résztvevőnek a fixációs pontot kell néznie,
   és az egyéni hely van kiválasztva, pontot is ki kell jelölni („Jelölj ki
   fixációs pontot a mérőfelületen.”).
6. Az `EspPortName` egy COM-port neve, vagy üres.

Ezeken felül a program **figyelmeztet** (`ExperimentSettings.GetWarnings`), de
a mérést nem tiltja, ha:

- az inger és a háttér kontrasztaránya 3:1 alatt van (az inger nehezen
  észrevehető lehet);
- az ingerre kattintós válasz (`ClickOnStimulus`) a periférikus
  (`KeepFixation`) móddal együtt van beállítva;
- tekintet-ellenőrzés mellett nincs gyakorló inger (`PracticeTrials = 0`).

A képernyőtől függő feltételeket a mérés indításakor és a Testreszabás ablakban
(a főablak képernyőjének méretével) a `StageFeasibility` ellenőrzi:

- a fixációs kereszt (a 14 képpont sugarú körülíró köre) legalább 36
  képpontra marad a képernyő szélétől és a fotodióda-jelzőtől; különben hiba
  („A fixációs pont túl közel esik a képernyő széléhez. …”, „A fixációs pont
  túl közel esik a fotodióda-jelzőhöz. …”, illetve középen lévő keresztnél „A
  képernyő közepén lévő fixációs kereszt túl közel esik a fotodióda-jelzőhöz.
  …”);
- a képernyő belsejébe tett fotodióda-jelző nem vághatja ketté az inger
  lehetséges helyeit (nagy ingernél a jelző tiltott környéke átérhet a
  képernyőn); különben hiba („A fotodióda-jelző ezzel az ingermérettel
  kettévágja a képernyőn az inger lehetséges helyeit. …”);
- a legkisebb távolságban az inger legalább egy irányban elfér; különben hiba
  („A legkisebb távolság (… px) ezen a … × … képpontos képernyőn egyik irányban
  sem fér el. …”);
- ha a legnagyobb távolság nem fér el, a mérés a még elférő legnagyobb
  távolsággal fut, és erről figyelmeztet („A legnagyobb távolságból csak … px
  fér el ezen a képernyőn.”); az értéket az `EffectiveMaxEccentricityPx`
  metaadat rögzíti.

### Szigorú beolvasás

A fájl beolvasása szigorú, mert egy csendben alapértékre eső beállítással a
mérés nem azzal futna, amit a kutató beállított. **Hiba**, és a mérés nem indul
el, ha a fájlban:

- ismeretlen (pl. elgépelt) mező van, pl. `"MeasuredTrial"`;
- rossz típusú érték áll: szöveg szám helyett, tört szám egész helyett, túl
  nagy szám, vagy számmal megadott felsorolási érték (`"Validation": 1`
  helyett `"Validation": "LookAtStimulus"` kell);
- egy mező kétszer szerepel (a kis- és nagybetű nem számít);
- egy szakasz vagy egy szín értéke `null`;
- a JSON szintaktikailag hibás, vagy a tartalom nem `{ … }` objektum.

Ilyenkor az üzenet a fájl sorát és a mező útvonalát is megadja, pl.
„A beállításfájl nem olvasható (4. sor, $.Trials.MeasuredTrial): hibás
JSON-szintaxis, ismeretlen mező vagy rossz típusú érték. Részletek: …”.

**Megengedett:** megjegyzés (`//` és `/* */`), záró vessző, tetszőleges kis- és
nagybetű a mezőnevekben, hiányzó mező vagy szakasz (ezek az alapértéket
kapják), UTF-8 kódolás (BOM-mal vagy anélkül) és BOM-mal jelölt UTF-16.

Ha a fájl beolvasható, de az értékei hibásak, a Testreszabás ablak a beolvasott
értékeket mutatja, így ott javíthatók. Ha a fájl nem olvasható (vagy újabb
programverzióhoz készült), az ablak alapértékekkel nyílik meg, felül egy
figyelmeztető sávval; a mentés ilyenkor felülírja a fájlt, az előző változat
`.bak`-ként megmarad.

### Mentés és biztonsági másolatok

- A program csak érvényes beállítást ment. Mentéskor a színek nagybetűs
  `#RRGGBB` alakra állnak, a portnév a körülötte lévő szóközök nélkül (üresen
  `null`-ként) kerül a fájlba, a `SettingsVersion` pedig `2` lesz.
- Az írás atomikus: a tartalom előbb a `beallitasok.json.tmp` fájlba kerül
  (lemezre ürítve), és csak utána lép a régi fájl helyére. Egy írás közbeni
  hiba (áramszünet, tele lemez) így sosem hagy félig írt beállításfájlt. Ha a
  fájlt egy másik program (OneDrive, víruskereső) épp zárolja, a csere
  100 ms-onként legfeljebb ötször próbálkozik.
- A felülírt változat **`beallitasok.json.bak`** néven megmarad. Ez gördülő
  másolat: minden mentés a legutóbbi előző változattal írja felül.
- Írásvédett vagy tartósan zárolt fájlnál a mentés hibaüzenettel áll meg, a
  Testreszabás ablak nyitva marad a módosításokkal.
- **A megjegyzések mentéskor elvesznek:** a program a fájlt megjegyzések nélkül
  írja ki. A kézzel, megjegyzésekkel szerkesztett változat csak a `.bak`
  fájlban marad meg, a következő mentésig.
- A program az első indításkor (alapértékek) is ír a fájlba, ugyanígy.

### Régi (1-es) formátumú beállításfájl

Az 1.x verziók látószög alapú, `NormalMode` / `PeripheralMode` szakaszos
beállításfájlját a program már nem alakítja át. Ilyen fájlnál (a
`SettingsVersion` hiányzik vagy legfeljebb 1, és szerepel benne a `NormalMode`,
a `PeripheralMode`, a `DefaultViewingDistanceCm` vagy a gyökérszintű
`MinValidGazeSampleRatio` mező) a betöltés hibát jelez, a mérés nem indul el, és
a Testreszabás ablak az alapértékekkel nyílik meg. A mérést ott újra be kell
állítani; mentéskor a régi fájl `beallitasok.json.bak` néven megmarad.

## A tekintet-ellenőrzés

| Testreszabás (Szemmozgás fül) | `Gaze.Validation` | A mérés neve (`ModeName`) | A próba érvényes, ha … | Szemkövető és kalibráció |
|---|---|---|---|---|
| „Szemmozgás-validáció szükséges” kikapcsolva | `None` | Mérés tekintet-ellenőrzés nélkül | az időkorláton belül válasz érkezett, és az nem elsietett (ingerre kattintós módban: az ingerre esett) | nem kell; ha mégis elérhető, a tekintetadat rögzül, de az érvényességbe nem számít bele |
| Az ingert (normál reakcióidő) | `LookAtStimulus` | Normál mérés | a válaszablak **legalább egy** érvényes mintájára: távolság(tekintet, ingerközéppont) ≤ `DiameterPx / 2 + ValidityRadiusPx` | kötelező |
| A fixációs pontot (periférikus reakcióidő) | `KeepFixation` | Periférikus mérés | a válaszablak **minden** érvényes mintájára: távolság(tekintet, fixációs pont) ≤ `ValidityRadiusPx` | kötelező |

Az elfogadási rádiusz jelentése módonként más:

- **Normál mód:** a rádiusz az inger **szélén túl** elfogadott tekintethiba. Az
  elfogadott tartomány az inger középpontja körüli, `DiameterPx / 2 +
  ValidityRadiusPx` sugarú kör, így a rádiusz nem függ az inger méretétől.
- **Periférikus mód:** a rádiusz a fixációs pont körüli kör sugara. A próba
  akkor is érvénytelen, ha a résztvevő az ingerre nézett, de a válasz
  pillanatára már visszanézett.

A **válaszablak** az inger kirajzolásától a válaszig, vagyis a számítógép által
érzékelt első kattintásig (időtúllépésnél az időkorlát lejártáig) tart; a
tekintet-ellenőrzés csak az ebbe eső mintákból dönt. Az ablak akkor kezdődik,
amikor az inger kirajzolásának hívása visszatér; ez 1–2 képkockával megelőzi
az inger tényleges megjelenését a kijelzőn. A tekintet a próba teljes ideje
alatt (előperiódus, inger, válasz) rögzül. Az érvénytelenség okát a program
ebben a sorrendben dönti el (`Measurement/TrialEvaluator.cs`):

1. `Timeout`: nem érkezett kattintás a `ReactionTimeoutMs` időn belül;
2. `Anticipation`: elsietett válasz, vagyis kattintás már az előperiódus alatt,
   vagy 100 ms-nál rövidebb reakcióidő (lásd [Elsietett válasz](#elsietett-válasz));
3. `MissedStimulus`: ingerre kattintós módban (`ClickOnStimulus`) a kattintás
   nem az ingerre esett; ez a tekintet-ellenőrzéstől függetlenül dől el;
4. tekintet-ellenőrzés nélkül (`None`) minden más próba érvényes;
5. `TrackerUnavailable`: a próba teljes ideje alatt egyetlen tekintetminta sem
   érkezett (a szemkövető kiesett). A mérés ettől nem áll le; a mérés végén az
   előtér állapotsora jelzi, hány próbában nem érkezett tekintetadat;
6. `InsufficientGazeData`: a válaszablakba nem esett minta, vagy az érvényes
   minták aránya a `MinValidGazeSampleRatio` alatt van (pislogás, elveszett
   szem);
7. `GazeViolation`: a fenti szabály nem teljesült.

Szemkövető nélkül (csak `None` módban lehetséges) a tekintetadatok oszlopai
(`ValidGazeRatio`, `GazeSamplesInWindow`, `MeanEyeDistanceMm`) üresek, a
tekintetminták fájljában csak a fejléc szerepel, és a `GazeDataRecorded`
metaadat `False`.

## Képpontok és fizikai méret

A program minden méretet és távolságot a mérést futtató képernyő **fizikai
képpontjaiban** kezel (az alkalmazás PerMonitorV2 módban fut, a Windows
nagyítása nem számít bele). Fizikai mértékegységgel (milliméterrel, fokkal) a
program nem számol, ezért a kijelző méretét sem kell megadni.

Az inger fizikai mérete ezért a monitortól függ. Például a 60 képpontos inger
egy 24 colos Full HD monitoron (≈ 0,277 mm/képpont) ≈ 16,6 mm, egy 27 colos 4K
monitoron (≈ 0,156 mm/képpont) ≈ 9,3 mm. Összehasonlítható mérésekhez ugyanaz a
monitor, felbontás és ülési távolság kell.

Ha a mérés szemkövetővel futott, a látószög utólag kiszámítható:

- a képpontméret a `DisplaySizeMm` (a Tobii display area mérete) és a
  `ScreenResolutionPx` metaadatból: `mmPerPx = szélesség mm / szélesség px`;
- a szem távolsága próbánként a `MeanEyeDistanceMm` oszlopból (a szem
  merőleges távolsága a kijelző síkjától, a válaszablak átlaga), mintánként a
  tekintetfájl `EyeDistanceMm` oszlopából;
- az inger átmérője fokban: `2 · atan(StimulusDiameterPx · mmPerPx / (2 · d))`,
  a távolsága a fixációs ponttól: `atan(StimulusEccentricityPx · mmPerPx / d)`.

Az utóbbi azt feltételezi, hogy a szem a fixációs kereszttel szemben van; a
képernyő szélén lévő ingereknél ez közelítés. Példa: a fenti 24 colos
monitoron, 60 cm-ről a 60 képpontos inger ≈ 1,6°, a 150 és az 500 képpontos
távolság ≈ 4,0°, illetve ≈ 13,0°. Szemkövető nélkül a metaadatokban nincs
kijelzőméret, az adatok csak képpontban értelmezhetők.

## Az inger elhelyezése

- Az inger középpontja a fixációs ponttól `r` távolságra, `θ` irányban van. Egy
  hely akkor megengedett, ha az inger befoglaló négyzete a képernyő szélétől és
  a fotodióda-jelzőtől is legalább 36 képpontra (`StageSafeMarginPx`) marad.
- **A távolság egyenletes eloszlású** a `[MinEccentricityPx,
  EffectiveMaxEccentricityPx]` tartományban: a program próbánként egyszer
  sorsolja. **Az irány egyenletes** az adott távolságban a képernyőn elférő
  irányok között: ha a sorsolt irányban az inger nem fér el, csak az irányt
  sorsolja újra, végül 0,1 fokos lépésekkel megkeresi az összes elférő irányt,
  és azok közül választ. (Ha a távolságot és az irányt együtt sorsolná újra, a
  nagy távolságok, ahol kevesebb irány fér el, alulreprezentáltak lennének.) A
  metaadat: `StimulusPlacementSampling=UniformDistance`.
- Az `EffectiveMaxEccentricityPx` a `MaxEccentricityPx`-ig terjedő legnagyobb,
  egész képpontos távolság, amelyben az inger legalább egy irányban elfér.
- Minden mérés saját véletlenmagot kap (`RandomSeed`). Ugyanazzal a
  programverzióval, beállításokkal és képernyőmérettel a mag az ingerhelyek és
  az előperiódusok sorozatát is meghatározza.
- A ténylegesen használt hely próbánként a `StimulusCenterX`, `StimulusCenterY`
  és a `StimulusEccentricityPx` oszlopba kerül.

## A mérési fájl (`measurements\*.csv`)

A fájl elején `# Kulcs=Érték` metaadat-sorok állnak, ezt a próbák fejléce és
sorai követik, a végén pedig a `# EndedAt=` és a `# Status=` sor. Az értékek
invariáns formátumúak (tizedespont, `True`/`False`). A metaadat-érték az `=`
jeltől a sor végéig tart, idézőjelezés nélkül (vessző is lehet benne); egy
esetleges sortörés szóközzé válik.

A mentés **próbánként** történik: a program minden próba sorát azonnal a fájl
végére írja, és a lemezre üríti. Megszakítás után az eddigi próbák
megmaradnak. Ha a program a mérés közben leáll, a hiányzó `# Status=` sorból
látszik, hogy a mérés nem zárult le.

A mérési és a tekintetfájl a mérés teljes ideje alatt nyitva marad. Közben más
program (pl. az Excel) megnyithatja olvasásra, de írásra nem: a mérés ettől nem
áll le. Egy rövid, átmeneti zárolásnál (OneDrive, víruskereső) az írás
100 ms-onként legfeljebb ötször próbálkozik. Új mérés egyik fájlja sem írhat
felül meglévő fájlt: ha a név foglalt, `_2`, `_3`, … utótag kerül a két fájl
nevébe.

A statisztika a hibás vagy csonka próbasorokat (pl. áramszünet vagy kézi
szerkesztés után; csonkának számít az is, amelyben kevesebb mező van, mint a
fejlécben) kihagyja, és a naplóba a fájl nevével és a sor számával
figyelmeztetést ír; a fájl többi próbája beolvasódik. Csak az a fájl marad ki
teljesen, amelynek a fejléce (a `ModeName` és a `StartedAt` metaadat, az
oszlopfejléc a `TrialNumber` és a `RecordedAt` oszloppal) nem értelmezhető.

### Metaadatok

A kulcsok ebben a sorrendben szerepelnek. A *-gal jelöltek csak akkor, ha van
értékük.

| Kulcs | Jelentés, példa |
|---|---|
| `FormatVersion` | a fájl formátumverziója: `3` |
| `ModeName` | a mérés neve: `Normál mérés`, `Periférikus mérés` vagy `Mérés tekintet-ellenőrzés nélkül`; ingerre kattintós módban ` – ingerre kattintás` utótaggal |
| `StartedAt` | a mérés kezdete (ISO 8601) |
| `AppVersion` | programverzió, pl. `2.0.0+04968c7…` (a `+` után a forrás git-commitja, ha a fordítás git-tárolóból történt) |
| `Tracker` | a szemkövető típusa és sorozatszáma; `nincs`, ha nem volt elérhető |
| `GazeOutputFrequencyHz` | a szemkövető mintavételi frekvenciája; üres, ha nincs szemkövető |
| `CalibrationConfirmed` | `True`, ha ebben a programfutásban volt megerősített kalibráció |
| `SerialPort`, `SerialBaudRate` | az ESP ténylegesen használt portja és sebessége |
| `EspFirmwareVersion` | az ESP firmware-ének verziója (`VERSION` kérésre adott válasz); üres, ha a firmware nem válaszolt rá (régebbi firmware) |
| `ScreenDevice` | a mérést futtató kijelző Windows-neve, pl. `\\.\DISPLAY1` |
| `ScreenResolutionPx` | a kijelző felbontása, pl. `1920x1080` |
| `DisplaySizeMm`* | a kijelző fizikai mérete, pl. `531.4x298.9`; csak szemkövetővel |
| `DisplaySizeSource`* | a méret forrása: `Tobii display area` |
| `DisplayAreaWarning`* | tekintet-ellenőrzés nélküli mérésnél: a display area hiányzik, vagy nem illik a kijelzőhöz (a tekintetadat képernyőhelye ilyenkor nem megbízható) |
| `RefreshRateHz` | pontos (tört) képfrissítési frekvencia |
| `VerticalActiveLines`, `VerticalTotalLines`, `DesktopHeightPx` | a scanout-korrekció bemenetei, lásd [SCANOUT_PROTOCOL.md](SCANOUT_PROTOCOL.md) |
| `ScanoutTimingSource`, `ScanoutTimingExact` | `QueryDisplayConfig` / `True`, vagy a közelítő `EnumDisplaySettings` / `False` |
| `ScanoutTimingWarning`* | pl. elforgatott vagy váltottsoros kijelző |
| `ScanoutReferenceRow` | a korrekció melyik sorra vonatkozik: `StimulusCenter` (az inger középsora) |
| `StageSizePx` | a mérőfelület (teljes képernyő) mérete, pl. `1920x1080` |
| `StageScreenOriginPx` | a mérőfelület bal felső sarka a képernyőn, pl. `0,0` |
| `PhotodiodeMarkerStagePx` | a fotodióda-jelző négyzete: `x,y,szélesség,magasság`, pl. `1844,1004,56,56` |
| `PhotodiodeGuardPx` | a jelző körüli fekete védősáv szélessége: `16` |
| `StageSafeMarginPx` | az inger és a kereszt legkisebb távolsága a széltől és a jelzőtől: `36` |
| `FixationCrossPx` | a kereszt mérete: `szárhossz,vastagság,körvonal`, pl. `20,3,2` |
| `MeasuredTrials`, `PracticeTrials` | a mért és a gyakorló ingerek száma |
| `ResponseMode` | a válasz módja: `ClickAnywhere` vagy `ClickOnStimulus` |
| `ReactionTimeoutMs` | a válaszra várás leghosszabb ideje |
| `AnticipationThresholdMs` | az elsietett válasz határa: az ennél rövidebb reakcióidő érvénytelen (`100`) |
| `SceneSettleDelayMs` | a jelenet utáni várakozás |
| `ForeperiodDistribution`, `ForeperiodMinMs`, `ForeperiodMaxMs`, `ForeperiodExponentialMeanMs` | az előperiódus beállításai |
| `RandomSeed` | a mérés véletlenmagja |
| `StimulusColor`, `BackgroundColor` | az inger és a háttér színe, pl. `#FFFFFF`, `#1E1E1E` |
| `StimulusDiameterPx` | az inger átmérője |
| `MinEccentricityPx`, `MaxEccentricityPx` | a beállított távolságtartomány |
| `EffectiveMaxEccentricityPx` | a ténylegesen elférő legnagyobb távolság |
| `StimulusPlacementSampling` | a helysorsolás módja: `UniformDistance` |
| `GazeValidation` | `None`, `LookAtStimulus` vagy `KeepFixation` |
| `GazeDataRecorded` | `True`, ha a mérésben rögzült tekintetadat (volt szemkövető) |
| `FixationPlacement` | a kereszt **érvényesülő** helye: `Center` vagy `Custom` |
| `FixationPlacementSetting` | a beállított érték (pl. `Custom` normál módban is, ahol nem érvényesül) |
| `FixationPointStagePx` | a kereszt középpontja a mérőfelületen, pl. `960,540` |
| `GazeValidityRadiusPx`, `MinValidGazeSampleRatio` | a tekintet-ellenőrzés beállításai |

A fájl végén:

| `Status` | Jelentés |
|---|---|
| `Completed` | minden tervezett próba lefutott |
| `Aborted` | megszakítás: Esc, az alkalmazás bezárása, mentési vagy váratlan hiba (az ESP mérés közbeni kiesése nem szakítja meg a mérést) |
| `AttemptLimitReached` | csak 2-es formátumú fájlokban: az érvénytelen próbák pótlása közben elfogyott a `MaxTestAttempts` |
| (nincs sor) | a program a mérés közben leállt |

### Próbaoszlopok

| Oszlop | Jelentés |
|---|---|
| `Phase` | `Practice` (gyakorló inger) vagy `Test` (mért inger) |
| `TrialNumber` | a próba sorszáma a fázisán belül, 1-től |
| `PlannedTestTrials` | a tervezett mért próbák száma (`MeasuredTrials`); minden sorban ugyanaz |
| `EspReactionTimeMs` | a hardveresen (ESP + fotodióda) mért reakcióidő, amit a program a kattintás után `RTIME` üzenettel kér le; üres időtúllépésnél, vagy ha az ESP nem válaszolt (érvényes próbában is) |
| `LocalReactionTimeMs` | a PC-n mért idő az inger kirajzolásától az operációs rendszer által érzékelt kattintásig (egérgomb lenyomása); időtúllépésnél üres |
| `Valid`, `InvalidReason` | `True`/`False`; `None`, `Timeout`, `Anticipation`, `MissedStimulus`, `TrackerUnavailable`, `InsufficientGazeData` vagy `GazeViolation` |
| `RecordedAt` | időbélyeg (ISO 8601) |
| `ForeperiodMs` | az inger előtti várakozás sorsolt (tervezett) hossza |
| `ActualForeperiodMs` | a ténylegesen eltelt idő a várakozás kezdetétől az inger kirajzolásáig, a PC óráján mérve |
| `PrematureClicks` | az előperiódus alatti (az inger megjelenése előtti) kattintások száma; ha nem 0, a próba `Anticipation` |
| `ScanoutCorrectionUs` | az ESP-nek küldött korrekció, µs |
| `StimulusCenterX`, `StimulusCenterY`, `StimulusDiameterPx` | az inger középpontja és átmérője a mérőfelületen, képpontban |
| `StimulusEccentricityPx` | az inger középpontjának távolsága a fixációs ponttól, képpontban |
| `ValidGazeRatio`, `GazeSamplesInWindow` | a válaszablak tekintetadatainak minősége; szemkövető nélkül üres |
| `MeanEyeDistanceMm` | a szem átlagos merőleges távolsága a kijelzőtől a válaszablakban; szemkövető nélkül üres |
| `ClickX`, `ClickY` | a válaszkattintás helye a mérőfelületen, képpontban (mindkét válaszmódban); időtúllépésnél üres |

Az `ActualForeperiodMs`, a `PrematureClicks`, a `ClickX` és a `ClickY` oszlop,
valamint az `AnticipationThresholdMs` metaadat és az `Anticipation` ok a 3-as
formátumon belül, a 2.0.0 fejlesztése közben került a fájlba. Egy korábbi
fejlesztői változattal készült 3-as fájlból hiányozhatnak; a statisztika az
oszlopokat név szerint keresi, így ezeket a fájlokat is beolvassa.

### A tekintetminták fájlja (`gaze\*_gaze.csv`)

Mintánként egy sor; a fejlécben a `ModeName`, a `StartedAt`, a hozzá tartozó
mérési fájl neve (`SessionFile`) és az időalap (`TimeBase`) szerepel. A `Phase`
+ `TrialNumber` oszloppár köti a mintát a próbához. Oszlopok:
`SystemTimeStampUs` (Tobii időbélyeg, µs), `TimeFromOnsetMs` (az inger
kirajzolásához mért idő, az előperiódusban negatív), `InResponseWindow` (beleszámított-e
a tekintet-ellenőrzésbe), `LeftEyeValid`, `RightEyeValid`, `NormalizedX`,
`NormalizedY` (Tobii-koordináta), `StageX`, `StageY` (a mérőfelületen,
képpontban; érvénytelen mintánál üres) és `EyeDistanceMm`.

### Formátumverziók

A statisztika minden változatot beolvassa; az oszlopokat név szerint keresi, így
a régi neveket is felismeri. A tervezett, illetve célzott próbaszám oszlopa és a
próbaszám-metaadat jelentése azonban verziónként más:

| `FormatVersion` | Program | Próbaszám-oszlop | Próbaszám-metaadat | Távolság-oszlop | `Completed` jelentése |
|---|---|---|---|---|---|
| 1 (nincs ilyen sor) | a legelső változat | `TotalTrials`: a mérés összes próbája (rögzített szám, gyakorlás nélkül) | – | – | csak befejezett mérés mentődött; a lezáró sor hiánya is befejezettet jelent |
| 2 | 1.x (1.1.0) | `TargetValidTrials`: a kívánt **érvényes** tesztpróbák száma; az érvénytelenek helyett újabb próba jött (`RepeatInvalidTrials`), legfeljebb `MaxTestAttempts`-ig | `TestTrials`: ugyanez a cél; mellette `RepeatInvalidTrials`, `MaxTestAttempts` és a látószög alapú kulcsok (`StimulusDiameterDeg`, `MinEccentricityDeg`, `MaxEccentricityDeg`, `EffectiveMaxEccentricityDeg`, `GazeValidityRadiusDeg`, `PixelsPerMm`, `ViewingDistanceMm`, `ViewingDistanceSource`) | `StimulusEccentricityDeg`: fokban | összegyűlt a kívánt számú érvényes próba |
| 3 | 2.0.0 | `PlannedTestTrials`: a pontosan lefutó mért próbák száma, pótlás nélkül | `MeasuredTrials`: ugyanez; nincs `TestTrials`, `RepeatInvalidTrials`, `MaxTestAttempts` | `StimulusEccentricityPx`: képpontban | minden tervezett próba lefutott |

Ebből következik, hogy egy 2-es fájlban a `TestTrials=10` 10 érvényes próbát
jelentett célként (az érvénytelenekkel együtt legfeljebb `MaxTestAttempts`
próba futott le), egy 3-as fájlban a `MeasuredTrials=10` pontosan 10 lefutó
mért próbát. A 2-es fájlok fokban megadott távolsága a
kijelző és a nézési távolság nélkül nem váltható át képpontra, ezért a program
nem is próbálja.
