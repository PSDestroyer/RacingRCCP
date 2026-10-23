# Implementarea Generic în Unity

Experiența Formula folosește un singur șasiu, opt livree, patru ramuri de dezvoltare și șase probe. Catalogul și prețurile sunt în [GenericCatalog.md](GenericCatalog.md). Runtime-ul citește `Assets/Formula/Resources/Formula/GenericCatalog.json`, copiat din [GenericCatalog.v1.json](GenericCatalog.v1.json).

## Date și salvare

`SaveManager` rămâne proprietarul salvării. Secțiunea `formula` separă șasiul, livreele deținute și echipate, nivelurile cumpărate și echipate, setup-ul, asistențele și rezultatele carierei. Identificatorii din catalog sunt cheile persistente. Listele inițiale sunt acordate idempotent prin migrare, astfel încât deserializarea nu dublează deținerea.

Migrarea păstrează creditele, XP-ul, nivelul existent ca prag minim, detaliile mașinilor vechi, personalizările vechi și câmpurile suplimentare necunoscute de la rădăcina salvării. Un profil fără fișier de salvare pornește cu valorile inițiale din catalog, indiferent de creditele sau numele din template-ul scenei vechi. Setările implicite sunt păstrate. Indexul mașinii vechi este arhivat separat; catalogul Formula are un singur index activ, 0. Nivelurile anterioare nu acordă retroactiv bonusuri în noua economie.

Cumpărăturile și rezultatele lucrează pe o copie a salvării. `SaveManager.TrySave` confirmă scrierea înainte de publicarea modificării; un eșec restaurează starea din memorie. Rezultatele au identificator unic de rulare și bonusuri unice pe probă și medalie. Reîncărcarea rezultatelor nu dublează plata. Cumpărarea unui upgrade verifică și nivelul așteptat de confirmare, pentru a evita debitarea la un click repetat.

PC și Editor folosesc `Application.persistentDataPath/save.json`, o scriere temporară și înlocuirea cu backup `.bak`. Dacă fișierul principal este corupt, se încearcă backup-ul. Dacă niciunul nu poate fi citit, originalele sunt păstrate și salvarea este blocată, fără suprascrierea profilului. `Assets/Formula/link.xml` păstrează DTO-urile create de JSON prin reflecție pentru targeturi IL2CPP, folosind [formatul link.xml Unity 6](https://docs.unity3d.com/6000.0/Documentation/Manual/managed-code-stripping-xml-formatting.html). Switch folosește API-ul Nintendo existent, doar în `UNITY_SWITCH && !UNITY_EDITOR`; rezultatul real al operației Commit este verificat.

## Meniu și garaj

Scena existentă `Assets/Scenes/Menu.unity` conține componenta `FormulaMainMenu`, cu referințe explicite către Canvas, navigarea veche, butonul Înapoi și punctul de prezentare. Inițializarea sa precedă meniul vechi. Decorul este reutilizat, iar mașinile stradale de prezentare sunt ascunse în timpul rulării. Scenele și modelele sursă rămân disponibile în proiect.

Navigarea oferă Acasă, Carieră, Garaj, Curse rapide, Profil și Setări. Acasă prezintă următoarea probă, recompensa inițială, creditele, nivelul și livreea echipată. Garajul are Dezvoltare, Setup și Livree. Livreele se previzualizează fără modificarea salvării; confirmarea echipează ori cumpără aspectul. Asistențele ABS, TCS și ESP sunt gratuite. Audio și remaparea comenzilor reutilizează interfața existentă.

Interfața este uGUI cu TextMeshPro și scalare de la 1280×720. Butoanele sunt selectabile, iar navigarea folosește EventSystem și Input System. Confirmările au opțiune Înapoi și verificare a tranzacției. Textele și dimensiunile finale pentru alte limbi necesită o verificare separată a localizării.

## Fizica dezvoltării

Toate valorile se calculează din configurația de bază capturată după inițializarea RCCP. Schimbarea nivelului sau a setup-ului nu cumulează multiplicatori peste un rezultat anterior. Prefabul de carieră folosește 1.400 Nm de frânare pe fiecare punte înainte de dezvoltare; prefabricatele sursă RCCP rămân la valorile lor anterioare.

| Ramură | Efect configurat pentru fiecare nivel |
| --- | --- |
| Motor | Cuplu +3% din bază; maximum +9% la nivelul 3 |
| Frâne | Cuplu de frânare față și spate +4% din bază; maximum +12%, păstrând proporțiile punților |
| Suspensie | Arcuri +1,5%, amortizare +3,5%, antiroll +2,5% din bază per nivel |
| Aerodinamică | Apăsare +4% și rezistență +1% din bază per nivel |

Setup-ul Echilibrat păstrează valorile dezvoltării. Viteză aplică 92% din apăsarea rezultată și 96% din rezistența rezultată. Viraje aplică 108% din apăsare și 104% din rezistență. Asistențele și geometria mașinii nu sunt upgrade-uri cumpărabile.

Efectul asupra timpilor de accelerație, distanței de frânare și stabilității trebuie interpretat împreună cu aderența, ABS și viteza. Procentele de cuplu nu sunt procente garantate de reducere a timpului sau distanței.

## Circuite și probe

Scenele `Assets/Formula/Scenes/Formula_Circuit_01.unity` și `Formula_Circuit_02.unity` folosesc geometria și decorul din cele două scene vizuale existente. Centrul drumului este calculat din perechile de margini ale mesh-ului EasyRoads, ordonate după coordonata UV longitudinală. Traseul este închis; checkpoint-urile sunt luate la fiecare trei secțiuni. Punctul de start este aliniat la traseu în zona camerei originale. Fiecare scenă are un NavMesh construit numai din geometria drumului și stocat în `Assets/Formula/Navigation`.

GamePlayManager păstrează startul, AI-ul, progresul pe traseu și tranziția la rezultate. Formula configurează tipul probei, numărul de tururi și adversarii. AI-ul are dezvoltare fixă pentru fiecare probă: nivel 0 la G03, nivel 1 la G04 și nivel 2 la G06. Copierea performanței jucătorului și recuperarea artificială în funcție de distanță sunt dezactivate pentru aceste probe.

Checkpoint-urile jucătorului sunt parcurse în ordine. Sunt acceptate apropierea de checkpoint sau trecerea planului său în interiorul coridorului pistei, cu mișcare în direcția corectă. Ieșirea din coridor mai mult de o secundă invalidează turul curent. Un tur ulterior valid poate salva rezultatul unei sesiuni Time Attack de trei tururi. G01 cere și accelerare la minimum 30 km/h, urmată de frânare până la cel mult 3 km/h.

Cronometrarea Formula folosește milisecunde, cel mai bun tur valid și totalul probei. Rezultatele afișează medalia, CR, XP, bonusul de nivel și livrelele noi. Un eșec al salvării oferă reîncercare, fără acordarea recompensei în memorie înainte de scrierea reușită.

Timpii de referință inițiali ai circuitelor sunt estimări din lungimea traseului la 32 m/s. Interfața îi marchează drept provizorii. Ei trebuie înlocuiți cu reperul măsurat în trei tururi curate cu configurația de bază înainte de fixarea dificultății finale a medaliilor.

## Unelte și validare

Meniul Unity `Tools > Formula Career` oferă pregătirea asseturilor Generic, pregătirea celor două circuite, conectarea meniului și camerelor, înregistrarea scenelor și verificările progresiei ori Play Mode.

`Prepare Two Circuits` refuză suprascrierea scenelor deja existente pentru a proteja ajustările manuale. `Connect Menu and Circuit Cameras` actualizează referințele meniului și proprietățile camerelor din scenele generate. Listele de scene păstrează ordinea scenelor existente; cele două circuite sunt adăugate la final.

Verificările domeniului folosesc date izolate. Testul Play Mode folosește un fișier temporar propriu, testează meniul, o oprire fizică, tururi conduse de AI-ul de test, cumpărarea prin UI, echiparea livrelei și reîncărcarea. La ieșire restaurează scena de lucru și ruta normală de salvare. Editorul și buildurile Development permit o rută temporară prin `FORMULA_VALIDATION_SAVE_PATH`; această suprascriere este exclusă din buildurile Release. Rezultatele și capturile sunt în `Docs/AI/FormulaCareer`.

Validarea pe dispozitiv Switch, profilarea performanței, calibrarea umană a timpilor și verificarea unui build desktop sunt distincte de compilarea în Editor și de testele domeniului. Starea executată a verificărilor este în rapoartele de validare, nu rezultă doar din existența codului.


## Măsurători și acoperire

`PhysicsValidation.json` înregistrează patru niveluri de motor și frâne în Play Mode, pe același plan, cu aceeași configurație de bază. Repriza pornește de la 60 km/h în treapta a patra, la 35% accelerație: după patru secunde s-au măsurat aproximativ 77,29 / 78,37 / 79,47 / 80,61 km/h pentru nivelurile 0 / 1 / 2 / 3. La oprirea de la 100 km/h până sub 3 km/h, cu ABS activ, distanțele au fost aproximativ 39,84 / 38,45 / 37,28 / 36,58 m. Sunt măsurători de test, nu promisiuni universale de performanță. La accelerație maximă, aderența și intervenția TCS pot limita sau inversa avantajul suplimentului de cuplu.

Suspensia și aerodinamica au fost verificate prin valorile RCCP aplicate, inclusiv repetarea fără multiplicare cumulativă. Nu s-a măsurat încă o îmbunătățire a timpului de tur pentru fiecare combinație de upgrade-uri și setup.

Navigarea UI a fost exercitată printr-un dispozitiv Gamepad virtual al Input System: deplasare cu D-pad, Submit și Cancel. Asta validează traseul evenimentelor și selecția în interfața reală. Verificarea cu un controller fizic, butoanele Nintendo și comportamentul pe hardware Switch rămân separate.


Starea ultimei verificări și pașii rămași sunt în [Validation.md](../AI/FormulaCareer/Validation.md) și [FinalStatus.json](../AI/FormulaCareer/FinalStatus.json). Buildul macOS inițial a produs aplicația, cu o eroare de shader în decor; cauza este corectată în sursă, iar reconfirmarea după corecție așteaptă deblocarea Mac-ului.
