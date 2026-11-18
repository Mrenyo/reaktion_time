# ESP soros protokoll

Ez a leírás a PC-n futó program (a továbbiakban: .NET) és az ESP32-S3 (a
továbbiakban: ESP) közti soros protokollt és a scanout-korrekció számítását
írja le. Az ESP oldalát a [`firmware/main/main.c`](../firmware/main/main.c)
valósítja meg, a .NET oldalát az `Infrastructure/Esp` mappa.

A kommunikáció szöveges, soronkénti: minden üzenetet egy sortörés (`\n`) zár
le, a számok invariáns kultúrájúak (tizedespont). Az ESP kérés nélkül semmit
nem küld. A .NET négy üzenetet küld:

| Üzenet | Mikor | Az ESP válasza |
|---|---|---|
| `CHECK` | a mérés indításakor, a port megnyitása után | `OK` |
| `VERSION` | a mérés indításakor, a sikeres `CHECK` után | a firmware verziója (pl. `1.1.0`) |
| előjeles szám (pl. `-9259.259`) | minden próba előtt: a scanout-korrekció µs-ban | nincs |
| `RTIME` | a próbában, miután a PC érzékelte a résztvevő kattintását | a korrigált reakcióidő µs-ban (pl. `250000.000`), vagy `NA`, ha az ESP a próbában nem mért |

Egy próba menete:

```text
PC  → ESP   -9259.259       (scanout-korrekció; új próba kezdődik)
            ... előperiódus, majd megjelenik az inger és a jelző ...
            ... a résztvevő kattint: az ESP a gombot, a PC az egérkattintást érzékeli ...
PC  → ESP   RTIME
ESP → PC    250000.000      (vagy NA)
```

## A soros port beállításai

| | |
|---|---|
| Átviteli sebesség | alapértelmezetten 115200 baud (`Hardware.EspBaudRate`); a firmware-ben UART0, 115200 |
| Keret | 8 adatbit, paritás nélkül, 1 stopbit (8N1) |
| Áramlásvezérlés | nincs; a .NET a DTR és az RTS vonalat nem állítja be |
| Kódolás | UTF-8 (az üzenetek csak ASCII karakterekből állnak) |
| Sorvég | a .NET `\n`-t küld; fogadáskor a sor végi `\r` és a szóközök nem számítanak (mindkét oldalon) |

A .NET a portot háttérszálon nyitja meg. Ha a port foglalt vagy még nem
elérhető, legfeljebb nyolcszor próbálkozik, 500 ms-onként. A megnyitás után
1,5 másodpercet vár (az ESP a port megnyitásakor újraindulhat), kiüríti a
puffereket, és csak ezután indítja az olvasást. Ha a port menet közben
használhatatlanná válik (pl. kihúzzák az ESP-t), az olvasás leáll, és a
következő küldés hibát ad; ennek kezelését lásd a
[Reakcióidő](#reakcióidő-rtime--szám-vagy-na) szakasz végén.

## Kapcsolatellenőrzés: `CHECK` → `OK`

A „Mérés indítása” gomb után a program az ESP portjának megnyitása után
`CHECK` üzenetet küld, amire az ESP-nek pontosan `OK` sorral kell válaszolnia
(a sor elején és végén lévő szóközök és `\r` nem számítanak). A mérés csak
`OK` válasz után indul. Mivel az ESP a port megnyitásakor újraindulhat, a
program a `CHECK`-et legfeljebb háromszor küldi el, egyenként 1 másodperces
várakozással; ha egyikre sem jön `OK`, a mérés nem indul el, és hibaüzenet
jelenik meg. Az `OK`-tól eltérő sorokat (pl. az ESP indulási üzeneteit) a
program figyelmen kívül hagyja. A `CHECK` az ESP mérési állapotát nem
változtatja meg.

## Firmware-verzió: `VERSION` → verziószám

A sikeres `CHECK` után a program egyszer `VERSION` üzenetet küld, amire az ESP
egyetlen sorban a firmware verziójával válaszol (a `main.c` `FW_VERSION`
állandója):

```text
1.1.0
```

A verzió `MAJOR.MINOR.PATCH` alakú, opcionális utótaggal (pl. `1.2.0-dev`). A
két pont miatt egy verziószám sosem érvényes szám, és nem is `OK` vagy `NA`,
így egy késve érkező válasz nem keveredhet össze egy `CHECK` vagy `RTIME`
válasszal (és fordítva). A program legfeljebb 500 ms-ig vár; ha ezalatt nem jön
ilyen alakú sor (pl. régebbi firmware, amely a `VERSION`-t érvénytelen sorként
figyelmen kívül hagyja), a verzió ismeretlen marad, de a mérés ettől még
elindul. A `VERSION` az ESP mérési állapotát nem változtatja meg.

## .NET → ESP: scanout-korrekció

A .NET minden próba előtt kiszámítja és elküldi az előjeles
scanout-korrekciót mikroszekundumban, legfeljebb három tizedesjeggyel. Ez
egyben új próbát is jelent: az ESP ilyenkor eldobja az előző próba esetleg
megmaradt adatait (pl. ha az előző próbában nem jött kattintás, és így `RTIME`
sem), és a fotodióda fényjelére kezd várni:

```text
-9259.259
```

Az alapértelmezett helyen (jobb alsó sarok) a jelző szinte mindig az inger
alatt van, ezért a korrekció jellemzően negatív: az inger sora korábban
rajzolódik ki, mint a jelzőé.

A korrekció legkorábban **200 ms-mal** az előző próba vége után megy ki
(amikor a jelző visszavált feketére: kattintás vagy időtúllépés). Enélkül az
ESP a fehér → fekete váltás után még lecsengő fotodióda-jelet az új próba
fényjelének vehette, és a reakcióidőt a próba elejétől mérte (az
előperiódussal megnövelve, pl. 1500–2800 ms 200–300 ms helyett). Az ESP
ettől függetlenül is csak fekete → fehér átmenetet fogad el: az új próba
kezdete után előbb a küszöb alatti (sötét) jelet kell látnia, és csak az ezt
követő küszöb feletti érték számít fényjelnek. Az első próba előtt nincs ilyen
várakozás: a jelző a mérési jelenet megjelenése óta fekete.

Ha a jelzőt az inger fölé helyezték át (Testreszabás, Hardver fül), a
korrekció pozitív, például:

```text
8333.333
```

Az ESP a `CHECK`, a `VERSION`, az `RTIME` és a két diagnosztikai parancs
(`DIAG`, `ADC`, lásd [Diagnosztikai parancsok](#diagnosztikai-parancsok))
kivételével minden beérkező sort egyetlen előjeles lebegőpontos számként
értelmez, az előjelet megőrizve. Ami nem érvényes szám, azt figyelmen kívül
hagyja.

A számítás:

```text
frameTimeUs   = 1 000 000 / refreshRateHz
lineTimeUs    = frameTimeUs / verticalTotalLines
linesPerRow   = verticalActiveLines / displayHeightPx

scanoutCorrectionUs =
    (stimulusCenterY - photodiodeCenterY) * linesPerRow * lineTimeUs
```

A .NET az inger és a képernyőn megjelenő fotodióda-jelző tényleges
vertikális középpontját használja (képpontsorban, a kijelző tetejétől). A
korrekció tehát az inger **középső sorára** vonatkozik; ezt a mérési fájl
`ScanoutReferenceRow=StimulusCenter` metaadata rögzíti.

A képidőzítést a mérés elején egyszer, a Windows `QueryDisplayConfig`
API-jával kérdezi le a mérést futtató kijelzőre:

- `refreshRateHz`: a pontos, tört frekvencia (pl. 59,94 vagy 60,0031 Hz),
  nem egész számra kerekítve;
- `verticalTotalLines`: a képjel összes sora, a képkockák közti holtidővel
  (vertical blanking) együtt, pl. 1080 látható sornál 1125 vagy 1140;
- `verticalActiveLines`: a látható sorok száma; ha eltér az asztal
  magasságától (`displayHeightPx`), a videokártya skáláz, és ezt a
  `linesPerRow` szorzó kezeli.

A holtidő figyelembevétele számít: a holtidő nélküli `(Δy / displayHeight) *
frameTime` képlet a korrekciót a `verticalTotalLines / displayHeight` arányban
(tipikusan 4–6%-kal) túlbecsülné.

Ha a pontos időzítés nem kérdezhető le, a .NET az `EnumDisplaySettings`
egész Hz-es frekvenciájával és holtidő nélkül számol. Ezt a mérési fájl
metaadataiban a `ScanoutTimingExact=False` jelzi.

### A modell feltevései

A korrekció akkor érvényes, ha:

- a kijelző a képet soronként, fentről lefelé, állandó sorfrekvenciával
  rajzolja ki, progresszív (nem váltottsoros) képjellel és elforgatás nélkül.
  Elforgatott vagy váltottsoros kijelzőnél a .NET figyelmeztetést ír a
  naplóba és a metaadatokba (`ScanoutTimingWarning`);
- nincs bekapcsolva változó frissítési frekvencia (G-Sync / FreeSync / VRR).
  Ezt a program nem ismeri fel és nem jelzi, ezért a méréshez a kijelzőn és a
  videokártya beállításaiban ki kell kapcsolni;
- a monitor belső képfeldolgozása (skálázás, képpuffer) nem változtat a
  sorok egymáshoz képesti időzítésén. A monitor teljes bemeneti késleltetése
  nem számít, mert a fotodióda a ténylegesen kivilágító képet méri;
- az inger és a jelző ugyanabban a képkockában jelenik meg (lásd alább).

Ezek a feltevések egy fotodiódás validációs méréssel ellenőrizhetők, például
úgy, hogy az inger a kijelző tetején és alján is megjelenik.

## Fotodióda-jelző a képernyőn

- A mérési nézet keret nélküli teljes képernyőn fut, és a mérőfelület a teljes
  képernyő (alsó sáv nélkül). A jelző egy 56 × 56 képpontos négyzet,
  alapértelmezés szerint a mérőfelület jobb alsó sarkában, a jobb és az alsó
  széltől 20-20 képpontra; ha a fotodióda máshol van, a Testreszabás Hardver
  fülén áthelyezhető (`Hardware.PhotodiodeMarkerX/Y`). A fizikai fotodiódát
  erre a négyzetre, középre kell felhelyezni, a teljes képernyős mérési nézet
  megnyitása után. A pontos helyet a mérési fájl `PhotodiodeMarkerStagePx`
  metaadata rögzíti (pl. 1920 × 1080-as képernyőn, alapértelmezett helyen
  `1844,1004,56,56`). A scanout-korrekció a jelző tényleges helyével (a
  középpontja sorával) számol, így áthelyezett jelzőnél is helyes.
- A négyzetet egy 16 képpontos fekete **védősáv** veszi körül
  (`PhotodiodeGuardPx`). Ez az előtérben és a mérési jelenetben is, bármilyen
  háttérszínnél fekete, így egy világos háttér fénye sem jut a négyzet köré
  tett érzékelőre. Csak a belső négyzet vált fehérre.
- Az előtérben (a mérés indítása előtt) a négyzet körül egy 1 képpontos szürke
  keret segíti a felhelyezést. A keret csak az előtérben látszik, a mérési
  jelenetben nem.
- A jelző fekete, amíg nincs inger, és pontosan addig fehér, amíg az inger
  látszik.
- Az ingert és a jelzőt ugyanaz a rajzolás teszi ki, ugyanarra a dupla
  pufferelt felületre, egyetlen másolással. Így a kettő mindig ugyanabban a
  képkockában jelenik meg, és a korrekció képlete (amely ezt feltételezi)
  érvényes.
- Az inger soha nem kerül a jelzőre: a jelzőtől és a képernyő szélétől is
  legalább 36 képpontra marad (`StageSafeMarginPx`). A védősáv ezen a margón
  belül van, így az inger elhelyezését nem befolyásolja.

### Mihez képest mér az ESP?

A fotodióda a **jelzőt** látja, nem az ingert. Az ESP reakcióideje ezért a
jelző fekete → fehér átmenetéhez van kötve, a scanout-korrekció pedig ezt az
időpontot tolja el az inger középső sorának kirajzolásáig. A jelző mindig
ugyanazt az átmenetet mutatja (fekete négyzet a fekete védősávban, majd fehér),
függetlenül az inger színétől és a háttértől, így az ESP küszöbe minden
beállításnál ugyanúgy működik.

Az inger saját képpontjai viszont a beállított háttérszínről a beállított
ingerszínre váltanak. Egy LCD/OLED kijelzőn ennek az átmenetnek a válaszideje
színpáronként eltér (pl. sötét, kis kontrasztú színeknél vagy overdrive
mellett más lehet, mint a fekete → fehér váltásnál). Az alapértelmezett
színektől (fehér inger `#1E1E1E` háttéren) eltérő beállításnál ezért érdemes
egy fotodiódás ellenőrző mérést végezni:

1. Egy második fotodiódát (oszcilloszkóppal vagy logikai analizátorral) tegyél
   a képernyő egy olyan pontjára, ahol az inger megjelenhet, a jelzőn lévő
   fotodióda maradjon a helyén.
2. Futtass egy mérést a vizsgált színekkel. A mérési fájl `StimulusCenterX`,
   `StimulusCenterY` és `StimulusDiameterPx` oszlopából kiválaszthatók azok a
   próbák, amelyekben az inger lefedte a második érzékelőt.
3. Ezekben a próbákban a két érzékelő jelének időkülönbsége, a két hely
   sorkülönbségéből adódó scanout-idővel csökkentve, megadja, mennyivel később
   (vagy korábban) éri el az inger a látható fényességet, mint a jelző. Ha ez
   nem elhanyagolható, az ESP-reakcióidőket ezzel kell korrigálni, vagy más
   színpárt kell választani.

A színeket a mérési fájl `StimulusColor` és `BackgroundColor` metaadata
rögzíti, így az ellenőrzés eredménye utólag is a megfelelő mérésekhez
rendelhető.

## Reakcióidő: `RTIME` → szám vagy `NA`

A próba végét a PC dönti el: a válasz az operációs rendszer által érzékelt
első egérgomb-lenyomás a mérőfelületen, az inger megjelenése után. (Az
előperiódus alatti kattintás nem válasz, de a próbát érvényteleníti: elsietett
válasz, lásd [MERESI_BEALLITASOK.md](MERESI_BEALLITASOK.md#elsietett-válasz).)
A PC ekkor eltünteti az ingert, rögzíti a saját, helyi reakcióidejét, majd
`RTIME` üzenetet küld. Az ESP erre a már korrigált, nemnegatív reakcióidőt
küldi vissza mikroszekundumban, három tizedesjeggyel:

```text
250000.000
```

Ez 250 ms reakcióidőt jelent. Az ESP a reakcióidőt csak `RTIME`-ra küldi,
magától nem. A gomblenyomást az ESP a PC-nél előbb érzékeli (a PC-hez az egér
USB-n, késleltetéssel jut el), így a válasz azonnal küldhető.

Az ESP csak a jelző fekete → fehér váltása utáni első gomblenyomást veszi
figyelembe. A fényjel előtti lenyomás nem számít, és ha a gomb a fényjel
pillanatában még le van nyomva, csak a felengedés utáni új lenyomás lesz a
válasz. A gomb egy alacsony jel után 30 ms-ig számít lenyomottnak
(pergésmentesítés); a reakcióidő az első alacsony jel idejéhez tartozik.

Ha az ESP-nek a próbához nincs mérése, `NA` sort küld:

```text
NA
```

Ez akkor fordul elő, ha a korrekció megérkezése óta nem érzékelt fényjelet
(fekete → fehér átmenetet), a fényjel után nem érzékelt gomblenyomást, vagy a
korrigált reakcióidő negatív lenne.

A .NET két választ fogad el: az `NA`-t (kis- és nagybetűtől függetlenül) és
egy nemnegatív, véges, invariáns formátumú számot. Bármelyik azonnal lezárja a
várakozást; ha egyik sem érkezik, a .NET legfeljebb 1 másodpercig vár.
Mértékegység, előtag, mezőnév vagy egyéb karakter nem számít válasznak.

`NA` vagy időtúllépés esetén a próba **ESP-reakcióidő nélkül** kerül
rögzítésre (`EspReactionTimeMs` üres), a helyi reakcióidővel, és a mérés
folytatódik. A próba érvényességét ilyenkor a helyi reakcióidő alapján dönti
el a program.

A válasz nem tartalmaz próbaazonosítót: a .NET az `RTIME` után érkező első
elfogadható sort az éppen futó próbához rendeli. Mivel az ESP csak kérésre
válaszol, és minden korrekció új próbát kezd, egy korábbi próba válasza csak
akkor keveredhetne ide, ha az 1 másodperces várakozás után, késve érkezne meg.
A `CHECK` és a `VERSION` válasza (`OK`, illetve a két pontot tartalmazó
verziószám) nem érvényes `RTIME`-válasz, így ezek nem zárhatják le a várakozást.

Ha a soros kommunikáció mérés közben megszakad (pl. kihúzzák az ESP-t), a program az ESP-t a mérés hátralévő
részére lezárja, a mérést a PC által érzékelt kattintásokkal befejezi, és a
mérés végén figyelmeztet. A mérés így ESP-hiba esetén is végigfut, csak
ESP-adat nélkül.

Az ESP számítása:

```text
correctedStimulusTimestampUs =
    rawPhotodiodeTimestampUs + scanoutCorrectionUs

reactionTimeUs =
    mouseTimestampUs - correctedStimulusTimestampUs
```

A soros kommunikáció és az egér USB-s késleltetése nem része a
reakcióidőnek. Az időbélyegek az ESP `esp_timer` órájából származnak
(64 bites mikroszekundum-számláló, amely nem csordul túl); a fotodiódát és a gombot egy folyamatosan futó, külön
processzormagra rögzített feladat figyeli. A firmware és a bekötés leírása:
[README.md](../README.md#firmware-és-hardver).

## Diagnosztikai parancsok

A firmware (1.1.0-tól) két további parancsot ismer. Ezeket a .NET program nem
küldi; soros monitorból (115200 baud, a parancs után sortörés) kérhetők le, a
mérőlánc ellenőrzéséhez. Mindkét válasz betűvel kezdődik, így nem téveszthető
össze egy `RTIME`-válasszal.

| Parancs | Válasz | Jelentés |
|---|---|---|
| `DIAG` | `DIAG ciklus=<n> atlag_us=<x> max_us=<y> adc_min=<a> adc_max=<b> kuszob=475`, vagy `DIAG NA` | a legutóbbi fényjelre várás (a korrekció megérkezésétől a fényjelig) adatai: a mérő ciklus lefutásainak száma, átlagos és legnagyobb periódusideje µs-ban, valamint a közben olvasott legkisebb és legnagyobb nyers ADC-érték. `DIAG NA`, ha még nem volt próba |
| `ADC` | `ADC <érték>`, vagy `NA` | egyetlen nyers ADC-olvasás (0–4095) a fotodióda bemenetén. Csak próbán kívül ad értéket; ha az ESP éppen fényjelre vagy gomblenyomásra vár, a válasz `NA` |

Az `atlag_us` a fényjel időbélyegének felbontását adja meg (a küszöb elérése és
az észlelése között legfeljebb egy ciklus telik el), a `max_us` a legrosszabb
esetet. Az `adc_min` a jelző sötét szintjét, az `adc_max` a küszöböt elsőként
elérő mintát mutatja. A jelző teljes világos szintje az `ADC` paranccsal
olvasható ki, miközben a jelző fehér (például a Testreszabás Hardver fülének
áthelyező nézetében); a sötét szint ugyanígy a mérési nézet előterében.
