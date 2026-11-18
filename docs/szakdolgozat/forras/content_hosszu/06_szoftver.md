# A PC-s vezérlőalkalmazás

A PC-s alkalmazás feladata a mérés vezérlése és értelmezése: megjeleníti az ingereket, kiszámítja a scanout-korrekciót, kommunikál az ESP-vel és a szemkövetővel, eldönti a próbák érvényességét, tárolja és megjeleníti az eredményeket. A reakcióidő mérése nem a feladata; a saját órájával mért idő csak összehasonlításra szolgál. A fejezet a program 2.0.0-s verzióját írja le.

## Technológiai környezet

Az alkalmazás .NET 10 alapú, C# nyelven írt, 64 bites Windows-alkalmazás, Windows Forms felülettel. A .NET választását az indokolja, hogy a PC-s oldalon több, felhasználóhoz és eszközintegrációhoz kötött feladatot kell stabilan kezelni: grafikus felület, soros port, a szemkövető gyártói programkönyvtára, fájlkezelés. Ezekhez a környezet kész, dokumentált megoldásokat ad [44].

!table A PC-s alkalmazás technológiai jellemzői || 4.6,11.4
Jellemző | Érték
Célkeretrendszer | `net10.0-windows`, x64
Felület | Windows Forms, PerMonitorV2 DPI-mód (minden koordináta fizikai képpont)
Külső csomagok | `System.IO.Ports` 10.0.0 (soros port), `Tobii.Research.x64` 2.1.0.1 (Tobii Pro SDK)
Forráskód mérete | körülbelül 10 500 sor C# (a tervező által generált fájlok nélkül)
Verzió | 2.0.0; a mérési fájlokba a forrás commit-azonosítójával együtt kerül
Adatok helye | `Dokumentumok\ReakcioidoMeres` (beállítások, mérések, tekintetminták, napló)

A Tobii Pro SDK a fordításkor a NuGet-csomagtárból töltődik le, a natív könyvtárával együtt; a licence nem kereskedelmi, kutatási célú felhasználást enged. A PerMonitorV2 DPI-mód azért lényeges, mert így a program által használt koordináták a Windows nagyításától függetlenül a kijelző tényleges képpontjai: az inger helye, a fotodióda-jelző helye és a tekintet képernyőkoordinátája ugyanabban a rendszerben értendő.

## Rétegzett felépítés

A forráskód rétegekre tagolódik; a mappák a névtereknek felelnek meg.

!fig fig_retegek.png | 15 | Az alkalmazás rétegei és függőségeik. A nyilak a függés irányát mutatják.

!table A rétegek felelőssége || 3.0,9.8,3.2
Réteg | Felelősség | Nem függ tőle
Domain | a mérés modellje: eredmények, beállítások a tartományaikkal és ellenőrzésükkel, próbaterv, az előperiódus mintavétele, a mérőfelület geometriája, ingerelhelyezés, a scanout-képlet | felület, hardver, fájlrendszer
Measurement | az alkalmazáslogika: a mérés menete (`MeasurementPresenter`, `TrialRunner`), a próba érvényessége (`TrialEvaluator`), a tekintetszabályok, a nézetek és a külső eszközök interfészei | Windows Forms, konkrét hardver
Infrastructure | a külső eszközök megvalósítása: Tobii, ESP és soros port, CSV-tárolás, JSON-beállítások, a kijelző képidőzítésének és fizikai méretének lekérdezése | felület
UI | a nézetek: főablak, menü, kalibráció, mérés, statisztika, Testreszabás, a fixációs pont és a fotodióda-jelző kijelölője; közös rajzolási segédek | Infrastructure
Diagnostics | naplózás háttérszálon, fájlba | –

A rétegzés mérési szempontból is hasznos. A mérés logikája (mikor érvényes egy próba, hová kerülhet az inger, mekkora a korrekció) a Domain és a Measurement rétegben van, ezek nem hivatkoznak sem a felületre, sem a hardverre. A számítások így önmagukban, eszközök nélkül is ellenőrizhetők, és egy másik szemkövető vagy válaszeszköz bevezetése nem érinti a mérés szabályait.

## Model–View–Presenter és függőséginjektálás

A nézetek a Model–View–Presenter (MVP) minta passzív nézet változatát követik [57]. A nézet csak megjelenít és eseményeket továbbít (például „a felhasználó a Mérés indítása gombra kattintott”, „kattintás történt a mérőfelületen”), döntést nem hoz. A presenter hoz meg minden döntést, kiszámolja a nézet teljes állapotát, és egyben adja át. A mérési nézetnél ez azt jelenti, hogy a `MeasurementView` nem tudja, mi az előperiódus vagy mikor érvényes egy próba; csak azt tudja, mit kell kirajzolnia.

!table Nézetek és presenterek || 4.4,4.2,7.4
Nézet | Presenter | Feladat
`MeasurementView` | `MeasurementPresenter`, `TrialRunner` | a mérés indítása, a próbák futtatása, lezárás
`CalibrationView` | `CalibrationPresenter` | a meglévő kalibráció ellenőrzése, újrakalibrálás indítása
`StatisticsView` | `StatisticsPresenter` | a mentett mérések betöltése és megjelenítése
`MeasurementSettingsDialog` | `SettingsEditorPresenter` | a beállítások szerkesztése, ellenőrzése, mentése

A presenterek minden külső függősége interfész: a szemkövető (`IGazeTracker`), a válaszeszköz (`IResponseDevice`, `IResponseDeviceConnector`), a tárolás (`IMeasurementRepository`), a beállítások (`IExperimentSettingsProvider`) és a kijelző képidőzítése (`IDisplayTimingProvider`). A konkrét megvalósításokat a program belépési pontja (`Program.Main`, a composition root) hozza létre, és konstruktoron keresztül adja át; külön függőséginjektáló keretrendszer nincs („pure DI”) [58]. A megoldás következménye, hogy a presenterek hamis nézettel és hamis eszközökkel, hardver nélkül is futtathatók.

A tekintet-ellenőrzés módjai közötti különbséget stratégiaobjektumok hordozzák: az `IGazeValidationRule` három megvalósítása (`NoGazeValidationRule`, `LookAtStimulusRule`, `KeepFixationRule`) adja meg a mérés nevét, a résztvevőnek szóló instrukciót, azt, hogy kell-e szemkövető, és magát a tekintet alapú döntést. A nézet és a presenter minden módban ugyanaz.

## Egy mérés menete

### A mérési nézet két állapota

A mérési nézet teljes képernyős, keret nélküli ablak, két állapottal. Az **előtérben** egy kártya látszik a résztvevőnek szóló instrukcióval, a beállítások rövid összefoglalójával és az előző mérés kimenetelével. A **mérési jelenetben** csak a beállított háttér, a fixációs kereszt, a fotodióda-jelző és – a próbák alatt – az inger látszik. Szöveg mérés közben nincs a képernyőn, így szövegváltozás sem eshet egybe az inger megjelenésével, és nem vonja el a figyelmet.

### Az indítás feltételei

A „Mérés indítása” gomb után a program minden feltételt a próbák előtt ellenőriz, ebben a sorrendben:

1. újra betölti és ellenőrzi a beállításokat;
2. tekintet-ellenőrzéses mérésnél megkeresi a szemkövetőt (ha nincs, a mérés nem indul); ellenőrzés nélküli mérésnél a szemkövetőt csak használja, ha elérhető;
3. lekérdezi a mérőfelület méretét, és ellenőrzi, hogy a fixációs pont és az ingertávolságok elférnek-e a képernyőn;
4. lekérdezi a kijelző képidőzítését a scanout-korrekcióhoz;
5. tekintet-ellenőrzésnél ellenőrzi, hogy a szemkövető kijelzőbeállítása (display area) a mérést futtató monitorhoz illik-e;
6. megnyitja a soros portot, ellenőrzi az ESP-t (`CHECK` → `OK`), és lekérdezi a firmware verzióját;
7. létrehozza a mérési fájlt, és beleírja a metaadatokat.

Ha bármelyik lépés nem sikerül, a program minden erőforrást felszabadít, hibaüzenetet ad, és a nézet az előtérben marad. A mérés tehát vagy minden feltétel teljesülésével indul el, vagy egyáltalán nem.

A futó mérés a beállítások egy saját másolatából és egy változatlan pillanatképből dolgozik (`SessionContext`: beállítások, tekintetszabály, geometria, véletlenmag, színek). A próbák, a kiértékelés és a metaadatok csak ebből olvasnak, így a beállítások mérés közbeni módosítása nem hat a futó mérésre, és a fájlba írt metaadatok biztosan azt írják le, amivel a mérés lefutott.

### Egy próba lépései

A mérési jelenet megjelenése után a program a beállított ideig (alapértelmezés: 2 s) vár, hogy a szem alkalmazkodjon az új háttérhez. Ezután következnek a gyakorló, majd a mért próbák. Egy próba lépései:

1. a program kisorsolja az inger helyét, és kiszámítja a hozzá tartozó scanout-korrekciót;
2. megvárja, hogy az előző próba vége óta legalább 200 ms teljen el (a jelző visszaváltása és a fotodióda-jel lecsengése miatt);
3. elküldi a korrekciót az ESP-nek; ez egyben az új próba kezdete az ESP számára;
4. elindítja a tekintet rögzítését (ha van szemkövető);
5. kivárja az előperiódust; az ezalatt érkező kattintásokat megszámolja;
6. egyetlen rajzolással megjeleníti az ingert és fehérre váltja a jelzőt, közvetlenül utána időbélyeget készít, és megnyitja a válaszablakot;
7. az első bal gombos kattintásra (vagy az időkorlát lejártakor) eltünteti az ingert, rögzíti a helyi reakcióidőt és a kattintás helyét;
8. ha volt kattintás, `RTIME` kéréssel lekérdezi az ESP reakcióidejét;
9. lezárja a tekintet rögzítését, eldönti a próba érvényességét, és a próba sorát azonnal a fájlba írja.

A 2. lépés egy fejlesztés közben feltárt hiba javítása. Kezdetben a korrekció közvetlenül az előző próba vége után ment ki. Ilyenkor az ESP a fehér → fekete váltás után még lecsengő jelet az új próba fényjelének vehette, és a reakcióidőt a próba elejétől mérte, az előperiódussal megnövelve (200–300 ms helyett 1500–2800 ms). A hibát a PC és az ESP idejének összevetése tette láthatóvá: a 2026. november 8-i fejlesztői mérések egy részében a kettő különbsége egyes próbákban több mint −1000 ms volt. A javítás kétoldalú: a PC kivárja a 200 ms-ot, az ESP pedig csak sötét állapotot követő fényjelet fogad el (5.3. szakasz).

### A próbák száma és az érvénytelen próbák

Egy mérés pontosan a beállított számú gyakorló és mért próbából áll, ebben a sorrendben. A gyakorló próbák is mentődnek, de az eredménybe nem számítanak bele. Az érvénytelen próbák **nem pótlódnak**: az érvénytelenség okával együtt rögzülnek, és a mérés folytatódik. A program korábbi változata a kívánt számú érvényes próba eléréséig ismételt; a végleges megoldás azért tért el ettől, mert így minden mérés ugyanannyi ingerből áll, a mérés hossza előre ismert, és az érvénytelen próbák aránya maga is értelmezhető adat (2.12. szakasz).

### Az előperiódus

Az inger előtti várakozás (előperiódus) hossza próbánként véletlen, hogy a résztvevő ne tudja ritmusból megjósolni az inger megjelenését [18] [56]. Kétféle eloszlás választható: egyenletes, vagy csonkolt exponenciális (ez az alapértelmezés). A csonkolt exponenciális eloszlás eloszlásfüggvénye a legrövidebb (a) és a leghosszabb (b) várakozás között, μ paraméterrel:

!eq F(x) = \frac{1 - e^{-(x-a)/\mu}}{1 - e^{-(b-a)/\mu}}, \qquad a \leq x \leq b

A mintavétel inverz eloszlásfüggvénnyel történik, egyetlen egyenletes eloszlású véletlen számból (u), újrasorsolás nélkül:

!eq x = a - \mu \cdot \ln\left(1 - u \cdot \left(1 - e^{-(b-a)/\mu}\right)\right)

Az alapértékek: a = 1000 ms, b = 3000 ms, μ = 700 ms. Az exponenciális eloszlás előnye, hogy az inger megjelenésének feltételes valószínűsége a várakozás során lassabban nő, mint egyenletes eloszlásnál: a résztvevő abból, hogy az inger még nem jelent meg, kevesebbet tud meg arról, mikor fog megjelenni.

!fig fig_foreperiod.png | 15 | Az előperiódus két választható eloszlása az alapértékekkel: sűrűségfüggvény (balra) és az inger megjelenésének feltételes rátája az eltelt várakozás függvényében (jobbra).

Az ábra jobb oldala mutatja a különbséget: egyenletes eloszlásnál a feltételes ráta a várakozás elejétől folyamatosan nő, a csonkolt exponenciálisnál a tartomány nagy részén közel állandó, és csak a felső határ közelében nő meg (a csonkolás miatt). A program a sorsolt és a ténylegesen eltelt előperiódust is rögzíti.

### Az inger elhelyezése

Az inger kitöltött kör, amelynek középpontja a fixációs ponttól adott távolságban (r) és irányban (θ) van. Egy hely akkor megengedett, ha az inger befoglaló négyzete a képernyő szélétől és a fotodióda-jelzőtől is legalább 36 képpontra marad. A távolság próbánként egyenletes eloszlású a beállított legkisebb és a képernyőn ténylegesen elférő legnagyobb érték között; az irány egyenletes az adott távolságban elférő irányok között.

A mintavétel kétlépcsős: a program a távolságot egyszer sorsolja, és ha a sorsolt irányban az inger nem fér el, csak az irányt sorsolja újra; végül 0,1 fokos lépésekkel megkeresi az összes elférő irányt, és azok közül választ. Ha a távolságot és az irányt együtt sorsolná újra, a nagy távolságok – ahol kevesebb irány fér el – ritkábban fordulnának elő, mint a beállítás szerint kellene. A legnagyobb elférő távolságot a program bináris kereséssel határozza meg, és a metaadatok között rögzíti.

Minden mérés saját véletlenmagot kap, amely a mérési fájlba kerül. Ugyanazzal a programverzióval, beállításokkal és képernyőmérettel a mag az ingerhelyek és az előperiódusok teljes sorozatát meghatározza, így egy mérés ingersorozata utólag reprodukálható.

## Megjelenítés: a mérőfelület és a fotodióda-jelző

A mérőfelület a mérést futtató képernyő egésze. Állandó elemei a fixációs kereszt és a fotodióda-jelző.

!fig fig_stage.png | 16 | A mérőfelület elemei 1920 × 1080 képpontos képernyőn, az alapértékekkel: fixációs kereszt, a legkisebb és a legnagyobb ingertávolság köre, egy inger, a fotodióda-jelző a tiltott környezetével, és narancssárgával a tekintet elfogadási tartománya a két módban.

!table A mérőfelület rögzített méretei (`Domain/StageLayout.cs`) || 4.8,3.0,8.2
Elem | Méret | Megjegyzés
Fotodióda-jelző | 56 × 56 képpont | alapértelmezés szerint a jobb alsó sarokban, a szélektől 20 képpontra; áthelyezhető
Védősáv a jelző körül | 16 képpont | mindig fekete, bármilyen háttérnél; a háttér fénye nem jut az érzékelő köré
Biztonsági távolság | 36 képpont | az inger és a kereszt legalább ennyire marad a képernyő szélétől és a jelzőtől
Fixációs kereszt | 20 képpontos szárak, 3 képpont vastag fehér vonal 2 képpontos fekete körvonallal | a kettős rajzolás miatt bármilyen háttéren látszik; körülírt körének sugara 14 képpont

A jelző fekete, amíg nincs inger, és pontosan addig fehér, amíg az inger látszik. Mivel mindig ugyanazt a fekete → fehér átmenetet mutatja fekete környezetben, az ESP küszöbe független az inger és a háttér beállított színétől.

**Az inger és a jelző egy képkockában.** A mérési elv feltételezi, hogy az inger és a jelző ugyanabban a képkockában jelenik meg. A program ezt úgy éri el, hogy a kettőt ugyanaz a rajzolási művelet teszi ki ugyanarra a kettősen pufferelt felületre: a kép előbb egy háttérpufferben készül el, majd egyetlen másolással kerül a képernyőre. A rajzolás szinkron (a hívás csak a másolás után tér vissza), a program közvetlenül utána készít időbélyeget. [BIZONYITAS: Annak igazolása, hogy az inger és a jelző valóban mindig ugyanabban a képkockában jelenik meg. A Windows Forms rajzolása nincs a kijelző frissítéséhez szinkronizálva, és a kép az ablakkezelő (DWM) kompozícióján keresztül jut a kijelzőre; elvileg előfordulhat, hogy a másolás két képkocka közé esik. Igazolható két fotodiódával (egy a jelzőn, egy az inger helyén) és oszcilloszkóppal: a két jel időkülönbségének sok próbán át meg kell egyeznie a két hely scanout-idejével, képkockányi ugrások nélkül.]

A Windows Forms rajzolása nincs a kijelző frissítéséhez kötve, ezért a program nem tudja, melyik képkockában jelenik meg az inger. Ez a mérési elv miatt nem hiba: a megjelenés tényleges idejét a fotodióda méri, nem a szoftver. A helyi (PC-s) reakcióidő kezdete viszont emiatt egy-két képkockával megelőzi az inger tényleges megjelenését.

A fotodióda-jelző helye a géphez tartozó beállítás. Ha az érzékelő nem a jobb alsó sarokba kerül, a Testreszabás Hardver fülén megnyitható teljes képernyős áthelyező nézetben a jelző húzással, kattintással vagy a nyílbillentyűkkel a szenzor alá vihető. Az áthelyező két célzóvonalat rajzol a jelző közepén át, rajtuk a középtől 2,5 cm-re keresztjelekkel; a milliméter–képpont átváltáshoz a program a monitor EDID-adataiból olvassa ki a kijelző fizikai méretét. A scanout-korrekció a jelző tényleges helyével számol, így áthelyezett jelzőnél is helyes.

## A scanout-korrekció

A legtöbb kijelző a képet soronként, fentről lefelé rajzolja ki, ezért a képernyő különböző magasságában lévő képpontok nem egyszerre váltanak (2.6. szakasz). A fotodióda a jelző sorának kirajzolását érzékeli, a résztvevő viszont az ingerre reagál, amely más sorban van. A korrekció a két sor kirajzolása közötti időt adja meg.

!fig fig_scanout.png | 14 | A scanout-korrekció elve: az inger középső sora és a jelző középső sora közötti sorkülönbségből számított időeltérés.

A TMDK-dolgozat egyszerűsített képlete a képkockaidőt a kijelző magasságával osztotta. A végleges számítás pontosabb: figyelembe veszi, hogy a képjel a látható sorokon kívül holtidős sorokat (vertical blanking) is tartalmaz, hogy a frissítési frekvencia nem egész szám, és hogy a videokártya skálázhat. A számítás:

!eq T_{\mathrm{kép}} = \frac{10^6}{f}\ \mathrm{\mu s}, \qquad T_{\mathrm{sor}} = \frac{T_{\mathrm{kép}}}{N_{\mathrm{összes}}}, \qquad k = \frac{N_{\mathrm{aktív}}}{H}

!eq \Delta t_{\mathrm{scanout}} = \left(y_{\mathrm{inger}} - y_{\mathrm{jelző}}\right) \cdot k \cdot T_{\mathrm{sor}}

ahol *f* a pontos frissítési frekvencia, *N*összes a képjel összes sora a holtidővel együtt, *N*aktív a látható sorok száma, *H* az asztal magassága képpontban, *y*inger és *y*jelző pedig az inger, illetve a jelző középpontjának sora a kijelző tetejétől (egész sorra kerekítve). A *k* szorzó azt adja meg, hány jelsor tartozik egy képpontsorhoz; értéke 1, ha nincs skálázás.

A bemeneteket a program a mérés elején egyszer, a Windows `QueryDisplayConfig` függvényével kérdezi le a mérést futtató kijelzőre [50] [51]. Ha a pontos időzítés nem kérdezhető le, a program egész frekvenciával és holtidő nélkül számol, és ezt a mérési fájlban jelzi (`ScanoutTimingExact=False`).

**Számpélda.** A fejlesztői mérésekhez használt kijelző adatai a mérési fájlok szerint: 1920 × 1080 képpont, 74,9725 Hz, 1080 látható és összesen 1119 sor. Ebből a képkockaidő 13 338,2 µs, egy sor ideje 11,92 µs. Az egyik próbában az inger középpontja egész sorra kerekítve a 260., a jelzőé az 1033. sorban volt; a korrekció (260 − 1033) · 11,92 µs ≈ −9214 µs, vagyis az inger sora körülbelül 9,2 ms-mal a jelző sora előtt rajzolódik ki. A mérési fájlban ehhez a próbához −9213,983 µs szerepel. Ezen a kijelzőn a korrekció a teljes képmagasságra legfeljebb körülbelül 12,9 ms; a holtidő figyelmen kívül hagyása ezt 1119/1080 arányban, azaz 3,6%-kal becsülné túl.

A korrekció az inger **középső sorára** vonatkozik. Egy 60 képpont átmérőjű inger felső és alsó széle között ezen a kijelzőn körülbelül 0,7 ms telik el; az inger tehát nem egyetlen pillanatban jelenik meg, a középső sor ennek az intervallumnak a közepe.

A modell feltevései:

- a kijelző a képet soronként, fentről lefelé, állandó sorfrekvenciával, progresszív képjellel és elforgatás nélkül rajzolja ki (elforgatott vagy váltottsoros kijelzőnél a program figyelmeztetést rögzít);
- nincs bekapcsolva változó frissítési frekvencia (G-Sync, FreeSync); ezt a program nem tudja felismerni, ezért a mérőhely beállításakor kell kikapcsolni;
- a monitor belső képfeldolgozása nem változtat a sorok egymáshoz viszonyított időzítésén (a teljes bemeneti késleltetés nem számít, mert a fotodióda a ténylegesen megjelenő képet méri);
- az inger és a jelző ugyanabban a képkockában jelenik meg.

[BIZONYITAS: A scanout-modell igazolása a mérésre használt kijelzőn: két fotodiódával (vagy az inger több függőleges pozíciójában megismételt méréssel) annak kimutatása, hogy a jelző és az inger helyén mért fényjel időkülönbsége a sorkülönbséggel lineárisan nő, és a meredekség megegyezik a számított soridővel. A mérésből megadható a modell maradék hibája (a mért és a számított eltérés különbségének átlaga és szórása).]

## Soros kommunikáció az ESP-vel

A kommunikáció szöveges, soronkénti; minden üzenetet sortörés zár le, a számok tizedesponttal szerepelnek. A port beállításai: 115200 baud (állítható), 8 adatbit, paritás nélkül, 1 stopbit, áramlásvezérlés nélkül.

!table A PC és az ESP közötti üzenetek || 3.6,4.6,7.8
PC → ESP | ESP → PC | Mikor és miért
`CHECK` | `OK` | a mérés indításakor; a mérés csak `OK` válasz után indul. Legfeljebb három kísérlet, egyenként 1 s várakozással (az ESP a port megnyitásakor újraindulhat)
`VERSION` | verziószám, pl. `1.1.0` | a sikeres `CHECK` után egyszer; a PC legfeljebb 500 ms-ig vár. Ha nem jön válasz (régebbi firmware), a verzió ismeretlen marad, a mérés ettől még elindul
előjeles szám, pl. `-9259.259` | – | minden próba előtt: a scanout-korrekció µs-ban; egyben az új próba kezdete
`RTIME` | szám µs-ban (pl. `250000.000`) vagy `NA` | a résztvevő kattintása után; a PC legfeljebb 1 s-ig vár

A soros réteg négy osztályra oszlik. Az `EspResponseDevice` a protokollt valósítja meg. A `Serial` a soralapú kérés–válasz kezelést adja: háttérben futó olvasóciklus párosítja a beérkező sorokat a várakozó kérésekkel, időkorláttal. A `SerialPortConnection` a rendszer soros portját burkolja. Az `EspPortFinder` automatikusan megkeresi az ESP portját a rendszerleíró adatbázis USB-azonosítói alapján (az Espressif saját azonosítója, valamint a fejlesztőlapokon szokásos CP210x, CH340/CH9102 és FTDI átalakítók); ha a gépen több ilyen eszköz van, a port a Testreszabásban választható ki.

A program a portot háttérszálon nyitja meg, foglalt portnál legfeljebb nyolcszor próbálkozik, a megnyitás után 1,5 másodpercet vár, és kiüríti a puffereket, mielőtt az olvasást elindítja.

A válasz nem tartalmaz próbaazonosítót: a PC az `RTIME` után érkező első elfogadható sort az éppen futó próbához rendeli. Mivel az ESP csak kérésre válaszol, és minden korrekció új próbát kezd, egy korábbi próba válasza csak akkor keveredhetne ide, ha az 1 másodperces várakozás lejárta után, késve érkezne meg. A PC csak kétféle választ fogad el: az `NA`-t és egy nemnegatív, véges számot; minden más sort figyelmen kívül hagy.

Ha az ESP `NA`-t válaszol vagy nem válaszol, a próba ESP-reakcióidő nélkül, a helyi idővel rögzül. Ha a soros kapcsolat mérés közben megszakad, a program az ESP-t a mérés hátralévő részére lezárja, a mérést a PC-n mért időkkel befejezi, és a végén figyelmeztet.

A firmware verziója a mérési fájl metaadatai közé kerül (`EspFirmwareVersion`), így utólag is megállapítható, melyik firmware-változattal készült egy mérés. A verziószám két pontot tartalmaz, ezért sem érvényes számnak, sem `OK` vagy `NA` válasznak nem értelmezhető, és nem keveredhet össze más kérés válaszával.

## Szemmozgáskövetés és tekintetszabályok

A reakcióidő számítása akkor is lehet technikailag pontos, ha a próba viselkedési szempontból érvénytelen: a résztvevő nem oda nézett, ahová a feladat szerint kellett. A szemkövetés ezt ellenőrzi. A Tobii eszköz nem az ESP helyett mér, és nem kerül be a reakcióidő képletébe.

### A Tobii Pro SDK használata

A program a szemkövetőt a Tobii Pro SDK .NET-változatán keresztül éri el [52]. A felhasznált funkciók:

- **eszközkeresés** (`FindAllEyeTrackers`): háttérszálon fut, mert másodpercekig is eltarthat;
- **tekintetadat-folyam** (`GazeDataReceived` esemény): mintánként a két szem tekintetpontja a kijelző síkján, normalizált (0–1) koordinátában, szemenkénti érvényességi jelzéssel és az eszköz időbélyegével;
- **kijelzőbeállítás** (`GetDisplayArea`): a kijelző mérete és térbeli helyzete a szemkövetőhöz képest; ebből számítható a kijelző fizikai mérete és a szem távolsága a kijelző síkjától;
- **mintavételi frekvencia** (`GetGazeOutputFrequency`), amely a metaadatok közé kerül;
- **rendszeridő** (`GetSystemTimeStamp`): a tekintetminták időalapja, amelyhez a program az inger kirajzolásának és a válasznak az időpontját is hozzárendeli.

Egy tekintetminta akkor érvényes, ha legalább az egyik szem érvényes, és a koordinátái véges számok. A minták a szemkövető saját szálán érkeznek; a próba teljes ideje alatt (előperiódus, inger, válasz) rögzülnek, a döntésbe azonban csak a válaszablakba eső minták számítanak. A program a szem merőleges távolságát is rögzíti a kijelző síkjától (a két szem átlagát), hogy a képpontban megadott méretek utólag látószögre válthatók legyenek.

A kalibrációt nem a program végzi, hanem a gyártó programja (Tobii Pro Eye Tracker Manager), amelyet a program el tud indítani. A kalibráció nézetben két lehetőség van: a meglévő kalibráció használata vagy újrakalibrálás; a program mindkét esetben ellenőrzi, hogy egy másodperc alatt érkezik-e érvényes tekintetadat. Tekintet-ellenőrzéses mérés csak megerősített kalibráció után nyitható meg.

A TMDK-dolgozat irodalomjegyzékében a szemkövető szoftveres integrációjánál a Tobii játékokhoz szánt fejlesztői csomagjára és fogyasztói eszközeire vonatkozó források szerepeltek ([45], [46]). A megvalósított program a kutatási célú Tobii Pro SDK-t használja, ezért ezek helyett a [52] és az [53] forrás az irányadó.

### A három tekintet-ellenőrzési mód

!table A tekintet-ellenőrzés módjai || 3.4,3.6,6.0,3.0
Mód | A mérés neve | A próba akkor érvényes, ha… | Szemkövető
Nincs (`None`) | Mérés tekintet-ellenőrzés nélkül | az időkorláton belül érkezett nem elsietett válasz | nem kell; ha van, az adat rögzül
Az ingert kell nézni (`LookAtStimulus`) | Normál mérés | a válaszablak **legalább egy** érvényes mintája az inger középpontjától legfeljebb d/2 + R távolságra van | kötelező
A fixációs pontot kell nézni (`KeepFixation`) | Periférikus mérés | a válaszablak **minden** érvényes mintája a fixációs ponttól legfeljebb R távolságra van | kötelező

A táblázatban d az inger átmérője, R az elfogadási rádiusz (alapérték: 80 képpont). A két mód a TMDK-dolgozat direkt és perifériás mérésének felel meg; a harmadik, ellenőrzés nélküli mód új, és szemkövető nélküli mérést tesz lehetővé.

Az elfogadási rádiusz jelentése módonként más. Normál módban az inger **szélén túl** elfogadott tekintethiba, így nem függ az inger méretétől. Periférikus módban a fixációs pont körüli kör sugara. Mindkét esetben területet, nem egyetlen képpontot jelöl ki, összhangban azzal, hogy a szemkövetés pontossága természeténél fogva területként értelmezhető [37]. Periférikus módban a próba akkor is érvénytelen, ha a résztvevő az ingerre pillantott, de a válasz pillanatára már visszanézett.

A **válaszablak** az inger kirajzolásától a PC által érzékelt első kattintásig (időtúllépésnél az időkorlát lejártáig) tart. Az ablak akkor kezdődik, amikor a rajzolás hívása visszatér; ez egy-két képkockával megelőzi az inger tényleges megjelenését. Ennek hatása a szabályokra eltérő: periférikus módban az ablak eleje szigorúbbá teszi a szabályt (a megjelenés előtti pillanatokban is a keresztet kell nézni, ami egyébként is elvárás), normál módban közömbös, mert ott egyetlen ingerre eső minta elegendő.

A beállítások ellenőrzése biztosítja, hogy a két terület elkülönüljön: tekintet-ellenőrzésnél az inger legkisebb távolsága nagyobb kell legyen, mint az inger sugara és az elfogadási rádiusz összege. Enélkül normál módban a kereszt nézése is az inger nézésének számíthatna, periférikus módban pedig az ingerre nézés is a megengedett körön belül maradhatna.

Periférikus módban a fixációs pont nemcsak a képernyő közepén lehet: a Testreszabásban a teljes képernyős mérőfelületen bárhová kijelölhető, ahol a szabályok engedik. Ez nagyobb ingertávolságokat tesz lehetővé egy irányban, például ha a kereszt a képernyő egyik szélének közelében van.

[BIZONYITAS: A szemkövető tényleges pontossága a mérőhelyen (a kalibráció utáni átlagos eltérés ismert pontokra nézve, képpontban és fokban), és ebből az elfogadási rádiusz alapértékének (80 képpont) indoklása. A program a kalibráció pontosságát nem méri; igazolandó, hogy a rádiusz nagyobb a szemkövető hibájánál, de elég kicsi ahhoz, hogy az ingerre nézést megkülönböztesse a fixációtól.]

## A próba érvényességének eldöntése

A program minden próbához pontosan egy érvénytelenségi okot rendel, vagy érvényesnek minősíti. A döntést a `TrialEvaluator` hozza meg, rögzített sorrendben; az első teljesülő feltétel adja az okot.

!table Az érvénytelenségi okok a döntés sorrendjében || 0.9,3.9,3.3,7.9
# | Ok | Felirat | Feltétel
1 | `Timeout` | Időtúllépés | nem érkezett kattintás a beállított időn belül (alapérték: 3000 ms)
2 | `Anticipation` | Elsietett válasz | kattintás történt már az előperiódus alatt, vagy a reakcióidő rövidebb 100 ms-nál (az ESP ideje, ennek hiányában a helyi idő alapján)
3 | `MissedStimulus` | Mellékattintás | ingerre kattintós válaszmódban a kattintás helye az inger sugaránál messzebb van a középpontjától
4 | – | Érvényes | tekintet-ellenőrzés nélküli mérésben minden más próba érvényes
5 | `TrackerUnavailable` | Nincs szemkövető | a próba teljes ideje alatt egyetlen tekintetminta sem érkezett
6 | `InsufficientGazeData` | Kevés tekintetadat | a válaszablakba nem esett minta, vagy az érvényes minták aránya a beállított határ (alapérték: 50%) alatt van
7 | `GazeViolation` | Tekintethiba | a tekintetszabály nem teljesült

A szabályok megfelelnek az irodalmi áttekintésben leírt szűrési szinteknek (2.12. szakasz). Az időalapú szűrés alsó határa 100 ms; ez a határ rögzített, a beállításfájlban nem módosítható, és a mérési fájl metaadatai között szerepel. Az előperiódus alatti kattintás külön eset: az ilyen kattintás nem válasz (az inger ettől még megjelenik, és a program megvárja a tényleges választ), de a próbát érvényteleníti, mert a résztvevő nem az ingerre várva reagált.

A válasz módja kétféle lehet. Az alapértelmezett módban a válasz az inger megjelenése utáni első bal gombos kattintás bárhol a mérőfelületen; az egérmutató ilyenkor nem látszik. A másik módban a résztvevőnek az ingerre kell kattintania, az egérmutató látszik, és a mellékattintás külön okkal érvénytelen. Válasznak mindkét módban csak a bal gomb számít, mert az ESP is csak ennek a kapcsolóját figyeli.

## Testreszabhatóság és beállítások

A TMDK-dolgozat idején a program két rögzített mérési módot kínált. A végleges változatban egyetlen mérés van, amelynek minden lényeges paramétere állítható a főmenüből megnyitható Testreszabás ablakban. Az ablak négy fülből áll, mellettük folyamatosan frissülő előnézet mutatja a mérési képernyő kicsinyített modelljét és az ingert valós, képpontra pontos méretben.

!table A testreszabható beállítások || 3.2,5.6,3.6,3.6
Fül | Beállítás | Tartomány | Alapérték
Ingerek és időzítés | mért ingerek száma | 1–1000 | 10
 | gyakorló ingerek száma | 0–100 | 3
 | válasz módja | kattintás bárhol / az ingerre | kattintás bárhol
 | maximális várakozás a válaszra | 100–60 000 ms | 3000 ms
 | előperiódus eloszlása | exponenciális / egyenletes | exponenciális
 | előperiódus legrövidebb és leghosszabb értéke | 0–60 000 ms | 1000 és 3000 ms
 | exponenciális várható érték | 1–60 000 ms | 700 ms
 | várakozás a mérési képernyő megjelenése után | 0–10 000 ms | 2000 ms
Megjelenés | inger színe, háttérszín | tetszőleges szín | fehér, sötétszürke (#1E1E1E)
 | inger átmérője | 4–500 képpont | 60
 | inger távolsága a fixációs ponttól (legalább, legfeljebb) | 0–10 000 képpont | 150 és 500
Szemmozgás | tekintet-ellenőrzés módja | nincs / ingert nézni / fixációt tartani | ingert nézni
 | fixációs pont helye | középen / egyéni hely | középen
 | elfogadási rádiusz | 1–2000 képpont | 80
 | érvényes tekintetminták minimális aránya | 1–100% | 50%
Hardver | ESP soros portja | automatikus / választott port | automatikus
 | átviteli sebesség | 1200–4 000 000 baud | 115200
 | a fotodióda-jelző helye | a mérőfelületen bárhol | jobb alsó sarok

### Ellenőrzés: hibák és figyelmeztetések

A beállítások ellenőrzése három szinten történik. Az egyes mezők tartományán túl a program összefüggő szabályokat is vizsgál: az inger és a háttér színe nem lehet azonos; a legkisebb távolság nem lehet nagyobb a legnagyobbnál; az inger nem takarhatja a fixációs keresztet; tekintet-ellenőrzésnél az inger legközelebbi helye is kívül esik az elfogadási tartományon. A képernyőtől függő feltételeket (elfér-e a legkisebb távolság, nem esik-e a fixációs pont túl közel a szélhez vagy a jelzőhöz, nem vágja-e ketté a jelző az inger lehetséges helyeit) a program a mérést futtató képernyő méretével ellenőrzi.

A **hibák** megakadályozzák a mentést és a mérés indítását. A **figyelmeztetések** módszertani jellegűek, és nem tiltanak: ilyen a 3:1 alatti kontrasztarány az inger és a háttér között, az ingerre kattintós válasz periférikus módban (a célzáshoz általában oda kell nézni), vagy a tekintet-ellenőrzés gyakorló inger nélkül. Minden üzenet megnevezi azt a fület, ahol a probléma javítható.

A vezérlők megengedett tartománya és az ellenőrzés ugyanabból a forrásból (`SettingsLimits`) dolgozik, a mérés indításakor pedig ugyanaz az ellenőrzés fut le, mint az ablakban. Így nem fordulhat elő, hogy az ablak elfogad egy beállítást, amellyel a mérés nem indítható.

### A beállításfájl

A beállítások egy JSON-fájlban (`beallitasok.json`) tárolódnak, amely kézzel is szerkeszthető; a teljes alapértelmezett fájl a B mellékletben található. A beolvasás szándékosan szigorú: ismeretlen (például elgépelt) mező, rossz típusú érték vagy kétszer szereplő mező hiba, és a mérés nem indul el. Ennek oka, hogy egy csendben alapértékre eső beállítással a mérés nem azzal futna, amit a kutató beállított, és ez utólag nem derülne ki.

A mentés atomikus: a tartalom előbb egy ideiglenes fájlba kerül, lemezre ürítve, és csak utána lép a régi fájl helyére; az előző változat biztonsági másolatként megmarad. Egy írás közbeni hiba így nem hagyhat félig írt beállításfájlt. A fájl formátumának verziószáma van; a korábbi (látószög alapú) formátumot a program felismeri és elutasítja, újabb programverzió fájlját pedig nem próbálja értelmezni.

## Adattárolás

A program minden mérést két szöveges (CSV) fájlba ment: az egyik a mérés metaadatait és próbánként egy sort, a másik a mérés összes tekintetmintáját tartalmazza. A formátum választását az indokolja, hogy a fájlok táblázatkezelőben és statisztikai környezetben (R, Python) közvetlenül megnyithatók, és emberi szemmel is olvashatók.

A mérési fájl elején megjegyzéssorokban a mérés minden körülménye szerepel: a programverzió a forrás azonosítójával, a szemkövető típusa és mintavételi frekvenciája, a soros port, a kijelző felbontása és pontos képidőzítése, a mérőfelület geometriája (a jelző, a kereszt és a biztonsági távolságok képpontban), az összes beállítás és a véletlenmag. A cél az, hogy egy mérési fájl önmagában, a program és a beállításfájl nélkül is elegendő legyen a mérés körülményeinek rekonstruálásához. A próbánkénti oszlopok jegyzéke a C mellékletben található.

Próbánként a fájlba kerül többek között a hardveres (ESP) és a helyi (PC) reakcióidő, az érvényesség és az érvénytelenség oka, a sorsolt és a tényleges előperiódus, az előperiódus alatti kattintások száma, az elküldött scanout-korrekció, az inger helye és távolsága, a tekintetadatok minősége, a szem távolsága és a kattintás helye. A tekintetminták fájljában mintánként szerepel az időbélyeg, az inger megjelenéséhez viszonyított idő, a két szem érvényessége, a tekintet helye és a szem távolsága.

A mentés próbánként történik: a program minden próba sorát azonnal a fájl végére írja, és a lemezre üríti. Megszakítás vagy programhiba után az addig rögzített próbák megmaradnak; a fájl végén a lezáró sor hiánya jelzi, hogy a mérés nem zárult le szabályosan. Új mérés nem írhat felül meglévő fájlt. A fájlok a mérés alatt más program számára olvasásra megnyithatók.

## Statisztika

A statisztika nézetben bármelyik korábbi mérés kiválasztható. A táblázat minden próbát mutat a két reakcióidővel, a kettő különbségével és az érvényesség állapotával. Az összesítés az érvényes mért próbák átlagát, mediánját és szórását adja meg a hardveres időre, a helyi időre és a kettő különbségére. A reakcióidők eloszlása jobbra ferde [59], ezért elemzéshez a medián a megbízhatóbb mutató.

A **PC − ESP** különbség a rendszer késleltetésének próbánkénti becslése: megmutatja, mennyivel hosszabb a szoftveres időbélyegekből számolt reakcióidő a hardveresen mértnél. Az átlaga a mérőlánc jellemző késleltetését, a szórása a késleltetés próbáról próbára való ingadozását mutatja. Ez az érték egyben a rendszer működésének ellenőrzésére is alkalmas: a szokatlanul nagy vagy negatív különbség hibára utal (lásd a 6.4. szakaszban leírt esetet).

## Szálkezelés és hibakezelés

A nézet eseményei és a mérés előkészítése a felhasználói felület szálán futnak. A próbák ciklusa háttérszálon fut, és minden, a nézetet vagy a szemkövető állapotát érintő műveletet a felület szálára küld. Az inger kirajzolása és az időbélyeg készítése egyetlen, a felület szálán futó műveletben történik: előbb a szinkron rajzolás, közvetlenül utána az időbélyeg. A szemkövető mintái a gyártói könyvtár saját szálán érkeznek, a felvétel zárolással védett. A soros port olvasása és a naplózás külön háttérszálon fut, így sem a kattintás kezelését, sem a rajzolást nem lassítja.

A hibakezelés elve az, hogy a mérés közben keletkező adat ne vesszen el, és a résztvevő elé mérés közben ne kerüljön hibaüzenet:

!table A mérés közbeni hibák kezelése || 5.0,11.0
Esemény | A program viselkedése
Az ESP egy próbában nem mért (`NA`) vagy nem válaszol | a próba a helyi idővel rögzül, a mérés folytatódik; a mérés végén a program jelzi, hány próbánál nincs hardveres idő
A soros kapcsolat megszakad | az ESP a mérés hátralévő részére lezárul, a mérés a helyi időkkel befejeződik; figyelmeztetés a mérés végén
A szemkövető kiesik tekintet-ellenőrzéses mérésben | a mérés leáll (minden további próba érvénytelen lenne), az addigi próbák mentve maradnak
A szemkövető kiesik ellenőrzés nélküli mérésben | a mérés tekintetadat nélkül folytatódik
Mentési hiba | a mérés megszakad, a már kiírt próbák megmaradnak
A résztvevő vagy a kutató megszakítja (Esc) | a mérés megszakítottként zárul le, az addigi próbák mentve maradnak
Váratlan programhiba | a naplóba kerül; a mérési fájl lezáró sor nélkül marad, ami jelzi a szabálytalan befejezést

A mérés vége után a gombok 1,5 másodpercig nem használhatók, hogy a válaszegér egy késve érkező kattintása ne indítson azonnal új mérést. Minden hiba részletei a naplófájlban is megtalálhatók.

A program fejlesztői módban (`--dev` kapcsoló) a főablakon megjeleníti a tekintet pillanatnyi helyét, a statisztikában pedig a scanout-korrekció oszlopát; a mérési nézeten ez a kijelzés rejtve marad.
