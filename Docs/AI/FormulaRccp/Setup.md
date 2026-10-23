# Cele două modele Formula configurate cu RCCP

## Prefabs

- `Assets/Formula/RCCP/Prefabs/A1/Formula_A1_RCCP_01.prefab` până la `_14`: modelul A1 cu cele 14 livree existente.
- `Assets/Formula/RCCP/Prefabs/Generic/Formula_Generic_RCCP_01.prefab` până la `_08`: modelul OBJ cu cele opt variante existente ale materialului caroseriei.

Sunt două configurații geometrice, cu 22 de combinații vizuale. Mesh-urile și materialele originale rămân surse; versiuni URP și mesh-urile separate ale roților OBJ sunt salvate în `Assets/Formula/RCCP`.

## Ce este configurat

- RCCP controller, Rigidbody cu interpolare și Continuous Dynamic, patru WheelColliders și legături vizuale pentru rotație/virare/suspensie.
- Tracțiune spate, diferențial limited-slip, transmisie automată cu opt trepte, frâne și ABS/TCS/ESP pentru simcade.
- Masă configurată 820 kg, cuplu maxim configurat 480 Nm, plajă 1.800–12.000 RPM și limită de viteză configurată 340 km/h. Acestea sunt setări de prototip, fără pretenție de specificație oficială 2027 sau performanță măsurată la limită.
- Direcție progresivă cu viteza, centru de masă coborât, aero și rezistență la rulare. Fără NOS și fără componenta LOD care dezactivează fizica în funcție de cameră.
- Coliziuni de șasiu și aripi, damage pe roți/lumini; deformarea caroseriei și detașarea roților sunt dezactivate în această configurație.
- Trei LED-uri roșii în spate: central pentru frânare și două de siguranță, legate la RCCP Lights. Iluminarea de siguranță folosește comanda low-beam a RCCP; nu este o simulare de regulament pentru rain light.
- Audio RCCP pe benzi de turație, accelerație/decelerație, pornire, transmisie, vânt, frâne și impact, conectat la mixerul existent. Sunt refolosite clipurile disponibile ale prefabului F1 existent.
- Particule RCCP și evacuare din conținutul existent.
- Puncte de cameră onboard și lângă roată; recorder RCCP atașat. Volanul modelului A1 este conectat la animația RCCP Visual Dashboard.
- Materiale generate pentru URP Lit, cu păstrarea texturilor și livreei originale.
- Layerele de vehicul/roți sunt luate din setările RCCP ale proiectului.

La OBJ, anvelopele sunt identificate ca patru componente geometrice separate. Părțile de jantă din grupul de material sunt distribuite roților, iar geometria staționară este păstrată separat. Numărul de indici/triunghiuri este verificat la separare. Modelul este normalizat cu fața pe +Z și roțile la nivelul solului.

## Cum le testezi

Deschide una dintre scene și apasă Play:

1. `Assets/Formula/RCCP/Scenes/Formula_A1_TestDrive.unity`
2. `Assets/Formula/RCCP/Scenes/Formula_Generic_TestDrive.unity`

Scenele au platformă de test, servicii RCCP, un monopost și camera RCCP. Folosesc bindings-urile RCCP existente ale proiectului, inclusiv tastatură și gamepad. Selectează Game View pentru control. Prefaburile pot fi plasate și într-o scenă existentă cu servicii RCCP.

Bindings implicite: W/S sau săgețile pentru accelerație/frână, A/D pentru direcție, C pentru cameră și L pentru luminile de siguranță. Pe gamepad: triggere pentru accelerație/frână, stick stâng pentru direcție, apăsarea stick-ului stâng pentru cameră și D-pad sus pentru lumini. Ține S/săgeată jos (sau triggerul stâng) pentru frânare și apoi marșarier la oprire. W/săgeată sus (sau triggerul drept) frânează mersul înapoi și revine automat în treapta înainte lângă oprire. Remapările salvate ale proiectului pot schimba aceste comenzi.

Catalogul de cumpărare/selecție din meniul vechi nu este modificat de această operație; prefabs și scenele de test sunt intrările directe pentru aceste modele.

## Unelte și verificare

În Unity: `Tools > Formula RCCP`:

- **Build Both Models** regenerează prefaburile, materialele URP, mesh-urile separate și scenele de test din sursele existente. Ajustările manuale ale ieșirilor generate trebuie păstrate într-un prefab variant propriu sau în configurația builderului.
- **Enable Automatic Reverse and Test** activează marșarierul automat și inversarea accelerație/frână în toate cele 22 de variante, păstrând celelalte setări; apoi rulează testele de condus.
- **Validate Prefabs** verifică structura tuturor celor 22 de livree: scripturi, patru roți, legături drivetrain, audio, lumini și materiale.
- **Run Driving Validation** rulează scenariul Play Mode pe fiecare model: contact cu solul, accelerație, virare/rotație, frânare, marșarier ținând frâna, revenire înainte ținând accelerația, LED de frână și surse audio. Unity păstrează configurația scenei de lucru prin `playModeStartScene` și o restaurează la ieșire.
- **Build and Test Both Models** execută generarea și verificările de mai sus.

Rezultatele rulării sunt în `DrivingValidation.json`; imaginile `A1.png` și `Generic.png` provin din randarea efectivă Unity. Aceste verificări acoperă funcționarea de bază în Editor; nu reprezintă validarea unui build PC/Switch, a unei sesiuni lungi sau a reglajului final de competiție.

Rularea din 22 octombrie 2026, în Unity `6000.0.55f1`: **22/22 prefabs validate structural și 2/2 scenarii de condus trecute**, fără erori capturate în scenariile finale. Ambele modele au avut patru roți pe sol, au accelerat, au răspuns la direcție/rotația roților și au frânat până aproape de zero. LED-urile de frână și sursele audio au răspuns. Scena inițială a fost restaurată la ieșirea din Play Mode.

## Corecție marșarier — 22 octombrie 2026

Problema a fost reprodusă pe ambele modele în Editor: la ținerea frânei la oprire, treapta R nu se cupla și deplasarea înapoi rămânea zero. Configurația generată avea `RCCP_Input.autoReverse` și `inverseThrottleBrakeOnReverse` dezactivate. Rezultatul inițial este păstrat în `ReverseBeforeFix.json`.

Cele două opțiuni sunt acum activate în builder și în toate cele 22 de prefaburi, printr-o actualizare țintită în Unity, fără regenerarea geometriei sau a scenelor. Verificarea structurală respinge configurațiile care pierd aceste setări. Testul Play Mode a trecut pe ambele modele: A1 a parcurs aproximativ 11,35 m înapoi, iar Generic 10,58 m, apoi ambele au revenit înainte la accelerație. Au trecut și verificările de frânare, direcție, lumini, audio și contact al roților. Input-ul este injectat prin API-ul RCCP pentru test; nu s-a efectuat un test fizic cu gamepad sau un build PC/Switch. Scena de lucru `Race_Track_1_Visual` a fost restaurată la ieșirea testelor.
