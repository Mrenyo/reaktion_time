# Felhasználói útmutató

Ez az útmutató a mérést vezető kutatónak szól: az első beállítástól a mérés
testreszabásán és lefolytatásán át az eredmények értelmezéséig. A program
2.0.0-s verziójára vonatkozik.

## Fogalmak

A dokumentáció (ez az útmutató, a [README](../README.md), a
[MERESI_BEALLITASOK.md](MERESI_BEALLITASOK.md), a
[SCANOUT_PROTOCOL.md](SCANOUT_PROTOCOL.md) és a
[FEJLESZTOI_DOKUMENTACIO.md](FEJLESZTOI_DOKUMENTACIO.md)) ezeket a szavakat
mindenhol ugyanabban az értelemben használja:

| Fogalom | Jelentés |
|---|---|
| **mérési nézet** | a program teljes képernyős nézete, amelyet a Mérés gomb nyit meg; két állapota az előtér és a mérési jelenet |
| **előtér** | a mérési nézet mérés előtti és utáni állapota: kártya az instrukcióval, az összefoglalóval és a gombokkal (nem a háttér ellentéte) |
| **mérési jelenet** | a mérési nézet mérés közbeni állapota: csak a beállított háttér, a fixációs kereszt, a fotodióda-jelző és az inger |
| **mérőfelület** | az a terület, amelyre a mérési jelenet rajzolódik: a mérést futtató képernyő egésze; a koordináták ennek a képpontjaiban értendők |
| **próba** | egy inger megjelenítése és a rá adott válasz; a **gyakorló** próbák nem számítanak bele az eredménybe, a **mért** próbák igen |
| **inger** | a mérőfelületen megjelenő kitöltött kör, amelyre a résztvevő reagál |
| **fixációs kereszt, fixációs pont** | a kereszt, amelyet a résztvevő az inger előtt néz; a fixációs pont a kereszt középpontja |
| **fotodióda-jelző** | a mérőfelület kis négyzete, amely az ingerrel együtt vált feketéről fehérre; erre kerül a fotodióda |
| **előperiódus** | a véletlen hosszú várakozás a próba kezdete és az inger megjelenése között |
| **válaszablak** | az inger kirajzolásától a válaszig (vagy az időkorlát lejártáig) tartó idő; a tekintet-ellenőrzés ennek a mintáiból dönt |
| **tekintet-ellenőrzés** | annak ellenőrzése a szemkövető adataiból, hogy a résztvevő oda nézett-e, ahová az utasítás szerint kellett; a felületen a Szemmozgás fül „Szemmozgás-validáció szükséges” kapcsolója |
| **normál mód, periférikus mód** | a tekintet-ellenőrzés két módja: normál módban az ingerre kell nézni, periférikus módban végig a fixációs pontra |
| **elfogadási rádiusz** | a tekintet megengedett eltérése képpontban: normál módban az inger szélétől, periférikus módban a fixációs ponttól |
| **elsietett válasz** | kattintás még az inger megjelenése előtt, vagy 100 ms-nál rövidebb reakcióidő; a próba érvénytelen |
| **ESP-idő, PC-idő** | a reakcióidő az ESP (fotodióda + gomb) mérése szerint, illetve a számítógép órája szerint (helyi idő); a referencia az ESP-idő |
| **scanout-korrekció** | a kijelző soronkénti kirajzolásából adódó időkülönbség az inger és a fotodióda-jelző sora között |
| **display area** | a Tobii Pro Eye Tracker Manager beállítása (a program angol nevén hivatkozik rá): a kijelző mérete és helyzete a szemkövetőhöz képest |
| **átviteli sebesség** | a soros port sebessége baudban |

## 1. Első beállítás (egyszer)

1. **ESP:** csatlakoztasd USB-n. A program a portját az USB-azonosítók alapján
   maga keresi meg. Ha a gépen több soros eszköz is van, és a program nem
   találja meg egyértelműen az ESP-t, válaszd ki a portját a Testreszabás
   **Hardver** fülén (lásd 2. fejezet). Az ESP-n a repó `firmware` mappájában
   lévő firmware-nek kell futnia (lásd [README](../README.md#firmware-és-hardver)).
2. **Tobii (csak tekintet-ellenőrzéshez):** telepítsd a Tobii Pro Eye Tracker
   Managert, csatlakoztasd a szemkövetőt, és állítsd be a **display area**-t arra
   a monitorra, amelyen a mérés fut (a monitor mérete és a szemkövető helyzete).
   A display area vetíti a tekintetet a képernyőre, ezért tekintet-ellenőrzéses
   mérés enélkül nem indul el. Tekintet-ellenőrzés nélküli méréshez a
   szemkövető nem kell; ha mégis csatlakoztatva van, a program a tekintetadatot
   ilyenkor is rögzíti.
3. **Beállítások:** az első indításkor létrejön a
   `Dokumentumok\ReakcioidoMeres\beallitasok.json` fájl az alapértékekkel. A
   beállításokat a főmenü Mérés kártyáján a **Testreszabás** gombbal módosíthatod
   (2. fejezet); a fájl kézzel is szerkeszthető
   ([MERESI_BEALLITASOK.md](MERESI_BEALLITASOK.md)). Ha a gépen egy korábbi
   programverzió (1.x) beállításfájlja van, azt a program nem tudja beolvasni:
   a mérés nem indul el, amíg a Testreszabásban újra be nem állítod és el nem
   mented a beállításokat (a régi fájl `.bak` kiterjesztéssel megmarad).
4. **Fotodióda:** nyisd meg a mérési nézetet (Mérés kártya, **Mérés** gomb;
   tekintet-ellenőrzéses beállításnál ehhez előbb kalibráció kell, lásd 3.
   fejezet). A nézet teljes képernyőre vált. A képernyő jobb alsó sarkában egy
   56 × 56 képpontos fekete négyzet látszik, körülötte egy fekete sáv, a négyzet
   körül pedig egy vékony szürke keret. A fotodiódát erre a négyzetre, középre
   kell felhelyezni. A keret csak a felhelyezést segíti, mérés közben nem
   látszik; a fekete sáv a mérés közben is megmarad, hogy a háttér fénye ne
   jusson az érzékelőre. Ha a fotodióda máshol van a képernyőn, a jelzőt a
   Testreszabás **Hardver** fülén lehet áthelyezni (lásd 2. fejezet).

## 2. A mérés testreszabása

A főmenü Mérés kártyáján a **Testreszabás** gomb nyitja meg a mérési
beállítások ablakát. Bal oldalt négy fül van, jobb oldalt egy folyamatosan
frissülő előnézet, alul az üzenetek és a gombok.

- Ha a beállításfájl nem volt olvasható (vagy régi, 1.x formátumú), az
  ablak tetején egy sárga sáv írja le, mi történt. Ha a fájl értékei hibásak, az
  ablak a fájlban lévő értékeket mutatja, így itt javíthatók; ha a fájl
  egyáltalán nem olvasható, alapértékek jelennek meg.
- Alul pirossal a **hibák**, sárgával a **figyelmeztetések** látszanak, minden
  módosítás után frissülve. Az a fül, amelyen hiba van, „⚠” jelet kap. Amíg van
  hiba, a Mentés gomb nem használható.
- A program a beállításokat annak a képernyőnek a méretével ellenőrzi, amelyen
  a főablak van: a mérés is ezen fut, teljes képernyőn.
- Gombok: **Alapértékek** (az első három fül alapértékei; a Hardver fül
  beállításai megmaradnak), **Mégse** (Esc), **Mentés** (Enter). A mentett
  beállítások a következő mérésindításkor érvényesülnek; a mérési nézet előtere
  azonnal az új beállításokat mutatja. Ha a mentés nem sikerül (pl. a fájl
  írásvédett, vagy egy másik program nyitva tartja), hibaüzenet jelenik meg, és
  az ablak a módosításokkal együtt nyitva marad.

### Ingerek és időzítés fül

| Mező | Tartomány | Alapérték | Jelentés |
|---|---|---|---|
| Válasz módja | Kattintás az inger megjelenésekor / Kattintás az ingerre | Kattintás az inger megjelenésekor | az elsőnél a résztvevő bárhová kattinthat, amint megjelenik az inger; a másodiknál az ingerre kell kattintania (az egérmutató ilyenkor látszik). A mellékattintás után is eltűnik az inger, de a próba érvénytelen („Mellékattintás”) |
| Mért ingerek száma (mérésszám) | 1–1000 | 10 | ennyi mért inger jelenik meg; az eredmény ezekből készül |
| Gyakorló (teszt) ingerek száma | 0–100 | 3 | a mért ingerek **előtt** jelennek meg; mentődnek, de az eredménybe nem számítanak bele |
| Maximális várakozás a válaszra | 100–60 000 ms | 3000 ms | ennyi idő után az inger eltűnik, a próba érvénytelen (időtúllépés), és jön a következő |
| Előperiódus (várakozás az inger előtt): eloszlás | Exponenciális (kiszámíthatatlan) / Egyenletes | Exponenciális | az exponenciális eloszlásnál a résztvevő nem tudja kiszámítani, mikor jön az inger |
| Előperiódus: legrövidebb / leghosszabb | 0–60 000 ms | 1000 / 3000 ms | a várakozás határai |
| Előperiódus: exponenciális várható érték | 1–60 000 ms | 700 ms | csak exponenciális eloszlásnál állítható |
| Várakozás a mérési képernyő megjelenése után | 0–10 000 ms | 2000 ms | a mérési jelenet megjelenése és az első inger között: a szem alkalmazkodik az új háttérhez |

A mezők alatt egy összegzés látszik, pl. „Összesen 13 inger jelenik meg:
először 3 gyakorló, utána 10 mért. A gyakorló ingerek nem számítanak bele az
eredménybe; az érvénytelen próbák nem pótlódnak.” Az érvénytelen próbák (pl.
időtúllépés, tekintethiba) helyett **nem** jön új próba: a mérés mindig
pontosan ennyi ingerből áll.

### Megjelenés fül

| Mező | Tartomány | Alapérték | Jelentés |
|---|---|---|---|
| Inger színe | `#RRGGBB` | `#FFFFFF` (fehér) | a színmintára kattintva színválasztó nyílik, vagy a kód beírható (a mezőből kilépéskor vagy Enterre érvényesül) |
| Háttérszín | `#RRGGBB` | `#1E1E1E` (sötétszürke) | a mérési jelenet háttere |
| ⇄ Színcsere | – | – | felcseréli az inger és a háttér színét |
| Inger mérete (átmérő) | 4–500 képpont | 60 | az inger egy kitöltött kör |
| Az inger távolsága a fixációs ponttól: legalább / legfeljebb | 0–10 000 képpont | 150 / 500 | az inger középpontjának távolsága; próbánként véletlenszerűen, ebben a tartományban |

Szabályok és figyelmeztetések:

- Az inger és a háttér színe nem lehet azonos. Ha a kontrasztjuk 3:1 alatt van,
  figyelmeztetés jelenik meg.
- Az inger nem takarhatja a keresztet: a legkisebb távolság legalább az inger
  sugara + 14 képpont. Tekintet-ellenőrzésnél ennél több kell: a legkisebb
  távolság legyen nagyobb, mint az inger sugara + az elfogadási rádiusz
  (különben a kereszt és az inger nézése nem különíthető el).
- Ha a legkisebb távolság a képernyőn egyik irányban sem fér el, az hiba. Ha a
  legnagyobb nem fér el, a program figyelmeztet („A legnagyobb távolságból csak
  … px fér el ezen a képernyőn.”), és a mérés a még elférő legnagyobbal fut.
- Minden méret képpontban értendő, ezért a fizikai méret a monitortól függ
  (lásd [MERESI_BEALLITASOK.md](MERESI_BEALLITASOK.md#képpontok-és-fizikai-méret)).

### Szemmozgás fül

- **Szemmozgás-validáció szükséges:** bekapcsolva a program a tekintet alapján
  ellenőrzi, hogy a résztvevő betartotta-e az utasítást. Kikapcsolva minden
  időben érkező válasz érvényes, és sem szemkövető, sem kalibráció nem kell.
- **Mit kell néznie a résztvevőnek?**
  - *Az ingert (normál reakcióidő):* a résztvevő a megjelenő ingerre néz, és
    úgy reagál. A próba érvényes, ha a tekintet a válaszig legalább egyszer az
    ingeren vagy a szélétől legfeljebb az elfogadási rádiusznyira volt.
  - *A fixációs pontot (periférikus reakcióidő):* a résztvevő végig a keresztet
    nézi, és úgy reagál a máshol megjelenő ingerre, hogy nem néz oda. A próba
    érvényes, ha a tekintet a válaszig végig a kereszt körüli, elfogadási
    rádiusz sugarú körön belül maradt.
- **A fixációs pont helye:** *Középen* vagy *Egyéni hely*. Csak akkor állítható,
  ha a résztvevőnek a fixációs pontot kell néznie; minden más esetben a kereszt
  a képernyő közepén van. Az egyéni helyet a **Kijelölés a mérőfelületen…**
  gombbal lehet kiválasztani (lásd lent); mellette a kijelölt pont látszik, pl.
  „x = 640, y = 360 px (33,3% · 33,3%)”, vagy „még nincs kijelölve”. Ha az
  Egyéni helyet választod, és még nincs kijelölt pont, a kijelölő magától
  megnyílik; ha ott a Mégse-t választod, a beállítás visszaáll Középenre. A
  kijelölt pont akkor is megmarad, ha közben másik módot választasz.
- **Elfogadási rádiusz** (1–2000 képpont, alapérték 80): normál módban az inger
  szélén túl elfogadott tekintethiba (így nem függ az inger méretétől),
  periférikus módban a fixációs pont körüli megengedett eltérés.
- **Érvényes tekintetminták minimális aránya** (1–100 %, alapérték 50 %): ha a
  válaszablakban ennél kevesebb a használható tekintetminta (pislogás,
  elveszett szem), a próba érvénytelen („Kevés tekintetadat”).

Ha a tekintet-ellenőrzés be van kapcsolva, de nincs gyakorló inger, a program
figyelmeztet: gyakorlás nélkül az első mért próbák nagyobb eséllyel
érvénytelenek.

### Hardver fül

- **ESP soros port:** „Automatikus felismerés” (a program az USB-azonosítók
  alapján keresi meg az ESP-t), vagy a listából egy COM-port, vagy beírt
  portnév. A lista a Testreszabás megnyitásakor elérhető portokat mutatja.
- **Átviteli sebesség (baud):** a listából (9600–921 600) vagy beírva,
  1200–4 000 000 között; alapérték 115200. Egyezzen az ESP firmware-ével.
- **A fotodióda-jelző helye:** alapértelmezés szerint a jobb alsó sarok. Ha a
  fotodióda máshová került, az **Áthelyezés a mérőfelületen…** gomb a mérés
  képernyőjén teljes képernyős áthelyezőt nyit, a beállított háttérrel és a
  fixációs kereszttel. A jelző itt fehéren világít, így jól látszik, hogy a
  szenzor alatta van-e.
  - A négyzet **húzással** mozgatható; máshová **kattintva** oda ugrik.
    **Nyilak:** finomhangolás 1 képponttal (Shifttel 10-zel), **R:** vissza a
    jobb alsó sarokba, **Enter** vagy **dupla kattintás:** kész, **Esc:** mégse.
  - A szenzor felhelyezését két piros **célzóvonal** segíti: egy vízszintes és
    egy függőleges vonal megy át a jelző közepén, a teljes képernyőn. Mindkét
    vonalon keresztjel van a középtől **2,5 cm-re**; ezekhez igazítsd a
    maszkkal felszerelt fotodiódát. A milliméter átváltásához a program a
    monitor EDID-adataiból olvassa ki a kijelző fizikai méretét. Ha ez nem
    érhető el (pl. egyes projektoroknál vagy virtuális kijelzőnél), csak a
    vonalak látszanak, és a szalag ezt jelzi.
  - A piros szaggatott keret jelzi, hová nem kerülhet inger és kereszt (a jelző
    36 képpontos környéke). A négyzet a képernyő legszéléig tolható (a sarokba
    is); ilyenkor a fekete sáv szél felőli része nem látszik, ez nem baj.
  - Ha a mérés az új hellyel nem indítható (pl. a középen lévő kereszt túl
    közel esne a jelzőhöz), a szalag pirosan írja az okát. A hely ettől még
    kijelölhető; a hibát a Testreszabásban kell javítani (pl. másik fixációs
    pont), addig a Mentés nem engedélyezett.
  - Az **Alaphelyzet (jobb alsó sarok)** gomb visszaállítja az alapértelmezett
    helyet. Az aktuális hely a gombok alatt látszik (a négyzet középpontja
    képpontban és a képernyőmérethez viszonyítva).
  - A hely a géphez tartozik: az **Alapértékek** gomb nem állítja vissza.

### Előnézet

A jobb oldali előnézet a mérési képernyő kicsinyített modellje („Előnézet –
1920 × 1080 px képernyő, kicsinyítve”): a beállított háttér, a fotodióda-jelző
a fekete sávjával, a fixációs kereszt, a legkisebb és a legnagyobb távolság
szaggatott köre, egy minta-inger a legkisebb távolságban, és narancssárga
szaggatott vonallal az elfogadási tartomány (normál módban a minta-inger körül,
periférikus módban a kereszt körül).

Alatta egy keretben az inger **valós méretben** látszik, a beállított háttéren,
képpontra pontosan („Valós méret: 60 px”; ha az inger nagyobb a keretnél: „–
nagyobb, mint a keret”). Ez akkor felel meg a mérésnek, ha az ablak ugyanazon a
monitoron van, amelyen a mérés fut.

### A fixációs pont kijelölése

A **Kijelölés a mérőfelületen…** gomb a mérés képernyőjén (ahol a főablak van)
teljes képernyős kijelölőt nyit, a beállított háttérrel, a fekete sávval és a
fotodióda-jelzővel, ahogy a mérésben látszik.

- A **csíkozott** területekre nem tehető kereszt: ez a képernyő szélén és a
  fotodióda-jelző körül egy-egy 50 képpontos sáv (a kereszt 14 képpontos köre +
  36 képpont biztonsági távolság).
- Az egér mozgatásakor a kereszt áttetsző előnézete követi a mutatót; pirosan
  jelenik meg, ahol nem helyezhető el (a tiltott sávban, vagy ahol a legkisebb
  távolságban az inger sehol sem férne el).
- **Kattintás** kijelöli a pontot, **dupla kattintás** kijelöli és be is fejezi
  a kijelölést. **Enter:** kész, **Esc:** mégse, **nyilak:** a kijelölt pont
  finomhangolása 1 képponttal (Shifttel 10 képponttal). A képernyő tetején (ha
  a pont alá esne: alján) egy sáv mutatja ezeket a lehetőségeket és a
  kijelölt pont helyét.
- A kijelölt kereszt körül szaggatott kör jelzi a legkisebb és a legnagyobb
  ingertávolságot, periférikus módban az elfogadási tartományt is.

A pont a képernyő szélességének és magasságának arányában tárolódik, így más
felbontású képernyőn arányosan ugyanoda kerül.

## 3. Egy mérés menete

1. **A résztvevő elhelyezése:** üljön kényelmesen a monitor (és ha van, a
   szemkövető) elé. Mivel a méretek képpontban vannak megadva, összehasonlítható
   mérésekhez a monitor, a felbontás és az ülési távolság is legyen ugyanaz.
2. **Kalibráció (csak tekintet-ellenőrzéshez):** a menüben a Kalibráció kártya.
   - *Meglévő kalibráció használata:* a program ellenőrzi, hogy van-e
     kalibráció a szemkövetőn, és hogy 1 másodperc alatt érkezik-e érvényes
     tekintetadat. Ehhez a résztvevőnek a képernyőt kell néznie.
   - *Újrakalibrálás:* a Tobii Pro Eye Tracker Manager kalibrációja indul el;
     utána a program ugyanúgy ellenőrzi, hogy érkezik-e érvényes tekintetadat.

   Tekintet-ellenőrzéses mérés csak kalibráció után nyitható meg („Kalibráció
   szükséges”). Ellenőrzés nélküli méréshez nem kell kalibráció.
3. **A mérési nézet megnyitása:** a Mérés kártya **Mérés** gombja. A nézet teljes
   képernyőre vált, és az **előteret** mutatja: az alkalmazás rögzített
   hátterén egy kártyát a „Reakcióidő-mérés” címmel, a résztvevőnek szóló
   instrukcióval (pl. „Nézz a keresztre. Amint megjelenik az inger, nézz rá, és
   a lehető leggyorsabban kattints. Utána nézz vissza a keresztre. Először 3
   gyakorló inger következik, ezek nem számítanak bele az eredménybe.
   (Megszakítás: Esc)”), az előző mérés kimenetelével, és a kutatónak szóló
   rövid összefoglalóval, pl. „10 mért + 3 gyakorló inger · válasz: kattintás
   bárhol · inger: 60 px, #FFFFFF a #1E1E1E háttéren · tekintet: az ingert kell
   nézni (elfogadási rádiusz: 80 px) · válaszidő-korlát: 3000 ms”. Hibás beállításoknál az
   összefoglaló az első hibát mutatja. A kártya alján a **Mérés indítása** és a
   **Vissza a menühöz** gomb van.
4. **Mérés indítása:** a résztvevő elolvassa az instrukciót, majd a kutató
   megnyomja a „Mérés indítása” gombot. A program:
   - újra betölti és ellenőrzi a beállításokat (hiba esetén üzenetet ad, és nem
     indul);
   - tekintet-ellenőrzésnél ellenőrzi a szemkövetőt és a display area-t; ha
     nincs ellenőrzés, a szemkövetőt csak használja, ha elérhető;
   - ellenőrzi, hogy a fixációs pont és az ingertávolságok elférnek-e a
     képernyőn;
   - megnyitja az ESP-kapcsolatot, és ellenőrzi, hogy az ESP válaszol-e
     (`CHECK` üzenetre `OK`);
   - létrehozza a mérési fájlt.

   Ha valamelyik lépés nem sikerül, hibaüzenet jelenik meg, és a nézet az
   előtérben marad.
5. **Mérés közben:** a képernyőn csak a **mérési jelenet** látszik: a beállított
   háttér, a fehér-fekete fixációs kereszt a helyén, a fotodióda fekete
   négyzete a fekete sávval, és a próbák alatt az inger. Szöveg nem látszik;
   az egérmutató csak ingerre kattintós módban látszik. Ebben a módban ügyelj
   rá, hogy a résztvevő ne hagyja az egérmutatót a fotodióda négyzetén, mert az
   zavarhatja az érzékelőt. A jelenet megjelenése után a program a beállított
   ideig (alapértelmezetten 2 másodpercig) vár, hogy a szem alkalmazkodjon az
   új háttérhez; utána jönnek a gyakorló, majd a mért ingerek. Minden próbában
   egy véletlen hosszú várakozás (előperiódus) után megjelenik az inger, és a
   résztvevő a lehető leggyorsabban reagál (a bal egérgombbal kattint). A
   választ a program a számítógép által érzékelt, az inger megjelenése utáni
   első bal gombos kattintásból veszi (a többi egérgomb nem számít, mert az ESP
   is csak a bal gombot érzékeli); ekkor az inger eltűnik, és a program lekéri az ESP-től a hardveresen
   mért reakcióidőt. Ha a résztvevő már az inger előtt kattint, az inger ettől
   még megjelenik, de a próba érvénytelen (elsietett válasz); ugyanígy
   érvénytelen a 100 ms-nál rövidebb reakcióidő. Ha a beállított időn belül
   nincs kattintás, az inger eltűnik, a próba érvénytelen (időtúllépés), és jön
   a következő. Az érvénytelen próbák helyett nem jön új próba. Ha az ESP egy
   próbában nem mért reakcióidőt (`NA` választ ad) vagy nem válaszol, illetve
   ha a mérés közben kiesik (pl. kihúzzák), a mérés nem áll le: az érintett
   próbákban csak a PC-n mért idő lesz meg, és a mérés végén erről
   figyelmeztetés jelenik meg. Ha a szemkövető esik ki mérés közben,
   tekintet-ellenőrzéses mérésnél a mérés leáll (az addigi próbák mentve
   maradnak), tekintet-ellenőrzés nélkül pedig tekintetadat nélkül folytatódik;
   a mérés végén erről is figyelmeztetés jelenik meg.
6. **Megszakítás:** az **Esc** billentyű leállítja a mérést, és visszatér az
   előtérbe. Az addig rögzített próbák mentve maradnak. Az előtérből a „Vissza
   a menühöz” gomb visz a főmenübe.
7. **A mérés vége:** a nézet visszatér az előtérbe, és a kártya mutatja az
   eredményt, pl. „A mérés elkészült: 9/10 érvényes próba, ESP medián: 312 ms.
   Az adatok elmentve.”, megszakításnál „A mérés megszakadt; az eddigi próbák
   elmentve (4 érvényes).” Ha voltak mért próbák ESP-reakcióidő nélkül, vagy
   próbák, amelyekben nem érkezett tekintetadat (a szemkövető kiesett), a
   szöveg végén erre „Figyelem: …” kezdetű mondat hívja fel a figyelmet. A
   gombok ilyenkor 1,5 másodpercig nem használhatók, hogy a válaszegér egy
   késve érkező kattintása ne indítson rögtön új mérést; utána az **Új mérés
   indítása** gombbal folytatható. Az eredmény addig látszik, amíg a mérési
   nézet nyitva van: a „Vissza a menühöz” után a nézet újra üres állapotsorral
   nyílik meg.

## 4. Az eredmények

A **Statisztika** nézetben kiválasztható bármelyik korábbi mérés.

- A táblázat minden próbát mutat. A gyakorló próbák „Gy” előtagot és
  „Gyakorló” feliratot kapnak, és nem számítanak bele az összesítésbe. Az
  időket a táblázat egy tizedesjegyre kerekítve mutatja.
- Az **ESP idő** a hardveresen (fotodióda + gomb), a **PC idő** a számítógépen
  mért reakcióidő. A **PC − ESP** oszlop a kettő különbsége, vagyis a
  rendszer késleltetése: mennyivel később jelenik meg az inger a képernyőn,
  mint ahogy a program kirajzolta, és mennyivel később dolgozza fel a
  számítógép a kattintást, mint ahogy a gomb lenyomódott. Ha valamelyik idő
  hiányzik, a különbség helyén „-” áll.
- A **Státusz** oszlop a mért próbák érvénytelenségének okát is megadja:

  | Felirat | Jelentés |
  |---|---|
  | Érvényes | a próba érvényes |
  | Időtúllépés | nem érkezett kattintás a beállított időn belül (Maximális várakozás a válaszra) |
  | Elsietett válasz | a résztvevő már az inger megjelenése előtt kattintott, vagy a reakcióidő 100 ms-nál rövidebb volt |
  | Mellékattintás | (ingerre kattintós módban) a résztvevő nem az ingerre kattintott |
  | Tekintethiba | a résztvevő nem tartotta be a tekintetre vonatkozó utasítást |
  | Kevés tekintetadat | a szemkövető a próba alatt túl sokszor elvesztette a szemet |
  | Nincs szemkövető | a próba alatt egyetlen tekintetminta sem érkezett (a szemkövető kiesett) |

- Az „Érvényes: 9 / 10” felirat az érvényes és az összes lefutott mért próba
  számát mutatja.
- Az **Összesítés** az érvényes mért próbák átlagát, mediánját és szórását
  mutatja: „ESP” a hardveres, „PC” a számítógépen mért időre, „PC − ESP” a
  kettő különbségére (csak azokból a próbákból, amelyekben mindkét idő
  megvan). Az „n” azt mutatja, hány próba értéke számít bele: az ESP-é kisebb
  lehet, ha az ESP egyes próbákban nem küldött reakcióidőt (ezekben az ESP
  oszlop „-”). A reakcióidők eloszlása jobbra ferde, ezért elemzéshez a medián
  a megbízhatóbb. A különbség szórása azt mutatja, mennyire ingadozik a
  rendszer késleltetése próbáról próbára.
- Ha egy mérési fájl nem olvasható be, vagy egy fájlban hibás (pl. félbemaradt)
  sorok vannak, a nézet ezt jelzi; a hibás sorok kimaradnak, a többi próba
  látszik. A részletek a naplóban vannak.
- A lista jelzi, ha egy mérés megszakadt vagy nem zárult le (pl. a program a
  mérés közben leállt). A „próbálkozási korlát” jelzés csak a korábbi
  programverziók méréseinél fordulhat elő.

A teljes adat (metaadatok, próbánkénti ingerhely és -távolság képpontban,
előperiódus, tekintetadat-minőség, szemtávolság, tekintetminták) a
`Dokumentumok\ReakcioidoMeres\measurements` mappában van, CSV-formátumban. Ez
Excelben, R-ben vagy Pythonban (pandas) közvetlenül megnyitható. A fájlokban a
„mért inger” `Phase=Test`, a „gyakorló (teszt) inger” `Phase=Practice`; az
oszlopok és a metaadatok leírása, valamint az utólagos látószög-számítás:
[MERESI_BEALLITASOK.md](MERESI_BEALLITASOK.md).

## 5. Hibaelhárítás

Az első oszlop a hibaablak címe, alatta dőlt betűvel az üzenet jellemző
részlete.

| Üzenet | Teendő |
|---|---|
| **Kalibráció szükséges** | végezz vagy válassz kalibrációt a Kalibráció kártyán, vagy kapcsold ki a tekintet-ellenőrzést a Testreszabásban (Szemmozgás fül) |
| **Tobii kapcsolati hiba** | (csak tekintet-ellenőrzésnél) ellenőrizd az USB-kapcsolatot és a Tobii Pro Eye Tracker Managert |
| **Nincs kalibráció** | a szemkövetőn nincs használható kalibráció: végezz újrakalibrálást |
| **Nem érzékelhető tekintet** | a résztvevő üljön a szemkövető elé, és nézzen a képernyőre; ha így sem sikerül, végezz újrakalibrálást |
| **Hiányzó kalibrációs program** | telepítsd a Tobii Pro Eye Tracker Managert |
| **A mérés nem indítható** – *A Tobii szemkövetőn nincs beállítva a kijelző (display area)…* | állítsd be a display area-t a mérést futtató monitorra; tekintet-ellenőrzés nélküli mérésnél ez csak figyelmeztetés (napló, `DisplayAreaWarning` metaadat) |
| **A mérés nem indítható** – *A Tobii display area (…) méretaránya nem egyezik…* | a display area másik monitorra van beállítva, vagy a program nem azon a monitoron fut |
| **COM-port foglalt** | zárd be a soros monitort (Arduino, PlatformIO) vagy a program másik példányát |
| **ESP kapcsolati hiba** – *Az ESP nem érhető el a(z) … porton*; a részletekben: *Nem sikerült egyértelműen azonosítani az ESP COM-portját* | csatlakoztasd újra, vagy válaszd ki a portot a Testreszabás Hardver fülén |
| **ESP kommunikációs hiba** – *…nem válaszolt a kapcsolatellenőrzésre* | a port megnyílt, de az ESP nem válaszolt `OK`-val a `CHECK` üzenetre: ellenőrizd, hogy a firmware fut-e, és az átviteli sebesség egyezik-e (Testreszabás, Hardver fül); szükség esetén csatlakoztasd újra az ESP-t |
| **Megszakadt az ESP-kapcsolat** (a mérés végén) | az ESP a mérés közben kiesett; a mérés ESP nélkül fejeződött be, az üzenetben megnevezett próbától nincs ESP-reakcióidő. Ellenőrizd a csatlakozást a következő mérés előtt |
| *Figyelem: … mért próbánál nincs ESP-reakcióidő.* (az előtér állapotsorában) | az ESP ezekben a próbákban nem mért: ellenőrizd, hogy a fotodióda a jelzőn van-e, és hogy a válaszgomb (egér) az ESP-re van-e kötve |
| *Figyelem: … próbában nem érkezett tekintetadat – ellenőrizd a szemkövető csatlakozását.* (az előtér állapotsorában) | a szemkövető a mérés közben kiesett; ezek a próbák tekintet-ellenőrzésnél érvénytelenek („Nincs szemkövető”) |
| **Hibás mérési beállítások** | nyisd meg a Testreszabást: az üzenet megnevezi a mezőt és a fület („⚠”), az ablak a hibás értékeket mutatja |
| *A beállításfájl nem olvasható (… sor …)* | javítsd a fájlt a megadott sorban (ismeretlen mezőnév, rossz típusú érték, JSON-hiba), vagy állítsd be a mérést a Testreszabásban, és mentsd (az előző fájl `.bak`-ként megmarad) |
| *A beállításfájl egy újabb programverzióhoz készült* | használd az újabb programot, vagy állítsd be újra a mérést a Testreszabásban (az előző fájl `.bak`-ként megmarad) |
| *A legkisebb távolság (… px) ezen a … képpontos képernyőn egyik irányban sem fér el* | csökkentsd a legkisebb távolságot vagy az inger méretét, vagy tedd a fixációs pontot közelebb a képernyő közepéhez |
| *A fixációs pont túl közel esik a képernyő széléhez* | jelölj ki új helyet a Szemmozgás fülön |
| *A fixációs pont / A képernyő közepén lévő fixációs kereszt túl közel esik a fotodióda-jelzőhöz* | helyezd át a jelzőt a Hardver fülön, vagy jelölj ki másik fixációs pontot a Szemmozgás fülön |
| *A fotodióda-jelző ezzel az ingermérettel kettévágja a képernyőn az inger lehetséges helyeit* | tedd a jelzőt (és a fotodiódát) közelebb a képernyő széléhez, vagy csökkentsd az inger méretét |
| *Jelölj ki fixációs pontot a mérőfelületen* | egyéni helyhez a Szemmozgás fülön ki kell jelölni a pontot |
| **A beállítások nem menthetők** | kapcsold ki a fájl „Csak olvasható” tulajdonságát, vagy zárd be a fájlt nyitva tartó programot; a módosítások az ablakban megmaradnak |
| **Mentési hiba** (mérés közben) | ellenőrizd, hogy az adatmappa írható-e (és van-e szabad hely); a már kiírt próbák megmaradnak |

Minden hiba a `Dokumentumok\ReakcioidoMeres\logs` mappa naplójában is
megtalálható, a részletekkel együtt.
