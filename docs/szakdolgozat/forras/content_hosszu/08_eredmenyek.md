# Eredmények

A munka eredményeként létrejött egy működő reakcióidőmérő rendszer, amelyben a mérési végpontok nem a PC szoftveres eseményeihez kötődnek. Az inger kezdetét a kijelzőn megjelenő jelző fotodiódás érzékelése adja, a válasz időpontját az egér kattintásához tartozó elektromos jel ESP-s rögzítése határozza meg, a reakcióidő pedig az ESP-ben számítódik ki. A fejezet előbb a megvalósított rendszert foglalja össze, majd a rendelkezésre álló mérési adatokat közli.

## A megvalósított rendszer

!table A célkitűzések teljesülése || 6.2,9.8
Célkitűzés | Megvalósítás
Közös, PC-től független időalap a két eseményre | ESP32-S3 firmware ESP-IDF-ben; mindkét időbélyeg az `esp_timer` órájából, a reakcióidő helyben számolva
Az inger fizikai megjelenésének érzékelése | BPW34 fotodióda transzimpedancia-erősítővel; az ingerrel egy rajzolásban megjelenő jelzőnégyzet
A válasz hardveres rögzítése | az egér bal gombjának jele az ESP megszakításos bemenetén
Pozíciófüggő megjelenési eltérés kezelése | scanout-korrekció a kijelző pontos képidőzítéséből (tört frekvencia, holtidő, skálázás)
A próbák érvényességének automatikus eldöntése | hét kimenetelű, rögzített sorrendű döntés (`TrialEvaluator`)
Tekintet-ellenőrzés | Tobii Pro SDK-integráció, normál és periférikus szabály, zónaalapú elfogadás
Testreszabhatóság | Testreszabás ablak előnézettel; a korábbi két rögzített mód helyett szabadon paraméterezhető mérés
Visszaellenőrizhető adattárolás | próbánkénti CSV teljes metaadatkészlettel és véletlenmaggal, külön tekintetfájl
Tartós hardverkivitel | hordozó NYÁK, 3D nyomtatott doboz, állítható befogó a fotodiódának

A felsorolt elemek működését a fejlesztés közbeni mérések mutatják; a pontossági állítások igazolásához szükséges célzott mérések a 7. fejezetben szerepelnek.

## A szoftveres és a hardveres reakcióidő összevetése

A rendszer közvetlenül kimutatható eredménye a szoftveres időbélyegekből számolt (PC) és a hardveresen mért (ESP) reakcióidő különbsége. Erre két adatsor áll rendelkezésre: a TMDK-dolgozatban közölt mérés és a végleges hardverrel és szoftverrel, a fejlesztés közben rögzített mérések.

### A TMDK-dolgozatban közölt mérés

A TMDK-dolgozat tíz mérési párt közölt. Az első mérési oszlop a korrekcióval számított (hardveres) reakcióidőt, a második a szoftveres időbélyeg alapján kapott reakcióidőt mutatja.

!table A TMDK-dolgozatban közölt reakcióidő-adatok || 2.4,4.4,5.2,3.2
Próba | Korrigált reakcióidő (ms) | Szoftveres időbélyeg szerinti idő (ms) | Eltérés (ms)
1 | 215 | 269 | 54
2 | 250 | 288 | 38
3 | 306 | 366 | 60
4 | 259 | 298 | 39
5 | 349 | 394 | 45
6 | 237 | 288 | 51
7 | 242 | 294 | 52
8 | 217 | 264 | 47
9 | 272 | 314 | 42
10 | 201 | 244 | 43
**Átlag** | **254,8** | **301,9** | **47,1**

A korrigált reakcióidők átlaga 254,8 ms, a szoftveres időbélyeggel számított reakcióidőké 301,9 ms, a két mérési mód közötti átlagos eltérés 47,1 ms. Az eltérés minden próbában pozitív. Ez a mérés a rendszer korábbi állapotával (közvetlen bekötés, korábbi firmware és program) készült.

[HIANYZO ADAT: A TMDK-mérés körülményei: a kijelző típusa és frissítési frekvenciája, az egér típusa, a résztvevő(k) száma, a mérési mód, valamint az, hogy a tíz próba egyetlen mérésből származik-e.]

### Mérések a végleges rendszerrel

A program adatmappájában a fejlesztés során rögzített mérési fájlok találhatók. Ezek közül a 2026. november 9-i négy, egymást követő mérés készült már a végleges próbaszerkezettel (a 6.4. szakaszban leírt hiba javítása után), és mindegyikben minden mért próbához tartozik hardveres reakcióidő. A mérések közös körülményei a fájlok metaadatai szerint: programverzió 2.0.0, 1920 × 1080 képpontos kijelző 74,97 Hz-es frissítéssel (1119 sor a képjelben), pontos képidőzítés (`QueryDisplayConfig`), tekintet-ellenőrzés nélküli mód, szemkövető nélkül, mérésenként 15 mért próba.

!table A 2026. november 9-i fejlesztői mérések összesítése (érvényes mért próbák) || 2.8,1.2,2.4,2.4,2.2,2.2,2.6
Mérés kezdete | n | ESP átlag (ms) | ESP medián (ms) | PC átlag (ms) | PC − ESP átlag (ms) | PC − ESP szórás; tartomány (ms)
18:38:26 | 15 | 223,0 | 196,9 | 265,6 | 42,6 | 9,1; 32,0–59,4
18:39:56 | 15 | 207,8 | 202,5 | 249,7 | 42,0 | 8,8; 30,8–59,9
18:58:42 | 15 | 253,0 | 227,7 | 295,1 | 42,1 | 6,1; 30,6–55,0
19:00:02 | 15 | 200,2 | 190,4 | 245,8 | 45,7 | 8,2; 27,6–60,3
**Együtt** | **60** | **221,0** | – | **264,1** | **43,1** | **8,1; 27,6–60,3**

!fig fig_adatok.png | 16 | A 2026. november 9-i négy fejlesztői mérés 60 érvényes próbája: a hardveres és a szoftveres reakcióidő próbánként (balra), valamint a kettő különbségének eloszlása (jobbra).

A hatvan próbában a szoftveres reakcióidő kivétel nélkül hosszabb volt a hardveresnél; a különbség átlaga 43,1 ms, mediánja 43,5 ms, szórása 8,1 ms, tartománya 27,6–60,3 ms. A négy mérés átlagos különbsége egymáshoz közel esik (42,0–45,7 ms), ami arra utal, hogy a különbség a mérőhelyre jellemző, mérésről mérésre stabil érték.

Az adatokból három megfigyelés adódik.

**A különbség nagyságrendje megegyezik a TMDK-mérésével.** A korábbi 47,1 ms és a mostani 43,1 ms közel van egymáshoz, noha a két mérés között a hardver, a firmware és a program is megváltozott. A két érték közvetlenül nem hasonlítható össze, mert a mérőhely azonossága nem ismert, de mindkettő azt mutatja, hogy a szoftveres időbélyegzés több tíz milliszekundumos többletet tartalmaz.

**A különbség szórása nem elhanyagolható.** A 8,1 ms-os szórás és a több mint 30 ms-os tartomány azt jelenti, hogy a szoftveres mérés hibája nem egyetlen állandó értékkel javítható: ha a szoftveres időkből az átlagos különbséget levonnánk, próbánként még mindig ±10–15 ms hiba maradna. Ez az a változó késleltetés, amelyet a hardveres mérés kiküszöböl. A szórás várható forrásai a képfrissítéshez való igazodás (74,97 Hz-en a képkockaidő 13,3 ms), az inger függőleges helyétől függő kirajzolási idő és az egér jelentési ciklusa.

**A különbség összefügg az inger függőleges helyével.** A próbánkénti scanout-korrekció és a PC − ESP különbség közötti korrelációs együttható 0,50 (n = 60). A korrekció ezekben a mérésekben −11,1 és 0 ms között változott; minél lejjebb jelent meg az inger a képernyőn (minél kisebb abszolút értékű a korrekció), annál nagyobb volt a szoftveres idő többlete. Ez megfelel a soronkénti kirajzolás modelljének: a képernyő alján megjelenő inger a rajzolási parancshoz képest később válik láthatóvá, mint a tetején megjelenő.

Ezek az adatok a fejlesztés közben, a rendszer kipróbálása során keletkeztek, nem előre megtervezett vizsgálatból származnak. A reakcióidők abszolút értéke (200–250 ms körüli átlagok) az irodalomban közölt vizuális egyszerű reakcióidők tartományába esik (2.3. szakasz), de a résztvevőről és a mérés körülményeiről nincs rögzített adat, ezért a reakcióidőkből következtetés nem vonható le.

[BIZONYITAS: A PC − ESP különbség megerősítése előre megtervezett méréssel: rögzített beállítások, dokumentált mérőhely (kijelző típusa és beállításai, egér típusa és jelentési frekvenciája, számítógép), több résztvevő, résztvevőnként elegendő számú próba. Megadandó a különbség átlaga, mediánja, szórása és eloszlása, valamint a scanout-korrekcióval való összefüggése. A fenti adatok a szerző gépén talált fejlesztői mérési fájlokból származnak; a szerzőnek meg kell erősítenie, hogy a négy mérés a végleges hardverrel készült, és hogy ki volt a résztvevő.]

### További megfigyelések a fejlesztői mérésekből

A mérési fájlok a rendszer hibatűrő működését is mutatják. A 2026. november 10-i mérések egy részében egyetlen próbához sem tartozik hardveres reakcióidő: az ESP ezekben a próbákban `NA` választ adott, a mérés pedig a szándék szerint a PC-n mért időkkel végigfutott, és minden próba mentésre került. [HIANYZO ADAT: Annak oka, hogy a 2026. november 10-i, 13:06 és 13:11 között készült négy mérésben az ESP nem adott reakcióidőt (például a fotodióda nem volt a jelzőn, vagy a gombjel nem volt csatlakoztatva). A fájlokból az ok nem állapítható meg.]

A 2026. november 8-i mérések egy részében a PC − ESP különbség egyes próbákban nagy negatív érték (−465 és −2505 ms között), az ESP ideje tehát jóval hosszabb volt a PC-énél. Ez a 6.4. szakaszban leírt hiba nyoma: az ESP az előző próba lecsengő fényjelét vette az új próba kezdetének. A hibát éppen a két idő próbánkénti összevetése tette észrevehetővé, és a javítás utáni mérésekben (november 9-től) ilyen érték nem fordul elő. Az eset azt mutatja, hogy a helyi idő megtartása ellenőrző adatként akkor is hasznos, ha a mérési eredmény nem erre épül.

## A tekintet-ellenőrzés eredményei

A tekintet-ellenőrzés megvalósult, és a program mindhárom módban futtatható. A rendelkezésre álló mérési fájlok azonban szemkövető nélkül készültek, ezért a tekintet-ellenőrzés működéséről mérési adat ebben a dolgozatban nem közölhető.

[HIANYZO ADAT: Szemkövetővel készült mérések normál és periférikus módban: az érvényes és az egyes okokkal érvénytelen próbák aránya, a válaszablak tekintetadatainak minősége (érvényes minták aránya), valamint legalább egy-egy szemléltető próba tekintetpályája az inger helyével.]
