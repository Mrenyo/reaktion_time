# Hardver

A mérőhardver négy részből áll: az ESP32-S3 mikrokontrollerből, a fotodióda áramköréből, az egérgomb jelének illesztéséből és a mechanikai elemekből (ház és befogó). Az elektronika saját tervezésű nyomtatott áramkörre került.

## A központi időmérő egység: ESP32-S3 N16R8

A reakcióidőt külön időmérő egység számítja ki még az adatküldés előtt, így az adatátvitel, az operációs rendszer ütemezése és a szoftveres eseményfeldolgozás változékonysága nem kerül az eredménybe.

Az egység ESP32-S3 alapú fejlesztőlap [41] N16R8 jelű modullal (16 MB flash, 8 MB PSRAM [47]); a TMDK-dolgozatban még általánosan ESP32-S3 fejlesztőpanel szerepelt. A firmware PSRAM-ot nem igényel; a választást a lap elérhetősége indokolta, a nagyobb memória a mérést nem befolyásolja.

!fig foto_esp32s3.png | 5 | ESP32-S3 fejlesztőlap (a kép a TMDK-dolgozatból származik).

A lapról kiolvasott adatok (`esptool`): ESP32-S3 (QFN56, v0.2-es revízió) 8 MB tokba épített PSRAM-mal, 40 MHz-es kvarc, 16 MB flash. Az USB–soros átalakító WCH CH343 (USB-azonosító 1A86:55D3); az eredeti ESP32-S3-DevKitC-1 CP2102N-t használ, így a lap azzal lábkompatibilis utángyártott változat. [HIANYZO ADAT: A fejlesztőlap gyártója (a lap feliratáról).]

!table Az ESP32-S3 N16R8 mérés szempontjából lényeges jellemzői || 4.6,5.2,6.2
Jellemző | Érték | Szerepe a mérésben
Processzor | kétmagos, 32 bites Xtensa LX7; a projektben a legnagyobb, 240 MHz-es órajelen [39] | az egyik mag kizárólag a mérést futtatja, a másik a soros kommunikációt
Memória | 16 MB flash, 8 MB PSRAM | nem korlátozó; a firmware néhány száz kB
Időzítő | `esp_timer`, 1 µs felbontás, 64 bites számláló [42] | a két esemény közös időalapja
A/D átalakító | 12 bites SAR ADC; ADC1 3-as csatorna = GPIO4 [39] | a fotodióda-erősítő kimenetének mintavételezése
Digitális bemenet | GPIO5, belső felhúzó ellenállás, élvezérelt megszakítás [40] | a válaszgomb jele
Soros kapcsolat | UART0 (115200 baud), WCH CH343 USB–soros átalakító | parancsok és eredmény a PC felé
Tápellátás | USB-ről, 5 V; a lapon 3,3 V-os stabilizátor | külön tápegység nem kell

Az A/D átalakító adatlap szerinti legnagyobb mintavételi sebessége (100 kSPS [39]) elvi felső korlát: a firmware egyenkénti (oneshot) módban, programból olvassa, ezért a tényleges mintavételi periódust a mérő ciklus futási ideje adja. Ezt a firmware maga méri (`DIAG`, 5.7. szakasz). Az 1.1.0-s firmware-rel 50, egyenként 2 s-os próbában (3,24 millió ciklus) a periódusidő átlaga 46,5 µs volt (próbánként 46,51–46,55 µs), a legnagyobb 56 µs. A fényjel időbélyegének felbontása tehát kb. 47 µs, a legrosszabb esetben 56 µs.

Az N16R8 modul az oktális PSRAM miatt a GPIO35–37 lábakat belsőleg használja; a mérőáramkör ezeket nem használja. A fotodióda bemenete az ADC1 egység csatornája, mert az ADC2 a vezeték nélküli funkciókkal osztozik. A firmware vezeték nélküli kapcsolatot nem használ, így rádiós tevékenység nem zavarja az időzítést.

## Kijelzőoldali esemény: a fotodiódás érzékelő

A rajzolási parancs időpontjából nem következik, hogy a kijelző adott pontján mikor változik a fény, ezért az inger kezdetét fizikai fényváltozásként kell mérni. [2] [24]

Az ingerrel egy időben a kijelző előre meghatározott területén fehér négyzet (a fotodióda-jelző) jelenik meg. A fotodióda ezt a fekete → fehér átmenetet érzékeli, az ESP pedig időbélyegzi. Így a rendszer nem átlagos kijelzőkésleltetést von le utólag, hanem minden próbában fizikai kijelzőoldali kezdőjellel rendelkezik.

### A BPW34 választásának indoklása

A fényérzékelő BPW34 típusú szilícium PIN fotodióda. A választás szempontja az volt, hogy a válaszideje ne legyen a mérés szűk keresztmetszete, és a kijelző fényére elegendő jelet adjon.

!fig foto_bpw34.png | 4.5 | BPW34 PIN fotodióda (a kép a TMDK-dolgozatból származik).

!table A BPW34 fotodióda adatlapi jellemzői és jelentőségük [43] [52] || 4.4,4.6,7.0
Jellemző | Adatlapi érték | Jelentősége
Spektrális érzékenység (λ0,1) | 430–1100 nm | lefedi a kijelző fehér fényének látható összetevőit
Sugárzásérzékeny felület | 7,5 mm² | nem igényel képpontnyi pontosságú illesztést, a jelzőnégyzet fényét átlagolja
Fényáram | 50 µA; a szerző jegyzete szerint 1 klx megvilágításnál (A szabványfényforrás, VR = 5 V) – a mérési feltétel az adatlapon ellenőrizendő | a kijelző megvilágításánál tíz mikroamperes nagyságrendű, jól erősíthető jel
Fel- és lefutási idő | 100 ns (VR = 10 V, RL = 1 kΩ, λ = 820 nm) | nagyságrendekkel gyorsabb a kijelző 1–10 ms-os átkapcsolásánál
Fél érzékenységi szög | ±65° | a közvetlenül a panelre helyezett dióda a jelző teljes felületéről gyűjt fényt
Tokozás | furatszerelt, átlátszó műanyag | egyszerűen beépíthető, nincs beépített szűrő

A 100 ns-os adatlapi fel- és lefutási idő záróirányú előfeszítés és kis terhelő ellenállás mellett érvényes; a tényleges áramkörben a válaszidőt az erősítő határozza meg.

A kijelző megvilágítása a jelző felületén a szerző becslése szerint 900 lx körüli, ami az adatlapi érzékenységgel körülbelül 45 µA fényáramot jelentene. Ez nagyságrendi becslés, mert az adatlapi érték A szabványfényforrásra vonatkozik, a kijelző háttérvilágítása pedig eltérő spektrumú. [BIZONYITAS: A kijelző megvilágítása a jelzőnégyzet felületén (fénymérővel, fehér és fekete jelzőnél), illetve a fotodióda ebből adódó fényárama vagy az erősítő kimeneti feszültsége a két állapotban.]

### A transzimpedancia-erősítő

A TMDK-dolgozatban a fotodióda jele közvetlenül az ESP bemenetére került; ilyenkor a dióda kapacitása és a nagy értékű terhelő ellenállás lassítja a jelet, a jelszint pedig a terheléstől függ. A végleges változatban a diódát transzimpedancia-erősítő (áram–feszültség átalakító) követi, amely a diódát közel nulla feszültségen tartja (a műveleti erősítő virtuális földpontján), így a kapacitása nem töltődik át, a kimeneti feszültség pedig a fényárammal arányos. [53]

!fig fig_kapcsolas.png | 16 | A két illesztő áramkör elvi rajza. A rajz a működési elvet mutatja, nem a megépített kapcsolás pontos mása.

Az erősítő kimeneti feszültsége és időállandója:

!eq U_{\mathrm{ki}} = I_{\mathrm{fotó}} \cdot R_f \qquad\qquad \tau = R_f \cdot C_f

ahol Rf a visszacsatoló ellenállás, Cf a vele párhuzamos kondenzátor, amely a stabilitást biztosítja, a zajt korlátozza, és meghatározza az érzékelőlánc válaszidejét. A szerző tervezési adata szerint az időállandó 10 µs; ez minden próbában azonos, állandó eltolás, két nagyságrenddel az 1 ms-os követelmény alatt. Egy 10 µs időállandójú elsőrendű tag a végértékének felét körülbelül 7 µs alatt éri el, az erősítő tehát a küszöbátlépést legfeljebb ennyivel késlelteti.

[HIANYZO ADAT: A fotodióda-erősítő kapcsolási rajza és alkatrészértékei: a műveleti erősítő típusa (a NYÁK-on U2, 8 lábú DIP tok), a visszacsatoló ellenállás és kondenzátor értéke (a 10 µs időállandót adó Rf és Cf), a tápfeszültség, valamint az, hogy a dióda előfeszítés nélkül (fotovoltaikus üzemben) vagy záróirányú előfeszítéssel működik-e. A NYÁK-rajzon R4, R5, C1 és C2 szerepel; ezek szerepe a kapcsolási rajz nélkül nem állapítható meg egyértelműen.]

Az érzékelőlánc válaszidejére felső korlátot ad egy LED-es mérés (a repó `led_fotodioda_meres/` mappája). Az ESP egy LED-et kapcsol be a fotodióda előtt, és méri, mennyi idő múlva éri el az erősítő kimenete a 150-es nyers ADC-küszöböt; az ADC-t ehhez közvetlenül a regiszterből olvassa (egy olvasás 6,4 µs). Két, egyenként 500 mérésből álló sorozatban 996 mérés adott 15 µs-ot; a kivételek 13 µs (kétszer, a sorozat elején), 8 µs és 16 µs (egyszer-egyszer). A LED, a fotodióda, az erősítő és a mintavétel együttes késése tehát jellemzően 15 µs, legfeljebb 16 µs, 6,4 µs-os felbontással. [BIZONYITAS: Az erősítő 10–90%-os felfutási ideje és időállandója oszcilloszkóppal, a 10 µs-os tervezési érték pontos igazolására.]

Az erősítő kimenete az ESP GPIO4 lábára (ADC1, 3-as csatorna) csatlakozik. A firmware az átalakítót 0 dB csillapítással használja, amelynél a mérési tartomány körülbelül 0–0,95 V [45]. A firmware-ben rögzített megfigyelés szerint a jelző fekete állapotában az átalakító 0 körüli, fehér állapotában 950 körüli nyers értéket ad (a 0–4095 tartományból), a küszöb ennek a fele, 475. [BIZONYITAS: A fekete és a fehér jelzőhöz tartozó ADC-értékek eloszlása (átlag, szórás, legkisebb és legnagyobb érték) a mérésre használt kijelzőn és annak szokásos fényerő-beállításánál; ebből a 475-ös küszöb zajtartaléka mindkét irányban. A szintek a firmware `ADC` parancsával olvashatók ki (fehér jelzőnél az áthelyező nézetben, fekete jelzőnél a mérési nézet előterében). Érdemes megadni azt is, mekkora fényerő-csökkentésig működik megbízhatóan a detektálás.]

A küszöbátlépés időpontja a kijelző képpontjainak felfutásától is függ: a firmware azt a pillanatot rögzíti, amikor a jelző fényessége eléri a végérték körülbelül felét. Ez a késés a panel válaszidejének része, és minden próbában hasonló. Alapértelmezett (fehér inger sötét háttéren) beállításnál a jelző és az inger felfutása közel azonos; eltérő színeknél a két átmenet ideje különbözhet (7.4. szakasz).

## Inputoldali esemény: az egérgomb jelének illesztése

A PC által fogadott egéresemény tartalmazza az egér belső feldolgozását, a jelentési ciklust, az operációs rendszer eseménysorát és az alkalmazás eseménykezelését. A mérés ezért az egér bal gombjának mikrokapcsolójáról az ESP-re kivezetett elektromos jelet veszi alapul. Az egér a PC felé továbbra is szabályosan működik: a program ebből érzékeli a kattintást, és ezzel zárja le a próbát, a reakcióidő számítása azonban nem erre épül.

A firmware a jelet a GPIO5 lábon, aktív alacsony logikával várja: nyugalomban a belső felhúzó ellenállás magas szinten tartja a bemenetet, lenyomáskor a jel alacsonyra vált. A bemenet a lefutó élre megszakítást kér, a mérő ciklus pedig folyamatosan is olvassa a szintet (5.5. szakasz). A TMDK-dolgozat idején a kapcsoló jele közvetlenül csatlakozott a mikrokontrollerre; a végleges változatban illesztő áramkör van a kettő között.

[HIANYZO ADAT: Az egérgomb jelillesztő áramkörének kapcsolási rajza és indoklása: honnan van kivezetve a jel (a mikrokapcsoló melyik kivezetése), milyen feszültségszintű az egér áramköre (5 V vagy 3,3 V), mi végzi a szintillesztést vagy leválasztást, és a NYÁK mely alkatrészei tartoznak ehhez a fokozathoz. Megadandó a felhasznált egér típusa is, és az, hogy a kivezetés hogyan csatlakozik a dobozhoz.]

[BIZONYITAS: Annak igazolása, hogy a jelkivezetés nem befolyásolja az egér működését, és hogy az ESP bemenetén mért lefutó él késése a mikrokapcsoló tényleges zárásához képest elhanyagolható (kétcsatornás oszcilloszkópos mérés a kapcsoló kivezetésén és a GPIO5 lábon). Ugyanebből a mérésből megadható a kapcsoló pergésének jellemző időtartama, amely a firmware 30 ms-os tartási idejét indokolja.]

## A nyomtatott áramkör

Az elektronika egyetlen, L alakú nyomtatott áramkörre került: a nagyobb rész a fejlesztőlap hordozója, a kisebb, jobb alsó rész az érzékelő-illesztés alkatrészeit tartalmazza.

!fig fig_pcb.png | 9.5 | A nyomtatott áramkör beültetési rajza (felső szitaréteg).

!table A nyomtatott áramkör rajzon azonosítható elemei || 2.2,5.6,8.2
Jelölés | Elem | Megjegyzés
U1 | 2 × 22 lábas foglalat az ESP32-S3 fejlesztőlapnak | az USB-csatlakozó a lap alsó éle felé néz, túlnyúlik a NYÁK szélén
U2 | 8 lábú DIP tokozású integrált áramkör | feltehetően a fotodióda-erősítő műveleti erősítője (típusa nem ismert)
R4, R5 | két furatszerelt ellenállás | szerepük a kapcsolási rajzból állapítható meg
C1, C2 | két kondenzátor | visszacsatoló, illetve tápszűrő szerep valószínű
H2 | háromlábú tüskesor | külső csatlakozás (rendeltetése a kapcsolási rajzból állapítható meg)
– | 2 × 2-es tüskesor a lap felső részén | külső csatlakozás (rendeltetése nem ismert)
– | négy rögzítőfurat a hordozó rész sarkainál | a doboz rögzítéséhez
– | furatsor a két rész határán | törésvonal vagy a két rész elválasztását jelző perforáció

A hordozó NYÁK a próbapanelhez és a szabadon vezetékelt bekötéshez képest mechanikailag stabil és ismételhető: az alkatrészek helye rögzített, a vezetékek rövidek, a nagy impedanciájú fotodióda-bemenet pedig kevésbé érzékeny a szórt zavarokra.

[HIANYZO ADAT: A NYÁK kapcsolási rajza és huzalozási terve (rétegszám, a fotodióda és a gombjel csatlakozóinak lábkiosztása, a H2 és a 2 × 2-es tüskesor rendeltetése, a furatsor szerepe), az alkatrészlista értékekkel, valamint a tervezőprogram és a gyártás módja. A rendelkezésre álló rajz csak a szitaréteget tartalmazza, méretek nélkül.]

## Mechanikai kialakítás: 3D nyomtatott elemek

A megismételhető méréshez a fotodiódának mindig a jelzőnégyzet közepére kell kerülnie, a környezeti fénytől árnyékolva, az elektronikának pedig védett házban kell lennie. Ezt nyolc, 3D nyomtatásra tervezett alkatrész biztosítja; a modellek a véglegesen használt változatok.

!table A 3D nyomtatott alkatrészek és befoglaló méreteik (a modellekből számítva) || 5.4,3.4,2.0,5.2
Fájl | Befoglaló méret (mm) | Térfogat (cm³) | Rendeltetés
`35-60mm_Hook_U_BPW_Box_V13.stl` | 81,0 × 51,0 × 85,5 | 46,4 | U alakú befogókengyel az érzékelő házával és árnyékoló peremmel
`BPW_Head_V2_Final.stl` | 23,0 × 32,0 × 6,0 | 1,5 | érzékelőfej: maszklemez a fotodióda nyílásával
`Screw_35-60mm_plus_10mm.stl` | 25,0 × 25,0 × 71,0 | 12,4 | recézett fejű szorítócsavar gömbvéggel
`Cap.stl` | 25,0 × 25,0 × 9,5 | 2,5 | nyomótalp, amely a csavar gömbvégére pattan
`esp_doboz_v6.stl` | 57,9 × 87,1 × 34,4 | 37,7 | az elektronika dobozának alja
`ESP_doboz_teto_v6.stl` | 57,9 × 83,1 × 3,0 | 14,3 | a doboz fedele négy csavarfurattal
`esp_doboz_usb_huvely.stl` | 18,1 × 12,6 × 20,9 | 1,1 | hüvely az USB-csatlakozó nyílásához
`zaroklip_v4.stl` | 8,5 × 24,4 × 1,2 | 0,1 | záróklip a csatlakozó rögzítéséhez

### A fotodióda befogója

A befogó a modellek alakja alapján satuszerűen fogja közre a kijelzőt. Az U alakú kengyel egyik szárán van az érzékelő háza (egy nyitott doboz) és egy vékony, széles perem, amely a kijelző előlapjára fekszik fel, és a jelzőnégyzet környékét árnyékolja a környezeti fénytől. A másik szár menetes furatába csavarható recézett fejű szorítócsavar a kijelző hátlapjának feszül; a gömbvégére pattanó nyomótalp ferde vagy ívelt hátlaphoz is felfekszik, és a szorítóerőt nagyobb felületen osztja el. A fájlnevek alapján a befogó 35–60 mm közötti vastagságú kijelzőperemhez készült, a csavar 10 mm ráhagyással.

!fig fig_3d_szenzor.png | 16 | A fotodióda befogójának alkatrészei (a modellekből készített képek).

Az érzékelőfej lemezének kör alakú nyílása maszk, amely csak a jelzőnégyzet fényét engedi a fotodiódára. A program a jelzőnégyzet körül 16 képpontos fekete védősávot rajzol, így világos háttérnél sem jut szórt fény a nyílás környékére (6.5. szakasz). A felhelyezést a program áthelyező nézete célzóvonalakkal segíti, amelyeken a jelző közepétől 2,5 cm-re keresztjelek vannak.

[HIANYZO ADAT: A befogó összeszerelési rajza vagy fényképe a kijelzőre szerelt állapotban, a maszknyílás átmérője, a fotodióda és a nyílás távolsága, valamint az, hogy az érzékelő házába kerül-e elektronika (pl. az erősítő) vagy csak a dióda. Megadandó a kábel típusa és hossza az érzékelő és a doboz között, mert a nagy impedanciájú jelnél ez a zavarérzékenységet befolyásolja.]

[BIZONYITAS: Az árnyékolás hatásosságának igazolása: a fotodióda-jel szintje fekete jelzőnél sötét szobában és erős szobai megvilágítás mellett. Az állítás az, hogy a környezeti fény nem közelíti meg a küszöböt.]

### Az elektronika doboza

A modellek alakja alapján a doboz alja téglatest alakú tálca, a négy sarkában csavarfuratokkal a fedél rögzítéséhez, a fenekén távtartóval a NYÁK számára. Az egyik rövid oldal lekerekített nyílásába külön nyomtatott hüvely illeszkedik az USB-csatlakozónak; a szemközti oldal közelében bordázott kivágások vannak, feltehetően a kábelek kivezetésére. Az E alakú záróklip ezekbe illeszkedik, és a csatlakozót, illetve a kábelt rögzíti. A fedél sík lemez négy furattal.

!fig fig_3d_doboz.png | 16 | Az elektronika dobozának alkatrészei (a modellekből készített képek).

[HIANYZO ADAT: A nyomtatás adatai: a nyomtató típusa, az alapanyag (pl. PLA vagy PETG) és színe, a rétegvastagság, a kitöltés, a támaszanyag használata, valamint a kötőelemek (csavarok mérete). Az érzékelőfejnél az anyag színe mérési szempontból is lényeges: a sötét, nem áttetsző anyag árnyékol megfelelően.]

## A szemmozgáskövető

A tekintetet képernyő alá szerelhető Tobii Pro szemmozgáskövető ellenőrzi, amelyet a program a Tobii Pro SDK-n keresztül ér el (6.8. szakasz). A TMDK-dolgozat a Tobii Pro Spark eszközt mutatta be; ez 60 Hz-es mintavételű, USB-n csatlakozó, képernyőalapú szemkövető [51].

!fig foto_tobii.png | 5 | Tobii Pro Spark szemmozgáskövető (a kép a TMDK-dolgozatból származik).

A szemkövető nem része az időmérő láncnak: a próba teljes ideje alatt tekintetmintákat ad, amelyekből a program eldönti, hogy a résztvevő a megengedett területre nézett-e. Használatához a gyártó programjában (Tobii Pro Eye Tracker Manager) be kell állítani a kijelző méretét és a szemkövetőhöz viszonyított helyzetét (display area), és el kell végezni a résztvevő kalibrációját.

[HIANYZO ADAT: A mérésekhez ténylegesen használt szemkövető típusa és firmware-verziója, a gyártó által megadott pontossági és precizitási adatok forrásmegjelöléssel, valamint a felszerelés módja. A rendelkezésre álló mérési fájlok mindegyike szemkövető nélkül készült (a `Tracker` metaadat értéke „nincs”), ezért a típus a fájlokból nem állapítható meg.]

## A mérőhely összeállítása

1. A fejlesztőlap a hordozó NYÁK foglalatába, a NYÁK a dobozba kerül; a doboz USB-kábele a PC-hez csatlakozik, és a tápellátást és az adatkapcsolatot is ez adja.
2. A fotodióda befogója a kijelző peremére kerül úgy, hogy az érzékelőfej nyílása a jelzőnégyzet közepe fölött legyen. A jelző alapértelmezés szerint a jobb alsó sarokban van, a szélektől 20 képpontra; a programban a szenzor alá helyezhető.
3. Az egér a szokásos módon a PC-hez, a gombjel kivezetése a dobozhoz csatlakozik.
4. Tekintet-ellenőrzéses méréshez a szemkövető a kijelző alá kerül; a display area beállítása és a kalibráció után használható.
5. A kijelzőn és a videokártyán ki kell kapcsolni a változó frissítési frekvenciát (G-Sync, FreeSync), mert a scanout-korrekció állandó képfrissítést feltételez; ajánlott a natív felbontás és a kijelző alacsony késleltetésű üzemmódja.
