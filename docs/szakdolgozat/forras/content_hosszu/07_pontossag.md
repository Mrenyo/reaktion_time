# Mérési pontosság és a bizonyítandó állítások

A rendszer célja az, hogy a mért reakcióidő a mérőlánc technikai késleltetésétől mentes legyen. Ez az állítás csak akkor megalapozott, ha a mérőlánc minden elemének saját hibája ismert. Ez a fejezet sorra veszi a hibaforrásokat, megadja a nagyságrendjüket ott, ahol az a rendelkezésre álló adatokból kiszámítható, és megjelöli azokat, amelyeket célzott méréssel kell igazolni. A fejezet tehát részben elemzés, részben mérési terv.

## A hibaforrások áttekintése

A hibák két csoportba sorolhatók (2.5. szakasz). Az **állandó eltolás** minden próbához ugyanannyit ad hozzá; ez az átlagot tolja el, a szórást nem növeli, és kalibrációval javítható. A **változó hiba** próbáról próbára más; ez a szórást növeli, és nem javítható utólag. A tervezés célja az volt, hogy a változó hibák a mérőláncból kikerüljenek, a megmaradó hibák pedig kicsik és lehetőleg állandók legyenek.

!table A mérőlánc hibaforrásai és becsült nagyságrendjük || 4.3,2.2,3.9,5.6
Hibaforrás | Jelleg | Becsült nagyság | Alap, illetve állapot
A fotodióda válaszideje | állandó | 100 ns | adatlap [43]
A transzimpedancia-erősítő válaszideje | állandó | kb. 7 µs a jelszint feléig (τ = 10 µs) | tervezési adat; igazolandó
A fényjel mintavételezése (a mérő ciklus periódusa) | változó | 0 és egy ciklusidő között | a firmware `DIAG` parancsával mérendő
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

A fényjel időbélyege a küszöb első észlelésekor készül. A küszöb tényleges elérése és az észlelés között legfeljebb egy ciklusnyi idő telik el, ezért a fényjel időbélyege 0 és egy ciklusidő közötti késéssel készül. Ha a késés ebben a tartományban egyenletes eloszlású, akkor T ciklusidő mellett az átlagos késés T/2, a szórása T/√12. Például 40 µs-os ciklusidőnél ez 20 µs átlagos késést és 11,5 µs szórást jelentene, ami két nagyságrenddel az 1 ms-os követelmény alatt van. A ciklusidő tényleges értéke a firmware `DIAG` parancsával olvasható ki; amíg ez a mérőhelyen nincs rögzítve, a számpélda csak a nagyságrendet szemlélteti.

[BIZONYITAS: Az időmérő egység végponttól végpontig mért hibája ismert időközű jelpárral: egy jelgenerátor (vagy második mikrokontroller) egy LED-et kapcsol be a fotodióda előtt, majd pontosan ismert késleltetés (pl. 100, 200, 300 ms) után alacsonyra húzza a gombbemenetet. Az ESP által mért idő és a beállított késleltetés különbségének átlaga adja a rendszeres hibát, szórása a véletlen hibát. Ez a mérés egyetlen kísérletben igazolja a K3 és a K4 követelményt, és összevontan tartalmazza az erősítő, a mintavételezés, a gombbemenet és az időalap hibáját.]

A gombjel időbélyegét a megszakítás és a mérő ciklus közösen adja. Az időbélyeg a megszakítási rutinban, illetve a ciklusban olvasott idő, tehát a tényleges élhez képest a megszakítás kiszolgálási idejével késik. Ez mikroszekundumos nagyságrendű; az előző mérés ezt is tartalmazza.

A mechanikus kapcsoló pergése a válasz időpontját nem befolyásolja, mert a firmware az első alacsony szint idejét rögzíti. A kapcsoló működéséből adódó késés – a gomb megnyomásának kezdete és a kapcsoló zárása közötti idő – a résztvevő mozgásának része, és minden gombnyomásos mérésben jelen van; a rendszer a zárás pillanatát tekinti a válasznak.

## A scanout-modell

A scanout-korrekció a kijelző képjelének időzítéséből számolt érték, nem mért adat. A hibája abból adódhat, hogy a kijelző belső működése eltér a képjel időzítésétől: a panel a beérkező képet pufferelheti és saját ütemben rajzolhatja ki, a háttérvilágítás villogtathat, egyes paneltípusok (például az OLED) pedig eltérő módon frissítenek. A modell feltevéseit a 6.6. szakasz sorolja fel, az igazolásukhoz szükséges mérést az ottani jelölés írja le.

A korrekció nagysága ad felső becslést a lehetséges hibára: a fejlesztéshez használt 75 Hz-es kijelzőn a teljes képmagasságra körülbelül 12,9 ms, 60 Hz-en körülbelül 16 ms. Ha a modell egyáltalán nem lenne érvényes (például a panel a teljes képet egyszerre váltaná), a hiba a próba korrekciójával lenne egyenlő, tehát az inger függőleges helyétől függene. A korrekció nélküli mérés ugyanekkora, pozíciófüggő hibát tartalmazna; a korrekció alkalmazása tehát akkor indokolt, ha a kijelző igazoltan soronként frissít.

A fejlesztői mérések közvetett támogatást adnak a modellhez: a PC − ESP különbség és a scanout-korrekció között pozitív összefüggés látszik (8.2. szakasz), vagyis a képernyő alsó részén megjelenő ingereknél a szoftveres idő többlete nagyobb, ahogy a soronkénti kirajzolásból várható. Ez azonban nem helyettesíti a közvetlen, kétérzékelős mérést.

## Az inger és a jelző megjelenésének kapcsolata

A fotodióda a jelzőt látja, nem az ingert. A mérés két feltevésre épül: a kettő ugyanabban a képkockában jelenik meg, és a képpontjaik hasonló idő alatt érik el a látható fényességet.

Az első feltevést a közös rajzolási művelet támasztja alá (6.5. szakasz); az igazolásához szükséges mérést az ottani jelölés írja le. Ha a feltevés ritkán sérülne, az a PC − ESP különbségben nem látszana, a reakcióidőben viszont egy képkockányi hibát okozna, ezért ez a rendszer egyik legfontosabb ellenőrzendő tulajdonsága.

A második feltevés a színektől függ. A jelző mindig fekete → fehér átmenetet végez. Az inger képpontjai a beállított háttérszínről a beállított ingerszínre váltanak, és egy folyadékkristályos kijelzőn az átmenet ideje színpáronként eltér: a sötét, kis kontrasztú átmenetek jellemzően lassabbak, a kijelző gyorsító áramköre (overdrive) pedig tovább módosíthatja az időket. Az alapértelmezett beállításnál (fehér inger sötétszürke háttéren) az inger átmenete közel azonos a jelzőével. Ettől eltérő színeknél az inger a jelzőhöz képest később vagy korábban érheti el a látható fényességet, és ez a különbség állandó eltolásként jelenik meg a mért reakcióidőben.

[BIZONYITAS: Az inger és a jelző átmenete közötti időkülönbség a ténylegesen használt színpárokra: második fotodióda az inger helyén, oszcilloszkóppal rögzítve mindkét jelet. A két jel időkülönbsége, a két hely scanout-idejével csökkentve megadja, mennyivel tér el az inger megjelenése a jelzőétől. Ha a különbség nem elhanyagolható, az adott színpárral mért reakcióidőket ezzel korrigálni kell. A mérési fájl az inger és a háttér színét rögzíti, így a korrekció utólag is hozzárendelhető a mérésekhez.]

Meg kell különböztetni a kijelző fizikai átmenetét az észleléstől. Hogy a résztvevő az inger felfutásának melyik pontján észleli az ingert, az az inger fényességétől, kontrasztjától és a látórendszertől függ; ez már az emberi reakcióidő része, nem a mérőlánc hibája. A rendszer azt az időpontot tekinti az inger kezdetének, amikor a jelző fényessége eléri a küszöböt.

## A válaszoldal és a PC-s referenciaidő

A helyi (PC-s) reakcióidő nem része a mérési eredménynek, de a PC − ESP különbség értelmezéséhez ismerni kell az összetevőit. A helyi idő kezdete a rajzolás visszatérése utáni időbélyeg, a vége az egérüzenet feldolgozásakor készülő időbélyeg. A kettő között a tényleges reakcióidőn felül az alábbi késleltetések vannak:

- az ablakkezelő kompozíciója és a várakozás a következő képfrissítésre (0 és egy képkockaidő között, plusz az esetleges további pufferelt képkockák);
- a kijelző bemeneti késleltetése és a panel válaszideje;
- az inger sorának kirajzolásáig eltelő idő a képkockán belül (scanout);
- a válaszoldalon az egér jelentési ciklusa (az eszköztől függően 1–8 ms), az USB és az operációs rendszer eseménysora, valamint a program üzenetfeldolgozása.

A különbség átlaga ezek összegének átlaga, a szórása pedig az összetevők szórásából adódik. A különbség ezért a konkrét számítógép, kijelző és egér együttesét jellemzi, és nem általánosítható más mérőhelyre.

## A tekintet-ellenőrzés bizonytalansága

A tekintet-ellenőrzés nem időmérés, hanem besorolás: a próba érvényes vagy érvénytelen. A hibája kétféle lehet: érvényes próbát érvénytelennek minősít, vagy érvénytelent érvényesnek. A besorolást a szemkövető pontossága, a kalibráció minősége, a mintavételi frekvencia és az elfogadási rádiusz együtt határozza meg.

Egy 60 Hz-es szemkövető körülbelül 16,7 ms-onként ad mintát, így egy 250 ms-os válaszablakba körülbelül 15 minta esik. Periférikus módban egy rövid, két minta közé eső odapillantás elvileg észrevétlen maradhat; a szemmozgás (szakkád) és az azt követő rögzítés azonban jellemzően több tíz, illetve több száz milliszekundumig tart, ezért az ingerre nézés a gyakorlatban több mintán is megjelenik. Normál módban a szabály megengedő (egyetlen minta elég), ezért ott a téves érvénytelenítés a valószínűbb hiba: ha a résztvevő reakcióideje rövidebb annál, mint amennyi idő alatt a tekintete az ingerhez ér, a próba érvénytelen lesz, noha a résztvevő szabályosan járt el.

[BIZONYITAS: A tekintet-ellenőrzés működésének igazolása szándékos szabályszegéssel és szabálykövetéssel: periférikus módban olyan próbák, amelyekben a résztvevő utasításra az ingerre néz (ezeknek érvénytelennek kell lenniük), és olyanok, amelyekben végig a keresztet nézi (ezeknek érvényesnek kell lenniük); normál módban fordítva. A két csoport helyes besorolásának aránya jellemzi a szabályok megbízhatóságát az alapértelmezett beállításokkal. A rendelkezésre álló mérési fájlok között szemkövetővel készült mérés nincs.]

## Összefoglaló: a validációs mérések terve

A fenti elemzés alapján a rendszer pontosságára vonatkozó állítások az alábbi mérésekkel támaszthatók alá. A táblázat a mérésekhez szükséges eszközöket is megadja; a sorszámozott jelölések teljes listája a D mellékletben található.

!table A javasolt validációs mérések || 5.0,5.6,5.4
Mérés | Eszközök | Mit igazol?
Az érzékelőlánc felfutási ideje | oszcilloszkóp, négyszögjellel hajtott LED | az erősítő 10 µs-os időállandóját
A mérő ciklus periódusideje | a firmware `DIAG` parancsa soros terminálból | a fényjel időbélyegének felbontását
Ismert időközű jelpár mérése | jelgenerátor vagy második mikrokontroller, LED | az időmérő egység teljes hibáját (K3, K4)
Sötét és világos jelszint, küszöbtartalék | a firmware `ADC` és `DIAG` parancsa | a 475-ös küszöb megbízhatóságát, a környezeti fény hatását
Gombjel a kapcsolón és a bemeneten | kétcsatornás oszcilloszkóp | az illesztő áramkör késését, a pergés idejét
Két fotodióda a képernyő két magasságában | kétcsatornás oszcilloszkóp | a scanout-modellt és az egy képkockában megjelenést
Második fotodióda az inger helyén, több színpárral | kétcsatornás oszcilloszkóp | az inger és a jelző átmenetének eltérését
Mérés több résztvevővel, rögzített beállításokkal | a teljes rendszer | a PC − ESP különbség nagyságát és szórását
Szándékos szabályszegés a két tekintetmódban | a teljes rendszer szemkövetővel | a tekintetszabályok helyes besorolását
