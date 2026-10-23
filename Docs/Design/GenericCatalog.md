# Catalogul Generic și economia progresiei

Versiunea 1, 22 octombrie 2026. Specificație pentru implementarea meniului și progresiei. Valorile sunt definite pentru prototip; ritmul lor trebuie verificat în sesiuni reale de joc înainte de lansare. Datele corespunzătoare sunt în [GenericCatalog.v1.json](GenericCatalog.v1.json).

Jucătorul dezvoltă un singur monopost, Formula G27, bazat pe modelul Generic. Cele opt variante existente sunt livree ale aceluiași șasiu. Progresul tehnic aparține șasiului și se păstrează la schimbarea livrelei. A1 și mașinile stradale sunt excluse din catalogul acestei experiențe; fișierele lor sursă nu sunt șterse prin această specificație.

## Mașina și profilul inițial

| Element | Valoare |
| --- | --- |
| Nume afișat | Formula G27 |
| Identificator șasiu | `generic_2027` |
| Achiziție | Acordată gratuit la crearea profilului |
| Credite inițiale | 0 CR |
| Experiență inițială | 0 XP, nivelul 1 |
| Livree inițială echipată | Apex Orange, varianta 01 |
| Livree suplimentară deținută inițial | Azure Blue, varianta 04 |
| Dezvoltare inițială | Motor 0, Frâne 0, Suspensie 0, Aerodinamică 0 |
| Setup inițial | Echilibrat |

Configurația nominală actuală a mașinii de bază are 820 kg, 480 Nm, 1.800–12.000 RPM, opt trepte, tracțiune spate și limită configurată de 340 km/h. Limita configurată nu reprezintă o viteză măsurată. Toate livreele folosesc aceeași configurație pentru același nivel tehnic și același setup.

Meniul prezintă un singur card de mașină și opt opțiuni de livree. Modelele Generic 01–08 sunt resurse pentru aspect; ele nu devin opt achiziții tehnice independente.

## Upgrade-uri și prețuri

| Ramură | Nivel 1 | Nivel 2 | Nivel 3 | Cost complet |
| --- | ---: | ---: | ---: | ---: |
| Motor | 1.200 CR | 2.400 CR | 4.200 CR | 7.800 CR |
| Frâne | 900 CR | 1.800 CR | 3.200 CR | 5.900 CR |
| Suspensie | 1.000 CR | 2.000 CR | 3.600 CR | 6.600 CR |
| Aerodinamică | 1.100 CR | 2.200 CR | 4.000 CR | 7.300 CR |
| Total pe nivel | 4.200 CR | 8.400 CR | 15.000 CR | 27.600 CR |

Prețul fiecărui nivel este costul suplimentar al achiziției. Nivelul 3 al motorului cere deținerea nivelului 2 și plata a încă 4.200 CR. Nivelurile se cumpără în ordine pe fiecare ramură.

| Nivel | Condiție comună de acces |
| --- | --- |
| 1 | Cel puțin bronz la G01 Acomodare |
| 2 | Cel puțin bronz la G03 Debut în cursă și nivelul 1 deținut pe ramura aleasă |
| 3 | Cel puțin bronz la G06 Finala Generic și nivelul 2 deținut pe ramura aleasă |

Motorul dezvoltă accelerația prin cuplu. Frânele dezvoltă capacitatea de frânare, păstrând echilibrul între punți. Suspensia dezvoltă controlul mișcărilor șasiului prin arcuri și amortizare. Aerodinamica oferă mai multă apăsare în viraje rapide, cu rezistență suplimentară pe liniile drepte. Valorile fizice exacte se stabilesc în etapa de reglare; upgrade-urile de frâne trebuie măsurate împreună cu aderența și ABS.

Upgrade-urile folosesc geometria existentă în prima versiune. Asistențele ABS, TCS și ESP rămân opțiuni gratuite. Componenta RCCP numită Handling nu reprezintă dezvoltarea suspensiei. Viteza maximă nu are o ramură separată de cumpărături.

După cumpărare, jucătorul poate echipa gratuit orice nivel deja deținut, inclusiv nivelul 0. Creditele cheltuite nu se rambursează. Setup-urile Echilibrat, Viteză și Viraje sunt gratuite de la început și respectă nivelurile tehnice echipate.

## Livree și deblocări

Numele de mai jos sunt nume de afișare; sufixul identifică prefabul existent `Formula_Generic_RCCP_XX.prefab` din `Assets/Formula/RCCP/Prefabs/Generic`.

| Variantă | Livree | Preț | Condiție și obținere |
| --- | --- | ---: | --- |
| 01 | Apex Orange | Gratuită | Acordată la crearea profilului și echipată inițial |
| 02 | Velocity Black | Recompensă | Acordată automat la cel puțin bronz în G06 |
| 03 | Corsa Red | 1.500 CR | Devine cumpărabilă după cel puțin bronz în G03 |
| 04 | Azure Blue | Gratuită | Acordată la crearea profilului |
| 05 | Pulse | Recompensă | Acordată automat la aur în G02 |
| 06 | Volt Yellow | 2.200 CR | Devine cumpărabilă după cel puțin bronz în G04 |
| 07 | Nova Purple | Recompensă | Acordată automat după aur în toate cele șase probe |
| 08 | Forest Green | 2.800 CR | Devine cumpărabilă după cel puțin bronz în G05 |

Cele trei livree cumpărabile costă în total 6.500 CR. Livreele de merit nu pot fi cumpărate. Îndeplinirea unei condiții de cumpărare oferă acces la achiziție; deținerea se acordă după plată. Livreele primite ca recompensă se acordă fără plată și nu înlocuiesc automat livreea echipată.

Schimbarea aspectului nu schimbă performanța, nivelurile de dezvoltare sau setup-ul. Grafica de echipă și sponsorii vor primi identitatea fictivă a jocului în etapa vizuală.

## Cele șase probe și recompensele

G01 este disponibilă de la început. Fiecare probă următoare cere cel puțin bronz în cea precedentă. Accesul nu cere nivel de pilot, o anumită livree ori cumpărarea unui upgrade.

| Probă | Format | Circuit | Recompensă de bază repetabilă | Bonus unic la primul bronz |
| --- | --- | --- | ---: | ---: |
| G01 Acomodare | Un tur solo, mașină de bază, setup Echilibrat | Circuitul 1 | 0 CR și 0 XP | 1.200 CR și 1.000 XP |
| G02 Primul tur rapid | Sesiune de trei tururi, cel mai bun tur valid | Circuitul 1 | 800 CR și 800 XP | 800 CR și 400 XP |
| G03 Debut în cursă | Trei tururi, trei adversari | Circuitul 1 | 1.000 CR și 1.000 XP | 1.000 CR și 500 XP |
| G04 Sprint | Trei tururi, trei adversari | Circuitul 2 | 1.400 CR și 1.200 XP | 1.200 CR și 600 XP |
| G05 Tur de precizie | Sesiune de trei tururi, cel mai bun tur valid | Circuitul 2 | 1.200 CR și 1.100 XP | 1.200 CR și 600 XP |
| G06 Finala Generic | Cinci tururi, trei adversari | Circuitul 2 | 2.000 CR și 1.600 XP | 1.800 CR și 800 XP |

Fiecare probă acordă suplimentar, o singură dată, 200 CR și 200 XP la primul argint și încă 400 CR și 400 XP la primul aur. Dacă primul rezultat este aur, se acordă toate cele trei bonusuri de medalie. La îmbunătățirea ulterioară se plătesc doar treptele încă neîncasate.

Exemplu G03: prima finalizare cu bronz plătește 2.000 CR și 1.500 XP. O rulare nouă cu aur plătește baza de 1.000 CR și 1.000 XP, plus bonusurile noi de argint și aur, adică încă 600 CR și 600 XP. Un aur repetat ulterior plătește numai baza, 1.000 CR și 1.000 XP.

La G02 și G05 se parcurge întreaga sesiune de trei tururi; este necesar cel puțin un tur valid pentru recompensă. G01 nu oferă recompense de bază la repetare; bonusurile pentru medalii superioare încă neobținute rămân disponibile.

O cursă G03, G04 sau G06 terminată valid pe locul 4 acordă recompensa de bază, dar nu acordă bronz și nu deblochează proba următoare. O sesiune Time Attack terminată valid sub standardul de bronz acordă baza, dar nu deblochează mai departe. Abandonul, descalificarea și o sesiune fără rezultat valid acordă 0 CR și 0 XP. Nu există taxă de participare sau cost de reparație.

## Condițiile medaliilor

| Probă | Bronz | Argint | Aur |
| --- | --- | --- | --- |
| G01 | Tur terminat valid | Timp de cel mult 125% din reper | Timp de cel mult 112% din reper |
| G02 și G05 | Cel mai bun tur valid de cel mult 120% din reper | Cel mult 110% din reper | Cel mult 103% din reper |
| G03, G04 și G06 | Locurile 1–3 | Locurile 1–2 | Locul 1 |

Reperul T al fiecărui circuit este mediana a trei tururi curate consecutive realizate de același tester pe gamepad, cu Generic nivel 0, setup Echilibrat, transmisie automată, ABS și TCS active, în condițiile fixe ale probei. Timpii în secunde și legarea celor două circuite la scene se stabilesc după validarea circuitelor. De exemplu, dacă T măsurat este 90 de secunde, pragurile Time Attack sunt 108, 99 și 92,7 secunde; acest exemplu nu fixează timpul vreunui circuit existent.

Medalia afișată este cea mai bună obținută vreodată în probă. Toate pragurile de bronz trebuie verificate cu mașina de bază. G02–G06 permit configurația deținută a mașinii; țintele lor sunt calibrate din configurația de bază. Dificultatea și asistențele nu modifică recompensele în versiunea 1. O modificare ulterioară a setărilor nu retrage medalii sau livree deja câștigate.

## Nivelul pilotului și modurile libere

Nivelul pilotului este `1 + floor(XP total / 3.000)`. Fiecare nivel nou oferă 300 CR. Experiența este cumulativă și nu se cheltuiește. Nivelul nu blochează probe, upgrade-uri sau livree în această versiune; reprezintă progresul general al profilului.

Cursele rapide și antrenamentul sunt disponibile separat pentru ambele circuite. Nu plătesc recompense de carieră și nu acordă medalii sau deblocări de carieră. Time Attack cu clasament comparabil folosește mașina de bază și o configurație fixă; rezultatele sale sunt separate de cele ale probelor de carieră cu upgrade-uri.

## Verificarea economiei

| Scenariu de parcurgere a carierei | Credite înainte de cheltuieli | XP total | Nivel final |
| --- | ---: | ---: | ---: |
| Șase prime finalizări, toate cu bronz | 14.500 CR | 9.600 XP | 4 |
| Șase prime finalizări, toate direct cu aur | 18.400 CR | 13.200 XP | 5 |

Sumele includ bonusurile de nivel. Prima probă cu bronz plătește exact 1.200 CR, suficient pentru oricare dintre cele patru upgrade-uri de nivel 1. Toate nivelurile 1 și 2 costă împreună 12.600 CR. După șase rezultate cu bronz rămân 1.900 CR dacă jucătorul a cumpărat toate aceste niveluri și nicio livree.

Toate upgrade-urile costă 27.600 CR; toate cele trei livree cumpărabile costă 6.500 CR. Completarea tuturor achizițiilor costă 34.100 CR. Reluarea probelor G02–G06 finanțează dezvoltarea rămasă după finală; aurul repetat nu reactivează bonusurile unice. Rata creditelor pe minut trebuie verificată după măsurarea duratei reale a probelor, pentru a evita ca o singură probă să domine economia.

## Reguli de salvare și acceptare

- Șasiul, livreele, probele și ramurile folosesc identificatorii stabili din fișierul JSON. Numele afișat și ordinea cardurilor nu sunt chei de salvare.
- Deținerea și nivelul echipat se salvează distinct. Schimbarea livrelei păstrează dezvoltarea și setup-ul.
- Rezultatul fiecărei rulări are identificator unic. Recompensele, XP-ul, medaliile și deblocările se aplică într-o singură operație persistentă și idempotentă. Revenirea în rezultate sau relansarea jocului nu plătește aceeași rulare a doua oară.
- Bonusurile unice sunt înregistrate pe profil, probă și treaptă de medalie. Rulările noi pot acorda din nou numai recompensa de bază.
- Achiziția reverifică soldul, accesul, nivelul anterior și deținerea înainte de debitare. Confirmările repetate nu dublează plata.
- Migrarea unui profil existent păstrează soldul și progresul relevant; nu îl resetează la valorile unui profil nou.
- Valorile fizice se recalculează din configurația de bază și nivelul echipat. Încărcările repetate nu cumulează îmbunătățirile.
- Acceptarea implementării cere relansarea jocului după cumpărare, îmbunătățirea unei medalii, acordarea unei livree și schimbarea aspectului, cu păstrarea corectă a tuturor valorilor.

Specificația și datele JSON definesc catalogul. Integrarea în Unity este descrisă în [GenericImplementation.md](GenericImplementation.md), iar verificările executate și limitele lor în [Validation.md](../AI/FormulaCareer/Validation.md).
