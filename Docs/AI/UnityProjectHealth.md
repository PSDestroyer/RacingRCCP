# Audit pentru Formula 2027 — baza existentă și pașii concreți

Data: 22 octombrie 2026. Commit analizat: `50720840`.

Direcție confirmată de utilizator: Nintendo Switch original + Steam/PC, condus simcade, prezentare cu ambiție AAA.

## Concluzia auditului

Proiectul oferă o bază reutilizabilă pentru Formula: meniu/garaj, selecție, control prin gamepad, RCCP, adversari, tururi, clasament, rezultate, progres, salvare și conținut vizual. Recomandarea este să păstrăm această infrastructură și să o adaptăm în etape.

Cel mai util prim rezultat este un monopost bine reglat pe Track A, accesibil din meniu, într-o cursă completă cu trei adversari. Calitatea condusului și funcționarea pe Switch trebuie demonstrate înainte de extinderea grilei, a campionatului și a prezentării finale.

Cele mai importante intervenții asupra bazei sunt compatibilitatea playerului PC, salvarea Switch, LOD-ul care modifică fizica, identitatea datelor și dialogurile de meniu. Acestea sunt distincte de funcțiile Formula care trebuie dezvoltate.

## Acoperire și limite

- Inventariate 37 de scripturi din `_GarageV2`, șase din `_PlatformSpeciffics`, configurația proiectului, pachetele, input actions, cataloagele și conținutul relevant importat. Analiza de cod s-a concentrat pe fluxurile active și dependențele lor; codul vendor a fost eșantionat unde influențează Formula.
- Inspectate static 27 de fișiere `.unity`/`.prefab`: cele cinci scene din `Assets/Scenes`, cele 12 prefabs din `_GarageV2/Prefabs`, F1 și cele nouă scene RaceTracksPack. Instanțele imbricate au fost urmărite pentru dependențele importante, fără a reconstrui complet starea Editorului.
- Indexate 9.983 de GUID-uri din metadatele `Assets`; nu au fost găsite GUID-uri duplicate în acest index. GUID-urile de script din pachetele declarate au fost rezolvate și din metadatele cache-ului pentru a nu confunda componentele Unity cu scripturi lipsă.
- Nu au fost găsite referințe explicite `m_Script: {fileID: 0}` în scenele principale și prefabs `_GarageV2` inspectate. Aceasta nu înlocuiește verificarea Missing Scripts/References în Editor pentru întreaga ierarhie de prefabs.
- Unity CLI nu a găsit instanța Pipeline pentru acest proiect. Fereastra Unity identificată prin UI aparținea proiectului `Test`, deci nu a fost folosită pentru validarea RacingRCCP.
- Nu au fost rulate Play Mode, build-uri, teste, capturi Profiler sau teste pe consolă. Funcționarea, aspectul final și FPS-ul rămân nevalidate în acest audit.
- Auditul nu modifică scripturi, scene, prefabs, pachete sau Project Settings. Acest raport este singurul fișier nou creat în această etapă.

**Interpretarea dovezilor:** „confirmat” înseamnă observat în configurație/cod/serializare; „probabil” descrie un impact ce necesită reproducere; „nevalidat” cere verificare în runtime sau pe dispozitiv. „Mic/mediu/mare” indică efort relativ, nu o estimare în zile.

## Ce există și cum îl folosim

| Sistem | Ce există concret | Decizie pentru Formula |
| --- | --- | --- |
| Motor și randare | Unity `6000.0.55f1`, URP `17.0.4`, SRP Batcher activ | Păstrăm; construim profil Switch și profil PC dintr-o bază comună |
| Meniu | `GarageUIController`: MainHub, Shop, Upgrade, Play, Settings; istoric Back, focus de gamepad | Păstrăm logica de navigare; adaptăm structura și prezentarea la motorsport |
| Showroom/garaj | Spawn de vehicule, statistici, comparații, orbită mouse/right stick, camere Cinemachine | Refolosim pentru prezentarea monopostului și echipei; ajustăm încadrarea și datele afișate |
| Cumpărare/customizare | Confirmări, bani, mașini cumpărate, vopsea, decals, roți, spoilere, neon/sirene și upgrade-uri RCCP | Păstrăm infrastructura utilă pentru livree și deblocări; alegem explicit ce personalizare aparține produsului Formula |
| Selecție cursă | `MapSelect`: hartă → circuit → misiune; butoane generate, stări locked/completed/medalii | Adaptăm la mod → circuit → setări de sesiune; separăm circuitul de tipul sesiunii |
| Control | Input System `1.14.2`, UI actions separate de RCCP; tastatură, gamepad și bindings G920 | Păstrăm; armonizăm tranzițiile UI/condus și verificăm dispozitivele țintă |
| Remapare/iconuri | UI de rebind în Settings; overrides salvate în JSON; iconuri Xbox/PlayStation/Switch și recunoaștere NPad | Refolosim; testăm glyph-uri, confirm/back, reconectare și persistența remapării |
| RCCP | Versiunea locală `V2.22`: WheelCollider, motor, cutie, diferențial, axe, stabilitate, aero, damage, audio, camere | Păstrăm ca bază simcade; reglăm un monopost prin configurații controlate |
| Monopost existent | `Resources/Cars/F1.prefab`: masă 850 în Rigidbody, opt rapoarte, motor 450 Nm/9.000 RPM, limită configurată 360, aero și asistențe | Este un punct de pornire tehnic; valorile nu sunt performanțe măsurate sau specificații 2027 |
| Modele Formula | 14 prefabs Formula A1, cu aceeași sursă mesh `a1.FBX`; fără scripturi de controller în prefaburile vizuale verificate | Le evaluăm ca variante vizuale/livree pentru echipe; rig-ul fizic RCCP trebuie integrat separat |
| Cursa | Intro, countdown, spawn grilă, AI, progres pe waypoint-uri, tururi, poziție, finish, recompense și XP | Refolosim; extindem cu start/finish corect, timing, reguli și sesiuni Formula |
| AI | RCCP NavMesh/waypoint, anticipare viraje, viteze țintă, PID, brake zones, recuperare; managerul copiază performanța jucătorului | Refolosim conducerea de bază; reglăm trasa/frânările și introducem dificultate coerentă |
| HUD și rezultate | Viteză, treaptă, turație, ABS/ESP/TCS, poziție, tururi, clasament și ecrane de rezultate/XP | Păstrăm sursele de date și fluxul; adăugăm timing și informații specifice Formula |
| Circuite | RaceTracksPack A–E, variante de calitate; Track A are integrare gameplay și 39 waypoint-uri serializate | Track A este candidatul inițial pentru testare; celelalte necesită configurare și validare proprie |
| RTG | Generator modular, configurații de traseu, waypoint circuit, tribune/public, sunet public și ecrane mari | Util pentru prototipare și decor; traseele RTG au nevoie de legătură cu ruta de cursă RCCP |
| Minihartă | `MiniMap` poate urmări jucătorul și adversarii; managerul de gameplay are cod de conectare | Capacitate existentă, neconfirmată activ în Track A; pentru HUD Formula evaluăm o hartă simplificată a circuitului |
| Audio | Mixer separat pentru vehicul/SFX/muzică, volume persistente; audio RCCP pe benzi RPM, frânare, vânt, impact | Păstrăm rutarea și sistemul; sunetul F1 folosește acum familia generică `Engine_Sport2` și trebuie adaptat |
| Salvare | Newtonsoft JSON, profil, setări, mașină, deblocări, medalii, loadout-uri; integrare cont/save Nintendo | Refolosim modelul după corectarea platformelor, cozii de salvare și identității datelor |
| Localizare | Pachet, locale, String Tables, localizatoare pe UI | Infrastructură utilă; traducerile sunt incomplete și există texte hardcodate |
| Replay | RCCP are recorder runtime pentru input/transform/viteză; recorderul propriu este experimental, `ReplayRccp` este un stub | Evaluăm recorderul RCCP înainte de un sistem nou; nu considerăm replay-ul de cursă integrat |
| Networking | Photon PUN/Realtime/Chat și demo-uri importate | Capacitate de infrastructură; cursele online, lobby-ul și sincronizarea de produs nu sunt demonstrate |
| Trafic/orașe/alte vehicule | RTC, orașe, modele stradale, exemple și alte asset-uri | Conținut secundar; selectăm numai ce servește circuitele, mediul sau meniul Formula |

## Probleme și adaptări prioritare

### F01 — Compilarea playerului PC folosește simboluri rezervate Switch

**Severitate:** ridicată. **Certitudine:** confirmat în sursă; build neexecutat. **Efort:** mic.

`Init.Awake` și `SaveManager.Load/Save` aleg Nintendo pentru orice player non-Editor. Clasele/metodele apelate există numai sub `UNITY_SWITCH && !UNITY_EDITOR`. Playerul Standalone are astfel referințe la simboluri excluse din compilare.

**Acțiune:** ramuri explicite pentru Switch și fișier local pe PC; verificare namespace-uri și API-uri de platformă. **Validare:** compilare player PC și build de dezvoltare Switch separat.

Dovezi: `Assets/_PlatformSpeciffics/Init.cs:13`, `Assets/_PlatformSpeciffics/SaveManager.cs:32`, `Assets/_PlatformSpeciffics/SaveManager.cs:103`.

### F02 — LOD-ul activ pe F1 schimbă fizica după distanța față de cameră

**Severitate:** ridicată pentru curse competitive. **Certitudine:** confirmat în cod și prefab; efectul în cursă neprofilat. **Efort:** mediu.

F1 are `RCCP_Lod` activ. La nivelurile 1–3, acesta dezactivează `AeroDynamics` și `Stability`, pe lângă efecte vizuale. Astfel un adversar îndepărtat poate pierde downforce și asistențe în funcție de camera activă. Poate afecta inclusiv o mașină urmărită de o cameră cinematică aflată la distanță.

**Acțiune:** separarea reducerilor vizuale/audio de simularea necesară cursei; păstrarea aero/stabilității independent de camera de randare. **Validare:** același viraj/viteză cu mașina aproape și departe de cameră și cu schimbare de cameră.

Dovezi: `Assets/_GarageV2/Resources/Cars/F1.prefab:3635`, `Assets/Realistic Car Controller Pro/Scripts/Vehicle/RCCP_Lod.cs:105`, `:207`.

### F03 — Salvările Switch pot fi abandonate fără reîncercare

**Severitate:** ridicată pentru progres/setări. **Certitudine:** confirmat în cod; pierderea efectivă de progres trebuie reprodusă pe dispozitiv. **Efort:** mediu.

`NintendoSave` respinge cereri timp de cinci secunde după ultima salvare și folosește `Time.time`. `SaveManager.SaveSwitch` ignoră rezultatul boolean și raportează finalizarea. Mai multe acțiuni de meniu cer salvare succesiv, iar Settings salvează când se închide. În pauză, timpul scalat nu avansează; la începutul procesului, `lastSaveTime = 0` împiedică și primele cereri înaintea pragului. Nu există coadă pentru ultima stare.

**Acțiune:** stare dirty și salvare grupată/reprogramată cu ceas nescalat, tratarea rezultatului, commit explicit al datelor importante. Nu eliminați arbitrar limitarea de scriere. Pe PC: fișier temporar/backup și validarea datelor încărcate; formatul actual nu are versiune de schemă.

**Validare:** schimbări rapide de setări, meniu în pauză, recompensă urmată de ieșire, profil nou, fișier invalid și redeschidere. Parametrii Switch de save/journal sunt serializați ca zero în ProjectSettings și trebuie verificați în configurația efectivă de build; nu s-a demonstrat un build funcțional.

Dovezi: `Assets/_PlatformSpeciffics/Switch/NintendoSave.cs:28`, `:141`, `:278`; `Assets/_PlatformSpeciffics/SaveManager.cs:50`, `:105`; `ProjectSettings/ProjectSettings.asset:655`.

### F04 — Datele de circuit/sesiune și mașină au identitate ambiguă

**Severitate:** medie acum, ridicată înainte de extinderea conținutului. **Certitudine:** confirmat în serializare și cod. **Efort:** mediu.

Toate cele nouă `MapSO` au `_id: 0`, iar recuperarea folosește `GetMapById`, care returnează primul rezultat. Tipul de misiune salvat separat poate recupera tipul, dar nu identifică unic toate setările sesiunii. Meniul conține și un circuit placeholder `dasa asa`, trei misiuni fără `mapSo` și `unlockAllLevelsForTesting: 1`.

Catalogul auto are ID-uri `0,1,2,3,5,6,7,8,9`, în timp ce `currentCar` este folosit ca index în listă. `CarButton` compară ID-ul cu acel index. Reordonarea catalogului sau adăugarea unui monopost poate asocia greșit selecția/progresul. Nu există validare la toate accesările indexului.

**Acțiune:** ID-uri stabile pentru mașină/echipă/circuit/sesiune, migrarea salvării și validare de catalog; legătură explicită între circuit și scena sa. **Validare:** selecție, restart, redeschidere, adăugarea unei mașini și compatibilitatea salvării vechi.

Dovezi: `Assets/_GarageV2/Resources/SO/MapSo/*.asset:15`; `Assets/Scenes/Menu.unity:71149`; `Assets/_GarageV2/Scripts/Static scripts/GlobalCarData.cs:19`; `Assets/_GarageV2/Scripts/CarButton.cs`; `Assets/_GarageV2/Scripts/GamePlayManager.cs:539`.

### F05 — Cancel în dialogul de cumpărare nu încheie operația asincronă

**Severitate:** medie. **Certitudine:** confirmat în controlul de execuție. **Efort:** mic.

`ShowBuyYesNoPanelAsync` așteaptă `tcs.Task`. Cancel cheamă `ClosePanel`, care ascunde UI-ul și setează `tcs = null` fără rezultat sau anulare. `BuyCar` rămâne în așteptare după ce a eliminat listener-ele de navigare, iar restaurarea de la final nu mai este atinsă pe acel traseu.

**Acțiune:** finalizarea task-ului la Cancel și la distrugere/dezactivare, cu restaurarea input-ului într-un bloc garantat. **Validare:** deschidere → Cancel → navigare → redeschidere → confirmare, repetat pe gamepad.

Dovezi: `Assets/_GarageV2/Scripts/YesNo.cs:42`, `:61`, `:86`; `Assets/_GarageV2/Scripts/CarSelection.cs:161`.

### F06 — F1 există, dar configurarea Formula nu este conectată la produs

**Tip:** integrare necesară, nu regresie. **Prioritate:** ridicată pentru primul prototip. **Certitudine:** confirmat. **Efort:** mediu.

Sunt 21 de prefabs în `Resources/Cars`, dar numai nouă `CarSO` în catalogul folosit de meniu; niciunul nu trimite la `Cars/F1`. F1 are NOS activ, auto-reverse și asistențe de șablon. Are și `ineffectiveBehavior: 1`: simpla alegere a profilului global „Race” nu îi aplică automat setările de comportament. Materialele Formula A1 inspectate referă shader-e built-in, deci compatibilitatea vizuală URP trebuie verificată.

**Acțiune:** variantă de monopost deținută de proiect, catalog Formula, sursă unică pentru configurația fizică; alegere explicită a asistențelor, aero, frânelor și treptelor. Revizuirea NOS/auto-reverse/auto-reset. Modelul de energie Formula, dacă îl dorim, se proiectează ca sistem propriu, cu reguli și afișare clare.

**Validare:** spawn din meniu, aplicarea configurației la jucător și AI, comportament pe viraje lente/rapide și materiale corecte în build.

Dovezi: `Assets/_GarageV2/Resources/Cars/F1.prefab:405`, `:2174`, `:2784`, `:3020`; `Assets/Realistic Car Controller Pro/Scripts/Vehicle/RCCP_CarController.cs:771`; catalogul `CarData`.

### F07 — Structura cursei și dificultatea sunt încă orientate spre misiuni arcade

**Tip:** adaptare de design/gameplay. **Certitudine:** confirmat. **Efort:** mare, implementabil incremental.

Progresul pe waypoint-uri și turele sunt implementate. `FormatRaceTime` afișează numai minute/secunde rotunjite; nu s-au identificat timing pe sectoare, cel mai bun tur, delta sau sesiuni de calificări. La finish-ul jucătorului, `raceStarted` devine false și progresul competiției se oprește. Pentru un Grand Prix trebuie definit cum se finalizează și clasifică restul grilei.

Dynamic pacing este activ în Track A și modifică torque/RPM/turbo și parametri AI în funcție de distanța față de jucător. Track A folosește `usePlayerCarForOpponents: 0`, deci poate genera alte modele din catalog, chiar dacă le copiază o parte din performanță.

**Acțiune:** timing dedicat; start/finish și checkpoint-uri de validare separate conceptual de trasa AI; dificultate prin trasa/viteza/frânarea/adversarii configurați. Pacing-ul se evaluează ca opțiune explicită pentru modurile accesibile. Adversarii Formula trebuie să aibă modele și configurații controlate.

**Validare:** start, trecere inversă a liniei, scurtături, respawn, finish apropiat, restart/pauză și clasificarea întregii grile.

Dovezi: `Assets/_GarageV2/Scripts/GamePlayManager.cs:1591`, `:1813`, `:1889`, `:2470`, `:3145`; `Assets/RaceTracksPack/Scenes/Track A/RT_Track_A.unity:93672`.

### F08 — Profilul grafic Switch trebuie construit și măsurat

**Severitate:** risc mediu de platformă; impactul în FPS/memorie este nevalidat. **Certitudine:** configurație confirmată. **Efort:** mediu/mare după profilare.

QualitySettings are numai profilul PC, cu `PC_RPAsset`. Rendererul folosește Forward+, SSAO și decals active; pipeline-ul are HDR, opaque texture, umbre pentru lumini suplimentare și patru cascade. Existența `Mobile_RPAsset` nu dovedește că există un profil Switch pregătit. Fizica proiectului folosește pas de 0,02 s, iar RCCP configurează substeps pe roți.

**Acțiune:** profil Switch explicit și comparație măsurată a rendererului/efectelor, lumină predominant baked unde scena permite, LOD vizual, limitarea materialelor/reflexiilor/particulelor/audio simultan și controlul conținutului inclus. Creșterea grilei se face după măsurători CPU/GPU/memorie; nu schimbăm frecvența fizicii fără verificarea condusului.

**Validare:** build pe Switch în handheld și docked, start cu grila vizibilă, viraj aglomerat și sesiune prelungită. 60 FPS rămâne țintă de investigat, nu rezultat demonstrat.

Dovezi: `ProjectSettings/QualitySettings.asset:8`; `Assets/Settings/PC_Renderer.asset:48`, `:70`, `:89`; `Assets/Settings/PC_RPAsset.asset:23`; `ProjectSettings/TimeManager.asset`.

### F09 — Încărcarea comună depinde de scena Username

**Severitate:** medie pentru coerența fluxului. **Certitudine:** confirmat în distribuția componentelor și ramurile de cod. **Efort:** mic/mediu.

Singura componentă `LoadingManager` serializată găsită în scene/prefabs este în `Username.unity`. Pe profil existent, `Init` poate merge direct în Menu; instanța statică a loaderului nu este atunci inițializată prin acel traseu, iar codul folosește fallback-ul sincron. Nu există un bootstrap comun care să garanteze toate serviciile persistente indiferent de traseul de intrare.

**Acțiune:** servicii de pornire explicite în Init/Bootstrap, loader persistent cu UI și apeluri coerente la tranziții. **Validare:** prima pornire, profil existent, întoarcere în meniu și lansare/restart ale cursei.

Dovezi: `Assets/Scenes/Username.unity:2481`; `Assets/_PlatformSpeciffics/Init.cs:20`; `Assets/_GarageV2/Scripts/LoadingManager.cs:10`; `Assets/_GarageV2/Scripts/MapSelect.cs:174`.

### F10 — Localizarea și feedback-ul pentru control sunt parțiale

**Severitate:** medie înainte de livrare. **Certitudine:** confirmat în active/sursă. **Efort:** mediu.

Tabela UI are 80 de intrări în engleză, cinci în franceză, două în italiană și zero în germană/spaniolă/japoneză/coreeană. Fallback-ul String Database este dezactivat în setarea inspectată. `LOCKED`, `COMPLETED`, `Purchased`, unele rezultate și nume de moduri sunt hardcodate. Setarea de vibrații este salvată, dar nu s-a identificat un consumator al `vibrationsState` care să producă haptics în codul inspectat.

**Acțiune:** limbi efectiv susținute, chei pentru textele produsului, fallback ales explicit, fonturi și verificare de layout; conectarea haptics după stabilirea comportamentului. **Validare:** schimbarea limbii, texte lungi, handheld, glyph-uri și efectul real al toggle-ului de vibrații.

Dovezi: `Assets/Localizator/UI_*.asset`; `Assets/Localizator/Localization Settings.asset`; `Assets/_GarageV2/Scripts/SettingsManager.cs:74`; `Assets/_GarageV2/Scripts/MissionButtonSelect.cs`.

### F11 — Extinderea trebuie să reducă responsabilitățile comune și munca repetată

**Severitate:** medie pentru mentenanță; impactul în performanță este probabil și neprofilat. **Efort:** incremental.

`GamePlayManager` are 4.304 linii și îmbină moduri drift, curse, spawn, pacing, UI, rezultate și economie. `GetRankedRacers` construiește/sortează o listă nouă la apel; indicatorii pot cere clasamentul separat. `InputManager.GetPlayerInput` reface căutarea în scenă la fiecare apel; `ReadIconGamepad` îl poate apela pe traseul Update. RCCP rebind/customizare are deja legături directe spre SaveManager-ul proiectului, deci importarea unei versiuni noi cere atenție la integrarea locală.

**Acțiune:** extragem componente focalizate pe măsură ce adăugăm funcții: RaceSession, Timing, RaceStandings, OpponentSpawner și prezentarea HUD. Calculăm o singură stare de clasament și o publicăm consumatorilor. Cache pentru input și refresh la schimbarea scenei/dispozitivului. Păstrăm integrările locale documentate.

**Validare:** regresia cursei existente și Profiler CPU/GC; nu este justificată o rescriere generală numai din cauza dimensiunii clasei.

Dovezi: `Assets/_GarageV2/Scripts/GamePlayManager.cs:3601`; `Assets/_GarageV2/Scripts/InputManager.cs:29`; `Assets/_GarageV2/Scripts/ReadIconGamepad.cs:33`; `Assets/Realistic Car Controller Pro/Scripts/Inputs/RCCP_RebindSaveLoad.cs`.

## Funcții Formula care trebuie dezvoltate sau definite

Nu au fost identificate în gameplay-ul propriu inspectat: echipe/piloți cu identitate stabilă, calificări și grilă rezultată, punctaj și clasament de campionat, sectoare/best lap/delta, reguli și penalizări, pit-stop/strategie, compuși și uzură de pneuri, management de energie și un replay complet integrat pentru întreaga cursă. Obiectivul exact pentru fiecare rămâne o decizie de produs.

RCCP are temperatură simplă a roții bazată pe slip, multiplicator de grip și deflație; acestea sunt puncte de extensie, fără a demonstra un model de compuși/uzură/strategie. Există damage și componente care pot detașa piese/roți, dar comportamentul daunelor unui monopost și efectul asupra aero cer integrare dedicată.

Conținutul de ploaie/skybox și asset-urile de trafic nu demonstrează vreme dinamică, evoluția aderenței sau strategie de cursă. La fel, importarea Photon nu demonstrează multiplayer de produs. Steamworks/achievements/cloud și publicarea pe magazin nu sunt validate.

## Plan concret, în ordinea dependențelor

| Pas | Lucru concret | Rezultat verificabil | Efort relativ |
| --- | --- | --- | --- |
| 1. Baza care pornește și salvează | F01, F03, F05, F09; separare PC/Switch, bootstrap comun, confirmări și persistență corecte | Profil nou/existent, navigare, Cancel, setări, pornire/restart/finish funcționează în player; datele persistă la redeschidere | Mediu |
| 2. Identitatea și catalogul Formula | F04/F06; ID-uri stabile, migrare minimă, un monopost selectabil și configurație de sesiune separată de circuit; curățarea intrărilor placeholder din fluxul de produs | Din meniu selectăm Formula și Track A fără indexări fragile sau intrări fără destinație | Mediu |
| 3. Condusul de referință | F02/F06; varianta proprie F1, aero/stabilitate independente de cameră, frâne, direcție, pneuri și transmisie; configurații clare de asistențe | Un monopost previzibil pe gamepad la frânare, ieșire și viraje rapide; aceleași rezultate când schimbăm camera | Mediu/mare |
| 4. Circuitul de referință și bugetul Switch | Track A: verificăm suprafața, coliziunile, sensul, grila, ruta AI și NavMesh; profil Switch minim măsurabil din F08 | Cursa de test rulează pe consolă; avem măsurători de timp CPU/GPU/memorie și limite inițiale pentru conținut | Mediu/mare |
| 5. O cursă Formula completă | F07: start, gate-uri de progres, timer cu milisecunde/sectoare, poziții și clasificare; trei adversari Formula cu frânări/trasă reglate | Trei tururi de la meniu la rezultate, fără tururi false; AI termină; pauza/restartul și finish-ul apropiat au rezultate coerente | Mare |
| 6. Meniul și HUD-ul Formula | Refolosim panourile/focusul; Quick Race, Time Trial, Garage/Team, Settings; configurare tururi, AI și asistențe; HUD cu poziție/tur/timing/telemetrie | Tot fluxul poate fi parcurs cu gamepad; textul și feedback-ul rămân lizibile handheld | Mediu/mare |
| 7. Prezentarea reprezentativă | Alegere model A1/F1 după verificare vizuală, materiale URP, livree, camere onboard/TV, lumină și sunet dedicat; optimizare după capturi | O singură cursă arată și sună la standardul vizat și se menține în bugetul măsurat | Mare |
| 8. Competiția pe termen lung | Date Team/Driver, calificări, grilă, punctaj și clasament de campionat; extindem separat regulile, pit-stop/tyres/energie după priorități | Un mini-campionat poate fi terminat și reluat din salvare; fiecare sistem nou are criterii proprii de verificare | Mare, pe funcție |
| 9. Extindere și livrare | Mai multe circuite/livree/adversari după buget, localizare completă pentru limbile alese, Steam și cerințe de platformă; online/replay ca milestone-uri distincte | Build-uri reproductibile și testate pe hardware-ul țintă, fără a confunda funcții importate cu funcții integrate | Mare, pe milestone |

Pașii 1–5 formează primul obiectiv jucabil. Pașii 6–7 stabilesc reperul de prezentare. Obiectivele de campionat și extindere se bazează pe această cursă validată.

## Ce măsurăm înainte de a mări scopul

- Funcțional: clean profile, existing profile, intrare/ieșire din fiecare panou, confirm/back, trecere UI/condus, pauză, restart, rezultate și persistență.
- Corectitudinea cursei: traseu valid, tăiere circuit, linie în sens invers, respawn, AI blocat, finish apropiat și clasificarea întregii grile.
- Performanță: grila completă în cadru, primul viraj, audio/particule la impact, variații de cameră, mai multe curse consecutive; CPU, GPU, memorie și GC.
- Platformă: input și reconectare, suspend/resume, handheld/docked, load/save pe dispozitiv și build PC de referință. Build-urile nu pot fi înlocuite de verificarea din Editor.
- Automatizare: există Unity Test Framework `1.5.1`, dar nu s-au identificat teste first-party. Primele teste utile vizează identitatea datelor/migrarea, validarea tururilor, timing și clasificarea; restul cere probe de integrare și playtest.

Documente asociate: `Docs/AI/UnityProjectContext.md` pentru mediul tehnic și `Docs/Formula2027Direction.md` pentru direcția inițială. Acest audit adaugă constatările detaliate; nu modifică deciziile confirmate de utilizator.
