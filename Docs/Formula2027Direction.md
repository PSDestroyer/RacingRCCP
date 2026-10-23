# Formula 2027 — direcția proiectului

Actualizat: 22 octombrie 2026.

## Decizii confirmate

- Tematică: curse Formula 2027, cu ambiție de prezentare AAA.
- Platforme prioritare: Nintendo Switch original și Steam/PC.
- Condus: simcade — credibil, competitiv și accesibil.
- Conținutul de mașini al experienței Formula: un singur șasiu Generic și cele opt livree existente. A1 este exclus din catalog și progresie.

Catalogul Generic, prețurile, recompensele și condițiile de acces pentru meniul și progresia prototipului sunt definite în [GenericCatalog.md](Design/GenericCatalog.md), cu datele corespunzătoare în [GenericCatalog.v1.json](Design/GenericCatalog.v1.json). Această specificație înlocuiește propunerea inițială de catalog cu mai multe șasiuri.

Acestea sunt obiectivele produsului. Calitatea vizuală, performanța și funcționarea build-urilor nu au fost încă validate. Restul documentului reprezintă propunerea pentru prima etapă.

## Experiența propusă

Un joc de monoposturi în care frânarea, trasa și ieșirea din viraj fac diferența, cu asistențe reglabile și comenzi proiectate de la început pentru gamepad. Identitatea vizuală urmărește motorsportul modern: monoposturi lizibile, materiale coerente, sunet convingător și informații de cursă prezentate în stilul unei transmisii TV.

Switch-ul original stabilește bugetul de bază. PC-ul poate adăuga rezoluție, umbre, reflexii și detalii suplimentare, păstrând aceleași reguli și comportament al mașinii. Interfața trebuie să rămână lizibilă în modul portabil.

„2027” este momentan direcția tematică. Reproducerea unui sezon, a unor echipe sau a unui regulament real nu a fost stabilită.

## Prima versiune demonstrativă

O cursă de trei tururi, cu un monopost controlat de jucător și trei adversari, pe un singur circuit. Această scară folosește configurația de bază existentă și permite evaluarea rapidă a condusului și performanței. Numărul final de mașini rămâne deschis.

| Componentă | Rezultatul urmărit |
| --- | --- |
| Acces | Monopost selectabil din meniul obișnuit, încărcare corectă a circuitului |
| Condus | Direcție adaptată vitezei, frânare predictibilă, accelerație controlabilă și asistențe reglabile |
| Cursă | Grilă, semafor, start, tururi valide, clasament, finish și rezultate |
| Cronometrare | Timpi cu milisecunde, sectoare, cel mai bun tur și diferență față de reper |
| HUD | Poziție, tur, timpi, viteză, treaptă și turație, lizibile pe ecranul portabil |
| Camere și audio | Cameră de urmărire și cameră onboard stabile, intro scurt, motor și pneuri ușor de interpretat |
| Platforme | Separare corectă a salvării PC/Switch, control prin gamepad, profil grafic pentru fiecare platformă |

## Ordinea implementării

1. **Stabilizarea bazei PC/Switch.** Corectarea condițiilor de compilare din `Init` și `SaveManager`, verificarea pornirii și salvării într-un player PC și verificarea separată pe Switch când mediul de build este disponibil.
2. **Monopostul jucabil.** Integrarea șasiului Generic și a celor opt livree în catalogul meniului, verificarea selecției/spawn-ului, alegerea circuitului și reglarea RCCP pentru simcade. Progresul tehnic aparține șasiului, conform catalogului Generic.
3. **Cursa Formula.** Reutilizarea startului, AI-ului, progresului pe circuit și rezultatelor; adăugarea cronometrării precise și a unui HUD dedicat prin componente cu responsabilități clare.
4. **Prezentare și performanță.** Coerență pentru materiale, iluminare, camere și sunet; niveluri de detaliu și efecte ajustate după măsurători pe dispozitiv.
5. **Extindere după validare.** Mai mulți adversari, calificări/campionat, strategie de pneuri/pit-stop și eventual funcții online, în funcție de prioritățile stabilite ulterior.

## Criterii de acceptare pentru prima etapă

- O cursă poate fi parcursă din meniu până la rezultate și reluată folosind gamepadul.
- Progresul pe circuit nu acordă tururi prin scurtături sau trecerea liniei de finish în sens greșit.
- Cronometrarea, clasamentul și rezultatele rămân coerente la pauză și restart.
- Adversarii pot termina cursa și se comportă previzibil în frânări și depășiri.
- Salvarea și încărcarea funcționează separat pe PC și pe Switch, fără dependențe Nintendo în playerul PC.
- Performanța este măsurată pe Switch în mod portabil și docked, inclusiv la start, cu toate mașinile vizibile. 60 FPS este ținta inițială de evaluat; bugetul final se decide după profilare, fără promisiuni înaintea măsurătorilor.
- Calitatea finală se verifică în build, nu numai în Editor. Hardware-ul PC de referință rămâne de stabilit.

## Baza existentă și limitele evaluării

Unity `6000.0.55f1`, URP `17.0.4`, RCCP, Input System, Cinemachine, meniu/garaj, AI, tururi și rezultate există deja. Prefabul F1 există, dar nu este inclus în catalogul normal de mașini. Condițiile de compilare ale salvării trimit în prezent toate build-urile non-Editor către cod Switch și trebuie separate pentru PC.

Evaluarea inițială s-a făcut din fișiere. Conexiunea Unity CLI nu a găsit o instanță Pipeline; nu au fost rulate jocul, teste sau build-uri și nu au fost modificate scene, prefabs ori cod runtime. Detaliile tehnice și sursele sunt în [UnityProjectContext.md](AI/UnityProjectContext.md).
