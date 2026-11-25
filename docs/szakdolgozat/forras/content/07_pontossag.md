# Mérési pontosság és a bizonyítandó állítások

A mért reakcióidő akkor tekinthető a mérőlánc technikai késleltetésétől mentesnek, ha a lánc minden elemének hibája ismert. A fejezet sorra veszi a hibaforrásokat, megadja a kiszámítható nagyságrendeket, és megjelöli a célzott méréssel igazolandó állításokat.

## A hibaforrások áttekintése

A hibák két csoportba sorolhatók (2.5. szakasz). Az **állandó eltolás** minden próbához ugyanannyit ad hozzá: az átlagot tolja el, a szórást nem növeli, és kalibrációval javítható. A **változó hiba** próbáról próbára más: a szórást növeli, és utólag nem javítható. A tervezés célja az volt, hogy a változó hibák kikerüljenek a mérőláncból, a megmaradók pedig kicsik és lehetőleg állandók legyenek.

!table A mérőlánc hibaforrásai és becsült nagyságrendjük || 4.3,2.2,3.9,5.6
Hibaforrás | Jelleg | Becsült nagyság | Alap, illetve állapot
A fotodióda válaszideje | állandó | 100 ns | adatlap [43]
A transzimpedancia-erősítő válaszideje | állandó | kb. 7 µs a jelszint feléig (τ = 10 µs) | tervezési adat; LED-del mérve a teljes lánc ≤ 16 µs
A fényjel mintavételezése (a mérő ciklus periódusa) | változó | 0–46,5 µs, legfeljebb 56 µs | mért (`DIAG`, 50 próba)
A küszöbátlépés függése a jelző felfutásától | állandó, kijelzőfüggő | a panel válaszidejének része (ms nagyságrend) | az inger hasonló átmenetet végez; színfüggése igazolandó
Az időalap (kvarc) pontossága | állandó, arányos | néhány ns 300 ms-on | adatlapi adat alapján igazolandó
A gombjel észlelése (megszakítás, ciklus) | változó | µs nagyságrend | mérendő
A jelillesztő áramkör késése | állandó | nem ismert | mérendő
A scanout-korrekció modellhibája | pozíciófüggő | a soridő pontosságától függ; a teljes korrekció legfeljebb kb. 13 ms | igazolandó
Az inger kiterjedése (nem egy sorban jelenik meg) | állandó | ±0,36 ms 60 képpontos ingernél, 75 Hz-en | számított
Az inger és a jelző nem egy képkockában jelenik meg | ritka, nagy | egy képkockaidő (pl. 13,3 ms) | igazolandó, hogy nem fordul elő
A soros átvitel és a PC ütemezése | – | nem része az eredménynek | az elvből következik
A szemkövető késleltetése | – | nem része az eredménynek | csak az érvényességi döntést érinti

## Az időmérő egység bizonytalansága

A fényjel időbélyege a küszöb első észlelésekor készül, tehát a küszöb tényleges eléréséhez képest 0 és egy ciklusidő közötti késéssel. Egyenletes eloszlást feltételezve T ciklusidő mellett az átlagos késés T/2, a szórása T/√12. A mért T = 46,5 µs-mal (4.1. szakasz) ez 23 µs átlagos késés és 13 µs szórás, legfeljebb 56 µs: két nagyságrenddel az 1 ms-os követelmény alatt.

[BIZONYITAS: Az időmérő egység végponttól végpontig mért hibája ismert időközű jelpárral: egy jelgenerátor (vagy második mikrokontroller) egy LED-et kapcsol be a fotodióda előtt, majd pontosan ismert késleltetés (pl. 100, 200, 300 ms) után alacsonyra húzza a gombbemenetet. Az ESP által mért idő és a beállított késleltetés különbségének átlaga adja a rendszeres hibát, szórása a véletlen hibát. Ez a mérés egyetlen kísérletben igazolja a K3 és a K4 követelményt, és összevontan tartalmazza az erősítő, a mintavételezés, a gombbemenet és az időalap hibáját.]

A gombjel időbélyege a megszakítási rutinban, illetve a mérő ciklusban olvasott idő, ezért a tényleges élhez képest a megszakítás kiszolgálási idejével késik; ez mikroszekundumos nagyságrendű, és az előző mérés ezt is tartalmazza. A kapcsoló pergése a válasz időpontját nem befolyásolja, mert a firmware az első alacsony szint idejét rögzíti. A gomb megnyomásának kezdete és a kapcsoló zárása közötti idő a résztvevő mozgásának része; a rendszer a zárás pillanatát tekinti a válasznak.

## A scanout-modell

A scanout-korrekció a képjel időzítéséből számolt érték, nem mért adat. Hibát az okozhat, ha a kijelző belső működése eltér a képjel időzítésétől: a panel pufferelheti a képet és saját ütemben rajzolhatja ki, a háttérvilágítás villogtathat, egyes paneltípusok (például az OLED) pedig eltérő módon frissítenek. A modell feltevéseit és az igazolásukhoz szükséges mérést a 6.6. szakasz tartalmazza.

A lehetséges hibára a korrekció nagysága ad felső becslést: a fejlesztéshez használt 75 Hz-es kijelzőn a teljes képmagasságra körülbelül 12,9 ms, 60 Hz-en körülbelül 16 ms. Ha a modell nem lenne érvényes (például a panel a teljes képet egyszerre váltaná), a hiba a próba korrekciójával lenne egyenlő, tehát az inger függőleges helyétől függene. A korrekció nélküli mérés ugyanekkora, pozíciófüggő hibát tartalmazna; a korrekció tehát akkor indokolt, ha a kijelző igazoltan soronként frissít.

A fejlesztői mérések közvetve támogatják a modellt: a PC − ESP különbség és a scanout-korrekció között pozitív összefüggés látszik (8.2. szakasz), vagyis a képernyő alsó részén megjelenő ingereknél a szoftveres idő többlete nagyobb. Ez nem helyettesíti a közvetlen, kétérzékelős mérést.

## Az inger és a jelző megjelenésének kapcsolata

A fotodióda a jelzőt látja, nem az ingert. A mérés két feltevésre épül: a kettő ugyanabban a képkockában jelenik meg, és a képpontjaik hasonló idő alatt érik el a látható fényességet. Az első feltevést a közös rajzolási művelet támasztja alá (6.5. szakasz), az igazoló mérést az ottani jelölés írja le. Ha ez a feltevés ritkán sérülne, a reakcióidőben egy képkockányi hiba keletkezne, amely a PC − ESP különbség szórása miatt nem lenne egyértelműen felismerhető; ezért kell közvetlen méréssel igazolni.

A második feltevés a színektől függ. A jelző mindig fekete → fehér átmenetet végez, az inger képpontjai a beállított háttérszínről az ingerszínre váltanak. Folyadékkristályos kijelzőn az átmenet ideje színpáronként eltér: a sötét, kis kontrasztú átmenetek jellemzően lassabbak, és a gyorsító áramkör (overdrive) is módosíthatja az időket. Az alapértelmezett beállításnál (fehér inger sötétszürke háttéren) az inger átmenete közel azonos a jelzőével; más színeknél az eltérés állandó eltolásként jelenik meg a mért reakcióidőben.

[BIZONYITAS: Az inger és a jelző átmenete közötti időkülönbség a ténylegesen használt színpárokra: második fotodióda az inger helyén, oszcilloszkóppal rögzítve mindkét jelet. A két jel időkülönbsége, a két hely scanout-idejével csökkentve megadja, mennyivel tér el az inger megjelenése a jelzőétől. Ha a különbség nem elhanyagolható, az adott színpárral mért reakcióidőket ezzel korrigálni kell. A mérési fájl az inger és a háttér színét rögzíti, így a korrekció utólag is hozzárendelhető a mérésekhez.]

A kijelző fizikai átmenete nem azonos az észleléssel. Az, hogy a résztvevő a felfutás melyik pontján észleli az ingert, a fényességtől, a kontraszttól és a látórendszertől függ, tehát az emberi reakcióidő része. A rendszer azt az időpontot tekinti az inger kezdetének, amikor a jelző fényessége eléri a küszöböt.

## A válaszoldal és a PC-s referenciaidő

A helyi (PC-s) reakcióidő nem része a mérési eredménynek, de a PC − ESP különbség értelmezéséhez ismerni kell az összetevőit. A helyi idő a rajzolás visszatérése utáni időbélyegtől az egérüzenet feldolgozásakor készülő időbélyegig tart, és a tényleges reakcióidőn felül az alábbi késleltetéseket tartalmazza:

- az ablakkezelő kompozíciója és a várakozás a következő képfrissítésre (0 és egy képkockaidő között, plusz az esetleges további pufferelt képkockák);
- a kijelző bemeneti késleltetése és a panel válaszideje;
- az inger sorának kirajzolásáig eltelő idő a képkockán belül (scanout);
- a válaszoldalon az egér jelentési ciklusa (az eszköztől függően 1–8 ms), az USB és az operációs rendszer eseménysora, valamint a program üzenetfeldolgozása.

A különbség átlaga és szórása ezen összetevőkből adódik, ezért a konkrét számítógép, kijelző és egér együttesét jellemzi, és nem általánosítható más mérőhelyre.

## A tekintet-ellenőrzés bizonytalansága

A tekintet-ellenőrzés nem időmérés, hanem besorolás: hibája az, ha érvényes próbát érvénytelennek vagy érvénytelent érvényesnek minősít. A besorolást a szemkövető pontossága, a kalibráció minősége, a mintavételi frekvencia és az elfogadási rádiusz együtt határozza meg.

Egy 60 Hz-es szemkövető körülbelül 16,7 ms-onként ad mintát, így egy 250 ms-os válaszablakba körülbelül 15 minta esik. Periférikus módban egy két minta közé eső odapillantás elvileg észrevétlen maradhat, de a szakkád és az azt követő rögzítés jellemzően több tíz, illetve több száz milliszekundumig tart, ezért az ingerre nézés több mintán is megjelenik. Normál módban a szabály megengedő (egyetlen minta elég), így ott a téves érvénytelenítés a valószínűbb: ha a résztvevő reakcióideje rövidebb annál, mint amennyi idő alatt a tekintete az ingerhez ér, a próba érvénytelen lesz, noha szabályosan járt el.

[BIZONYITAS: A tekintet-ellenőrzés működésének igazolása szándékos szabályszegéssel és szabálykövetéssel: periférikus módban olyan próbák, amelyekben a résztvevő utasításra az ingerre néz (ezeknek érvénytelennek kell lenniük), és olyanok, amelyekben végig a keresztet nézi (ezeknek érvényesnek kell lenniük); normál módban fordítva. A két csoport helyes besorolásának aránya jellemzi a szabályok megbízhatóságát az alapértelmezett beállításokkal. A rendelkezésre álló mérési fájlok között szemkövetővel készült mérés nincs.]

## Összefoglaló: a validációs mérések terve

A rendszer pontosságára vonatkozó állítások az alábbi mérésekkel támaszthatók alá; a táblázat a szükséges eszközöket is megadja.

!table A javasolt validációs mérések || 5.0,5.6,5.4
Mérés | Eszközök | Mit igazol?
Az erősítő felfutási ideje | oszcilloszkóp, négyszögjellel hajtott LED | a 10 µs-os időállandót (felső korlát már mérve: ≤ 16 µs)
Ismert időközű jelpár mérése | jelgenerátor vagy második mikrokontroller, LED | az időmérő egység teljes hibáját (K3, K4)
Sötét és világos jelszint, küszöbtartalék | a firmware `ADC` és `DIAG` parancsa | a 475-ös küszöb megbízhatóságát, a környezeti fény hatását
Gombjel a kapcsolón és a bemeneten | kétcsatornás oszcilloszkóp | az illesztő áramkör késését, a pergés idejét
Két fotodióda a képernyő két magasságában | kétcsatornás oszcilloszkóp | a scanout-modellt és az egy képkockában megjelenést
Második fotodióda az inger helyén, több színpárral | kétcsatornás oszcilloszkóp | az inger és a jelző átmenetének eltérését
Mérés több résztvevővel, rögzített beállításokkal | a teljes rendszer | a PC − ESP különbség nagyságát és szórását
Szándékos szabályszegés a két tekintetmódban | a teljes rendszer szemkövetővel | a tekintetszabályok helyes besorolását
