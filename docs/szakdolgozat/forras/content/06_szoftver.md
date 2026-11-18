# A PC-s vezérlőalkalmazás

A PC-s alkalmazás vezérli és értelmezi a mérést: megjeleníti az ingereket, kiszámítja a scanout-korrekciót, kommunikál az ESP-vel és a szemkövetővel, eldönti a próbák érvényességét, tárolja és megjeleníti az eredményeket. A reakcióidőt nem ez méri; a saját órájával mért idő csak összehasonlításra szolgál. A fejezet a program 2.0.0-s verzióját írja le.

## Technológiai környezet

Az alkalmazás .NET 10 alapú, C# nyelven írt, 64 bites Windows-alkalmazás, Windows Forms felülettel. A környezet a grafikus felülethez, a soros porthoz, a szemkövető gyártói programkönyvtárához és a fájlkezeléshez kész, dokumentált megoldásokat ad [44].

!table A PC-s alkalmazás technológiai jellemzői || 4.6,11.4
Jellemző | Érték
Célkeretrendszer | `net10.0-windows`, x64
Felület | Windows Forms, PerMonitorV2 DPI-mód (minden koordináta fizikai képpont)
Külső csomagok | `System.IO.Ports` 10.0.0 (soros port), `Tobii.Research.x64` 2.1.0.1 (Tobii Pro SDK)
Forráskód mérete | körülbelül 10 500 sor C# (a tervező által generált fájlok nélkül)
Verzió | 2.0.0; a mérési fájlokba a forrás commit-azonosítójával együtt kerül
Adatok helye | `Dokumentumok\ReakcioidoMeres` (beállítások, mérések, tekintetminták, napló)

A PerMonitorV2 DPI-mód miatt a program koordinátái a Windows nagyításától függetlenül a kijelző tényleges képpontjai: az inger, a fotodióda-jelző és a tekintet helye ugyanabban a rendszerben értendő. A Tobii Pro SDK licence nem kereskedelmi, kutatási célú felhasználást enged.

## Rétegzett felépítés

A forráskód rétegekre tagolódik; a mappák a névtereknek felelnek meg.

!fig fig_retegek.png | 15 | Az alkalmazás rétegei és függőségeik. A nyilak a függés irányát mutatják.

!table A rétegek felelőssége || 3.0,9.8,3.2
Réteg | Felelősség | Nem függ tőle
Domain | a mérés modellje: eredmények, beállítások, próbaterv, előperiódus, geometria, ingerelhelyezés, scanout-képlet | felület, hardver, fájlrendszer
Measurement | a mérés menete (`MeasurementPresenter`, `TrialRunner`), a próba érvényessége (`TrialEvaluator`), tekintetszabályok, interfészek | Windows Forms, konkrét hardver
Infrastructure | a külső eszközök megvalósítása: Tobii, ESP és soros port, CSV-tárolás, JSON-beállítások, kijelzőadatok | felület
UI | a nézetek és a közös rajzolási segédek | Infrastructure
Diagnostics | naplózás háttérszálon, fájlba | –

A mérés logikája (mikor érvényes egy próba, hová kerülhet az inger, mekkora a korrekció) a Domain és a Measurement rétegben van, amelyek sem a felületre, sem a hardverre nem hivatkoznak; a számítások így eszközök nélkül is ellenőrizhetők.

## Model–View–Presenter és függőséginjektálás

A nézetek a Model–View–Presenter (MVP) minta passzív nézet változatát követik [57]: a nézet (mérés, kalibráció, statisztika, Testreszabás) csak megjelenít és eseményeket továbbít, minden döntést a hozzá tartozó presenter hoz meg. A presenterek külső függőségei interfészek (`IGazeTracker`, `IResponseDevice`, `IMeasurementRepository`, `IExperimentSettingsProvider`, `IDisplayTimingProvider`); a megvalósításokat a belépési pont (`Program.Main`) hozza létre és konstruktoron át adja át, külön keretrendszer nélkül („pure DI”) [58], így a presenterek hardver nélkül is futtathatók. A tekintet-ellenőrzés módjait az `IGazeValidationRule` három megvalósítása (`NoGazeValidationRule`, `LookAtStimulusRule`, `KeepFixationRule`) hordozza; a nézet és a presenter minden módban ugyanaz.

## Egy mérés menete

A mérési nézet teljes képernyős, keret nélküli ablak. Az előtérben egy kártya mutatja az instrukciót, a beállítások összefoglalóját és az előző mérés kimenetelét; a mérési jelenetben csak a háttér, a fixációs kereszt, a fotodióda-jelző és az inger látszik, szöveg nem. A „Mérés indítása” gomb után a program a próbák előtt, ebben a sorrendben:

1. újra betölti és ellenőrzi a beállításokat;
2. tekintet-ellenőrzéses mérésnél megkeresi a szemkövetőt (ha nincs, a mérés nem indul); ellenőrzés nélküli mérésnél csak használja, ha elérhető;
3. ellenőrzi, hogy a fixációs pont és az ingertávolságok elférnek-e a mérőfelületen;
4. lekérdezi a kijelző képidőzítését a scanout-korrekcióhoz;
5. tekintet-ellenőrzésnél ellenőrzi, hogy a szemkövető kijelzőbeállítása (display area) a mérést futtató monitorhoz illik-e;
6. megnyitja a soros portot, ellenőrzi az ESP-t (`CHECK` → `OK`), és lekérdezi a firmware verzióját;
7. létrehozza a mérési fájlt, és beleírja a metaadatokat.

Ha bármelyik lépés sikertelen, a program felszabadítja az erőforrásokat, hibaüzenetet ad, és a mérés nem indul el. A futó mérés a beállítások változatlan pillanatképéből (`SessionContext`: beállítások, tekintetszabály, geometria, véletlenmag, színek) dolgozik, így a metaadatok azt írják le, amivel a mérés lefutott.

Egy mérés pontosan a beállított számú gyakorló, majd mért próbából áll; a gyakorló próbák mentődnek, de az eredménybe nem számítanak bele. Az érvénytelen próbák **nem pótlódnak**, hanem az okukkal együtt rögzülnek: így minden mérés ugyanannyi ingerből áll, a hossza előre ismert, és az érvénytelen próbák aránya maga is értelmezhető adat (2.12. szakasz).

### Egy próba lépései

A mérési jelenet megjelenése után a program a beállított ideig (alapértelmezés: 2 s) vár, hogy a szem alkalmazkodjon a háttérhez. Egy próba lépései:

1. a program kisorsolja az inger helyét, és kiszámítja a scanout-korrekciót;
2. megvárja, hogy az előző próba vége óta legalább 200 ms teljen el (a jelző visszaváltása és a fotodióda-jel lecsengése miatt);
3. elküldi a korrekciót az ESP-nek; ez egyben az új próba kezdete az ESP számára;
4. elindítja a tekintet rögzítését (ha van szemkövető);
5. kivárja az előperiódust; az ezalatt érkező kattintásokat megszámolja;
6. egyetlen rajzolással megjeleníti az ingert és fehérre váltja a jelzőt, közvetlenül utána időbélyeget készít, és megnyitja a válaszablakot;
7. az első bal gombos kattintásra (vagy az időkorlát lejártakor) eltünteti az ingert, rögzíti a helyi reakcióidőt és a kattintás helyét;
8. ha volt kattintás, `RTIME` kéréssel lekérdezi az ESP reakcióidejét;
9. lezárja a tekintet rögzítését, eldönti a próba érvényességét, és a próba sorát azonnal a fájlba írja.

A 2. lépés egy fejlesztés közben feltárt hiba javítása. Kezdetben a korrekció közvetlenül az előző próba vége után ment ki, így az ESP a fehér → fekete váltás után még lecsengő jelet az új próba fényjelének vehette, és a reakcióidőt az előperiódussal megnövelve mérte (200–300 ms helyett 1500–2800 ms). A hibát a PC és az ESP idejének összevetése mutatta ki: a 2026. november 8-i fejlesztői mérések egy részében a kettő különbsége egyes próbákban több mint −1000 ms volt. A javítás kétoldalú: a PC kivárja a 200 ms-ot, az ESP pedig csak sötét állapotot követő fényjelet fogad el (5.3. szakasz).

### Az előperiódus

Az inger előtti várakozás (előperiódus) hossza próbánként véletlen, hogy a résztvevő ne tudja ritmusból megjósolni az inger megjelenését [18] [56]. Az eloszlás egyenletes vagy csonkolt exponenciális (alapértelmezés). Az utóbbi eloszlásfüggvénye a legrövidebb (a) és a leghosszabb (b) várakozás között, μ paraméterrel:

!eq F(x) = \frac{1 - e^{-(x-a)/\mu}}{1 - e^{-(b-a)/\mu}}, \qquad a \leq x \leq b

A mintavétel inverz eloszlásfüggvénnyel történik, egyetlen egyenletes eloszlású véletlen számból (u), újrasorsolás nélkül:

!eq x = a - \mu \cdot \ln\left(1 - u \cdot \left(1 - e^{-(b-a)/\mu}\right)\right)

Az alapértékek: a = 1000 ms, b = 3000 ms, μ = 700 ms.

!fig fig_foreperiod.png | 15 | Az előperiódus két választható eloszlása az alapértékekkel: sűrűségfüggvény (balra) és az inger megjelenésének feltételes rátája az eltelt várakozás függvényében (jobbra).

Egyenletes eloszlásnál a feltételes ráta a várakozás elejétől folyamatosan nő, a csonkolt exponenciálisnál a tartomány első felében alig változik, és csak a felső határ közelében (a csonkolás miatt) nő meg; a résztvevő így abból, hogy az inger még nem jelent meg, kevesebbet tud meg a megjelenés idejéről. A program a sorsolt és a ténylegesen eltelt előperiódust is rögzíti.

### Az inger elhelyezése

Az inger kitöltött kör, középpontja a fixációs ponttól r távolságban és θ irányban van. Egy hely akkor megengedett, ha az inger befoglaló négyzete a képernyő szélétől és a fotodióda-jelzőtől is legalább 36 képpontra marad. A távolság próbánként egyenletes eloszlású a beállított legkisebb és a képernyőn elférő legnagyobb érték között (ezt a program bináris kereséssel határozza meg és a metaadatokban rögzíti); az irány egyenletes az adott távolságban elférő irányok között.

A program a távolságot egyszer sorsolja, és ha a sorsolt irányban az inger nem fér el, csak az irányt sorsolja újra; végül 0,1 fokos lépésekkel megkeresi az összes elférő irányt, és azok közül választ. Együttes újrasorsolásnál a nagy távolságok, ahol kevesebb irány fér el, a beállítottnál ritkábban fordulnának elő. Minden mérés saját véletlenmagot kap, amely a mérési fájlba kerül; azonos programverzióval, beállításokkal és képernyőmérettel a mag az ingerhelyek és az előperiódusok teljes sorozatát meghatározza.

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

**Az inger és a jelző egy képkockában.** A mérési elv feltételezi, hogy az inger és a jelző ugyanabban a képkockában jelenik meg. A program a kettőt ugyanazzal a rajzolási művelettel teszi ki ugyanarra a kettősen pufferelt felületre: a kép egy háttérpufferben készül el, és egyetlen másolással kerül a képernyőre. A rajzolás szinkron, a program közvetlenül utána készít időbélyeget. [BIZONYITAS: Annak igazolása, hogy az inger és a jelző valóban mindig ugyanabban a képkockában jelenik meg. A Windows Forms rajzolása nincs a kijelző frissítéséhez szinkronizálva, és a kép az ablakkezelő (DWM) kompozícióján keresztül jut a kijelzőre; elvileg előfordulhat, hogy a másolás két képkocka közé esik. Igazolható két fotodiódával (egy a jelzőn, egy az inger helyén) és oszcilloszkóppal: a két jel időkülönbségének sok próbán át meg kell egyeznie a két hely scanout-idejével, képkockányi ugrások nélkül.]

A program nem tudja, melyik képkockában jelenik meg az inger; ez nem hiba, mert a megjelenés tényleges idejét a fotodióda méri. A helyi (PC-s) reakcióidő kezdete viszont emiatt egy-két képkockával megelőzi az inger tényleges megjelenését.

A jelző helye a géphez tartozó beállítás: a Testreszabás Hardver fülén megnyitható teljes képernyős nézetben húzással, kattintással vagy a nyílbillentyűkkel a szenzor alá vihető. A scanout-korrekció a jelző tényleges helyével számol.

## A scanout-korrekció

A legtöbb kijelző a képet soronként, fentről lefelé rajzolja ki, ezért a különböző magasságban lévő képpontok nem egyszerre váltanak (2.6. szakasz). A fotodióda a jelző sorának kirajzolását érzékeli, a résztvevő viszont az ingerre reagál, amely más sorban van; a korrekció a két sor kirajzolása közötti idő.

!fig fig_scanout.png | 14 | A scanout-korrekció elve: az inger középső sora és a jelző középső sora közötti sorkülönbségből számított időeltérés.

A TMDK-dolgozat egyszerűsített képlete a képkockaidőt a kijelző magasságával osztotta. A végleges számítás figyelembe veszi a képjel holtidős sorait (vertical blanking), a nem egész frissítési frekvenciát és a videokártya esetleges skálázását:

!eq T_{\mathrm{kép}} = \frac{10^6}{f}\ \mathrm{\mu s}, \qquad T_{\mathrm{sor}} = \frac{T_{\mathrm{kép}}}{N_{\mathrm{összes}}}, \qquad k = \frac{N_{\mathrm{aktív}}}{H}

!eq \Delta t_{\mathrm{scanout}} = \left(y_{\mathrm{inger}} - y_{\mathrm{jelző}}\right) \cdot k \cdot T_{\mathrm{sor}}

ahol *f* a pontos frissítési frekvencia, *N*összes a képjel összes sora a holtidővel együtt, *N*aktív a látható sorok száma, *H* az asztal magassága képpontban, *y*inger és *y*jelző pedig az inger, illetve a jelző középpontjának sora a kijelző tetejétől (egész sorra kerekítve). A *k* szorzó azt adja meg, hány jelsor tartozik egy képpontsorhoz; értéke 1, ha nincs skálázás.

A bemeneteket a program a mérés elején egyszer, a Windows `QueryDisplayConfig` függvényével kérdezi le a mérést futtató kijelzőre [50] [51]. Ha a pontos időzítés nem kérdezhető le, egész frekvenciával és holtidő nélkül számol, és ezt a mérési fájlban jelzi (`ScanoutTimingExact=False`).

**Számpélda.** A fejlesztői mérésekhez használt kijelző adatai a mérési fájlok szerint: 1920 × 1080 képpont, 74,9725 Hz, 1080 látható és összesen 1119 sor. Ebből a képkockaidő 13 338,2 µs, egy sor ideje 11,92 µs. Az egyik próbában az inger középpontja a 260., a jelzőé az 1033. sorban volt; a korrekció (260 − 1033) · 11,92 µs ≈ −9214 µs, vagyis az inger sora körülbelül 9,2 ms-mal a jelző sora előtt rajzolódik ki. A mérési fájlban ehhez a próbához −9213,983 µs szerepel. Ezen a kijelzőn a korrekció a teljes képmagasságra legfeljebb körülbelül 12,9 ms; a holtidő figyelmen kívül hagyása ezt 1119/1080 arányban, azaz 3,6%-kal becsülné túl.

A korrekció az inger **középső sorára** vonatkozik. Egy 60 képpont átmérőjű inger felső és alsó széle között ezen a kijelzőn körülbelül 0,7 ms telik el; a középső sor ennek az intervallumnak a közepe.

A modell feltevései:

- a kijelző a képet soronként, fentről lefelé, állandó sorfrekvenciával, progresszív képjellel és elforgatás nélkül rajzolja ki (elforgatott vagy váltottsoros kijelzőnél a program figyelmeztetést rögzít);
- nincs bekapcsolva változó frissítési frekvencia (G-Sync, FreeSync); ezt a program nem tudja felismerni, ezért a mérőhely beállításakor kell kikapcsolni;
- a monitor belső képfeldolgozása nem változtat a sorok egymáshoz viszonyított időzítésén (a teljes bemeneti késleltetés nem számít, mert a fotodióda a ténylegesen megjelenő képet méri);
- az inger és a jelző ugyanabban a képkockában jelenik meg.

[BIZONYITAS: A scanout-modell igazolása a mérésre használt kijelzőn: két fotodiódával (vagy az inger több függőleges pozíciójában megismételt méréssel) annak kimutatása, hogy a jelző és az inger helyén mért fényjel időkülönbsége a sorkülönbséggel lineárisan nő, és a meredekség megegyezik a számított soridővel. A mérésből megadható a modell maradék hibája (a mért és a számított eltérés különbségének átlaga és szórása).]

## Soros kommunikáció az ESP-vel

A kommunikáció szöveges, soronkénti; minden üzenetet sortörés zár le, a számok tizedesponttal szerepelnek. A port beállításai: 115200 baud (állítható), 8 adatbit, paritás nélkül, 1 stopbit (8N1), áramlásvezérlés nélkül.

!table A PC és az ESP közötti üzenetek || 3.6,4.6,7.8
PC → ESP | ESP → PC | Mikor és miért
`CHECK` | `OK` | a mérés indításakor; a mérés csak `OK` válasz után indul. Legfeljebb három kísérlet, egyenként 1 s várakozással (az ESP a port megnyitásakor újraindulhat)
`VERSION` | verziószám, pl. `1.1.0` | a sikeres `CHECK` után egyszer; a PC legfeljebb 500 ms-ig vár. Ha nem jön válasz (régebbi firmware), a verzió ismeretlen marad, a mérés ettől még elindul
előjeles szám, pl. `-9259.259` | – | minden próba előtt: a scanout-korrekció µs-ban; egyben az új próba kezdete
`RTIME` | szám µs-ban (pl. `250000.000`) vagy `NA` | a résztvevő kattintása után; a PC legfeljebb 1 s-ig vár

A protokollt az `EspResponseDevice` valósítja meg; a `Serial` háttérben futó olvasóciklussal, időkorláttal párosítja a beérkező sorokat a várakozó kérésekkel, a `SerialPortConnection` a rendszer soros portját burkolja. Az `EspPortFinder` az USB-azonosítók alapján automatikusan megkeresi az ESP portját; több ilyen eszköznél a port a Testreszabásban választható ki. A program a portot háttérszálon nyitja meg, foglalt portnál legfeljebb nyolcszor próbálkozik, a megnyitás után 1,5 másodpercet vár, és kiüríti a puffereket.

A válasz nem tartalmaz próbaazonosítót: a PC az `RTIME` után érkező első elfogadható sort (`NA` vagy nemnegatív, véges szám) az éppen futó próbához rendeli, minden más sort figyelmen kívül hagy. Mivel az ESP csak kérésre válaszol, és minden korrekció új próbát kezd, korábbi próba válasza csak az 1 másodperces várakozás lejárta után, késve érkezve keveredhetne ide.

Ha az ESP `NA`-t válaszol vagy nem válaszol, a próba ESP-reakcióidő nélkül, a helyi idővel rögzül. Ha a soros kapcsolat mérés közben megszakad, a program a mérést a PC-n mért időkkel fejezi be, és a végén figyelmeztet. A firmware verziója a mérési fájl metaadatai közé kerül (`EspFirmwareVersion`); a két pontot tartalmazó verziószám nem téveszthető össze sem számmal, sem `OK` vagy `NA` válasszal.

## Szemmozgáskövetés és tekintetszabályok

A reakcióidő akkor is lehet technikailag pontos, ha a próba viselkedési szempontból érvénytelen, mert a résztvevő nem oda nézett, ahová a feladat szerint kellett. A szemkövetés ezt ellenőrzi; a Tobii eszköz nem az ESP helyett mér, és nem kerül be a reakcióidő képletébe.

### A Tobii Pro SDK használata

A program a szemkövetőt a Tobii Pro SDK .NET-változatán keresztül éri el [52]. A felhasznált funkciók:

- **eszközkeresés** (`FindAllEyeTrackers`), háttérszálon;
- **tekintetadat-folyam** (`GazeDataReceived`): mintánként a két szem tekintetpontja a kijelző síkján, normalizált (0–1) koordinátában, szemenkénti érvényességi jelzéssel és az eszköz időbélyegével;
- **kijelzőbeállítás** (`GetDisplayArea`): a kijelző mérete és helyzete a szemkövetőhöz képest;
- **mintavételi frekvencia** (`GetGazeOutputFrequency`), amely a metaadatok közé kerül;
- **rendszeridő** (`GetSystemTimeStamp`): a tekintetminták időalapja, amelyhez a program az inger kirajzolásának és a válasznak az időpontját is hozzárendeli.

Egy tekintetminta akkor érvényes, ha legalább az egyik szem érvényes, és a koordinátái véges számok. A minták a próba teljes ideje alatt rögzülnek, a döntésbe azonban csak a válaszablakba esők számítanak. A program a szem merőleges távolságát is rögzíti a kijelző síkjától (a két szem átlagát), hogy a képpontban megadott méretek utólag látószögre válthatók legyenek.

A kalibrációt a gyártó programja (Tobii Pro Eye Tracker Manager) végzi, amelyet a program el tud indítani. A kalibráció nézetben a meglévő kalibráció használata vagy újrakalibrálás választható; a program mindkét esetben ellenőrzi, hogy egy másodperc alatt érkezik-e érvényes tekintetadat. Tekintet-ellenőrzéses mérés csak megerősített kalibráció után nyitható meg.

A TMDK-dolgozat irodalomjegyzékében a szemkövető szoftveres integrációjánál a Tobii játékokhoz szánt fejlesztői csomagjára és fogyasztói eszközeire vonatkozó források szerepeltek ([45], [46]). A megvalósított program a kutatási célú Tobii Pro SDK-t használja, ezért ezek helyett a [52] és az [53] forrás az irányadó.

### A három tekintet-ellenőrzési mód

!table A tekintet-ellenőrzés módjai || 3.4,3.6,6.0,3.0
Mód | A mérés neve | A próba akkor érvényes, ha… | Szemkövető
Nincs (`None`) | Mérés tekintet-ellenőrzés nélkül | az időkorláton belül érkezett nem elsietett válasz | nem kell; ha van, az adat rögzül
Az ingert kell nézni (`LookAtStimulus`) | Normál mérés | a válaszablak **legalább egy** érvényes mintája az inger középpontjától legfeljebb d/2 + R távolságra van | kötelező
A fixációs pontot kell nézni (`KeepFixation`) | Periférikus mérés | a válaszablak **minden** érvényes mintája a fixációs ponttól legfeljebb R távolságra van | kötelező

A táblázatban d az inger átmérője, R az elfogadási rádiusz (alapérték: 80 képpont). A két mód a TMDK-dolgozat direkt és perifériás mérésének felel meg; az ellenőrzés nélküli mód új, és szemkövető nélküli mérést tesz lehetővé.

Normál módban a rádiusz az inger **szélén túl** elfogadott tekintethiba, így nem függ az inger méretétől; periférikus módban a fixációs pont körüli kör sugara. Mindkét esetben területet jelöl ki, összhangban azzal, hogy a szemkövetés pontossága területként értelmezhető [37]. Periférikus módban a próba akkor is érvénytelen, ha a résztvevő az ingerre pillantott, de a válasz pillanatára már visszanézett.

A **válaszablak** az inger kirajzolásától a PC által érzékelt első kattintásig (időtúllépésnél az időkorlát lejártáig) tart. Mivel a rajzolás hívásának visszatérésekor kezdődik, egy-két képkockával megelőzi az inger tényleges megjelenését: ez periférikus módban szigorúbbá teszi a szabályt, normál módban közömbös, mert ott egyetlen ingerre eső minta elegendő.

A beállítások ellenőrzése biztosítja a két terület elkülönülését: tekintet-ellenőrzésnél az inger legkisebb távolsága nagyobb kell legyen, mint az inger sugara és az elfogadási rádiusz összege. Periférikus módban a fixációs pont a mérőfelületen bárhová kijelölhető, ahol a szabályok engedik, ami egy irányban nagyobb ingertávolságokat tesz lehetővé.

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

A szabályok megfelelnek az irodalmi áttekintésben leírt szűrési szinteknek (2.12. szakasz). Az időalapú szűrés 100 ms-os alsó határa rögzített, a beállításfájlban nem módosítható, és a metaadatok között szerepel. Az előperiódus alatti kattintás nem válasz (az inger ettől még megjelenik, és a program megvárja a tényleges választ), de a próbát érvényteleníti.

Az alapértelmezett válaszmódban a válasz az inger megjelenése utáni első bal gombos kattintás bárhol a mérőfelületen, az egérmutató nem látszik. A másik módban az ingerre kell kattintani, az egérmutató látszik, és a mellékattintás külön okkal érvénytelen. Válasznak csak a bal gomb számít, mert az ESP is csak ennek a kapcsolóját figyeli.

## Testreszabhatóság és beállítások

A TMDK-dolgozat idején a program két rögzített mérési módot kínált. A végleges változatban egyetlen mérés van, amelynek minden lényeges paramétere állítható a Testreszabás ablak négy fülén; mellettük folyamatosan frissülő előnézet mutatja a mérési képernyő kicsinyített modelljét és az ingert valós méretben.

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

A mezők tartományán túl a program összefüggő szabályokat is ellenőriz (az inger és a háttér színe nem azonos, a legkisebb távolság nem nagyobb a legnagyobbnál, az inger nem takarja a keresztet), a képernyőtől függő feltételeket pedig a mérést futtató képernyő méretével vizsgálja. A **hibák** megakadályozzák a mentést és a mérés indítását; a **figyelmeztetések** módszertani jellegűek és nem tiltanak (például 3:1 alatti kontrasztarány az inger és a háttér között, ingerre kattintós válasz periférikus módban, tekintet-ellenőrzés gyakorló inger nélkül). Az ablak és a mérés indítása ugyanazt az ellenőrzést futtatja ugyanabból a forrásból (`SettingsLimits`).

A beállítások kézzel is szerkeszthető JSON-fájlban (`beallitasok.json`) tárolódnak; a részletes leírás a projekt dokumentációjában (docs/MERESI_BEALLITASOK.md) található. A beolvasás szigorú: ismeretlen mező, rossz típusú érték vagy kétszer szereplő mező hiba, és a mérés nem indul el, mert egy csendben alapértékre eső beállítás utólag nem derülne ki. A mentés atomikus (ideiglenes fájlon keresztül, az előző változat biztonsági másolatával), a fájl formátuma verziószámozott.

## Adattárolás

A program minden mérést két CSV-fájlba ment: az egyik a metaadatokat és próbánként egy sort, a másik az összes tekintetmintát tartalmazza. A mérési fájl elején megjegyzéssorokban szerepel a programverzió, a szemkövető típusa és mintavételi frekvenciája, a soros port, a kijelző felbontása és képidőzítése, a mérőfelület geometriája, az összes beállítás és a véletlenmag, így a fájl önmagában elegendő a mérés körülményeinek rekonstruálásához. Próbánként a fájlba kerül a hardveres (ESP) és a helyi (PC) reakcióidő, az érvényesség és az érvénytelenség oka, a sorsolt és a tényleges előperiódus, az előperiódus alatti kattintások száma, a scanout-korrekció, az inger helye és távolsága, a tekintetadatok minősége, a szem távolsága és a kattintás helye; az oszlopok részletes leírása a projekt dokumentációjában (docs/MERESI_BEALLITASOK.md) található.

A program minden próba sorát azonnal a fájl végére írja, és a lemezre üríti, így megszakítás vagy programhiba után az addigi próbák megmaradnak; a lezáró sor hiánya jelzi, hogy a mérés nem zárult le szabályosan. Új mérés nem írhat felül meglévő fájlt.

## Statisztika

A statisztika nézetben bármelyik korábbi mérés kiválasztható; a táblázat minden próbát mutat a két reakcióidővel, a különbségükkel és az érvényességgel. Az összesítés az érvényes mért próbák átlagát, mediánját és szórását adja meg a hardveres időre, a helyi időre és a kettő különbségére; a reakcióidők eloszlása jobbra ferde [59], ezért a medián a megbízhatóbb mutató. A **PC − ESP** különbség a rendszer késleltetésének próbánkénti becslése: az átlaga a mérőlánc jellemző késleltetését, a szórása annak ingadozását mutatja, a szokatlanul nagy vagy negatív érték pedig hibára utal (6.4. szakasz).

## Szálkezelés és hibakezelés

A próbák ciklusa háttérszálon fut, és minden, a nézetet érintő műveletet a felület szálára küld; az inger kirajzolása és az időbélyeg készítése egyetlen, a felület szálán futó műveletben történik. A szemkövető mintái a gyártói könyvtár saját szálán érkeznek, a soros port olvasása és a naplózás külön háttérszálon fut. A hibakezelés elve, hogy a mérés közben keletkező adat ne vesszen el, és a résztvevő elé mérés közben ne kerüljön hibaüzenet:

!table A mérés közbeni hibák kezelése || 5.0,11.0
Esemény | A program viselkedése
Az ESP egy próbában nem mért (`NA`) vagy nem válaszol | a próba a helyi idővel rögzül, a mérés folytatódik; a mérés végén a program jelzi, hány próbánál nincs hardveres idő
A soros kapcsolat megszakad | az ESP a mérés hátralévő részére lezárul, a mérés a helyi időkkel befejeződik; figyelmeztetés a mérés végén
A szemkövető kiesik tekintet-ellenőrzéses mérésben | a mérés leáll (minden további próba érvénytelen lenne), az addigi próbák mentve maradnak
A szemkövető kiesik ellenőrzés nélküli mérésben | a mérés tekintetadat nélkül folytatódik
Mentési hiba | a mérés megszakad, a már kiírt próbák megmaradnak
A résztvevő vagy a kutató megszakítja (Esc) | a mérés megszakítottként zárul le, az addigi próbák mentve maradnak
Váratlan programhiba | a naplóba kerül; a mérési fájl lezáró sor nélkül marad, ami jelzi a szabálytalan befejezést

A mérés vége után a gombok 1,5 másodpercig nem használhatók, hogy a válaszegér egy késve érkező kattintása ne indítson azonnal új mérést. A hibák részletei a naplófájlba kerülnek.
