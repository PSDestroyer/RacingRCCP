# Validarea integrării Formula Generic

Unity 6000.0.55f1, proiectul RacingRCCP. Mac-ul a fost deblocat și validarea a fost reluată la 23 octombrie. Cele 22 de verificări de progresie/salvare trec în Editor. Meniul, garajul, gamepad-ul virtual, setările existente, blocarea lansărilor repetate și frânarea trec; turul fizic G01 este însă declarat invalid atât la simulare 5×, cât și 1×. Fluxul complet nu este validat în starea actuală. Investigația rămâne deschisă; regulile de validitate nu au fost relaxate. Testele folosesc profiluri temporare.

Paleta roșu/alb/galben este integrată în componentele Formula și verificată în capturile `Home.png`, `Development.png`, `Liveries.png`. Butoanele blocate folosesc o nuanță de roșu în loc de gri, iar pagina și subpagina active au indicator galben. Integrarea exactă din fișierul Figma este blocată de refuzul accesului de editor prin conector. Nu au fost importate componente sau imagini din Figma. `UIIntegration.json` descrie delimitarea; panoul vechi audio/remapare își păstrează stilul.

| Etapă | Dovezi | Stare |
| --- | --- | --- |
| Șasiu / livree / dezvoltare separate | migrare, 10 cicluri JSON, echipare fără pierderea upgrade-urilor | Trecut |
| Generic în meniu și cursă | un singur CarSO, previzualizare și lansarea Circuitului 1 | Meniu trecut; flux complet nerevalidat |
| Garaj: Dezvoltare / Setup / Livree | patru ramuri, trei preseturi gratuite, opt livree, cumpărare prin UI | Trecut |
| Upgrade-uri fizice | patru niveluri motor/frâne măsurate, suspensie/aero și lipsa cumulării verificate | Trecut cu limitele de mai jos |
| Șase probe | configurate în catalog; deblocările tuturor verificate; traseul fizic comun parcurs pe ambele circuite | Trecut pentru integrare |
| Economie și recompense | 22 verificări, bonusuri unice, rezultate repetate, totaluri bronz/aur, cost 27.600 CR | Trecut |
| Acasă / Profil / Setări / Rezultate | capturi meniu 1280×720, navigare / Submit / Cancel prin gamepad virtual, audio/remapare existentă | Meniu trecut; rezultate nerevalidate |
| Salvare și platforme | backup corupt și migrare trecute; cumpărare/reîncărcare prin UI trecute în rularea istorică | Reluarea fluxului complet și validarea targeturilor rămân deschise |

## Rapoarte reproductibile

- `Tools > Formula Career > Validate Progression and Saves`: `ProgressionValidation.json`, 22 verificări, 0 eșecuri. Include refuzul suprascrierii când ambele fișiere sunt corupte și păstrarea backup-ului valid după recuperare.
- `Run Menu and Race Validation`: raportul actual `RuntimeValidation.json` are 6 verificări trecute și eșecul „Full lap failed validity”. Cele două încercări sunt păstrate în `RuntimeValidation.accelerated-invalid-lap-diagnostic.json` și `RuntimeValidation.normal-speed-invalid-lap-diagnostic.json`. Ipoteza că doar accelerarea simulării produce eșecul este contrazisă de testul la viteză normală; modificarea experimentală a vitezei validatorului a fost retrasă. Rularea istorică cu 10 verificări trecute precede corectarea duratei de invalidare în timp de simulare și nu validează starea curentă.
- `Validate Upgrade Physics`: `PhysicsValidation.json`. Repriză de la 60 km/h, treapta a patra fixă, 35% accelerație, patru secunde. Oprire de la 100 km/h până sub 3 km/h, ABS activ. Motor: aproximativ 77,29 / 78,37 / 79,47 / 80,61 km/h. Frâne: 39,84 / 38,45 / 37,28 / 36,58 m. Parametrii suspensiei și aerodinamicii sunt verificați separat, apoi resetați pentru comparațiile motor/frâne.
- `Build Desktop Validation`: build de dezvoltare al fluxului Formula (Init, Username, Menu și cele două circuite) pe targetul desktop activ. Raportul final este `BuildValidation.json` când Unity termină operația. Ordinea scenelor din proiect nu este înlocuită de această verificare.

Capturi: `Home.png`, `Development.png`, `Liveries.png`, `Results.png`, `Circuit2.png`. Rapoartele `*.initial-*-diagnostic.json` și cele de diagnostic fizic păstrează eșecurile investigației; ele nu reprezintă starea finală.

## Limite și verificări încă necesare

- Timpii pentru medalii sunt provizorii și marcați astfel în UI; necesită calibrare prin trei tururi curate cu mașina de bază și evaluarea dificultății de către jucător.
- Suspensia și aerodinamica modifică valorile fizice reale, însă nu a fost măsurat avantajul pe tur pentru fiecare combinație. Motorul nu garantează o lansare mai rapidă la accelerație maximă dacă aderența și TCS limitează puterea.
- Gamepad-ul de test este virtual. Controller fizic, butoane Nintendo, remaparea pe dispozitiv și ergonomia în mod portabil necesită validare fizică.
- Singurul modul de build desktop instalat pe acest host este MacStandaloneSupport. Un build macOS nu validează Windows/Steam. Modulul Windows și targetul Switch cu SDK/device nu sunt disponibile aici.
- Bugetele de CPU/GPU, memoria, încărcarea și numărul de cadre pe Switch original nu au fost profilate. Nu există încă dovadă de pregătire pentru lansare pe Switch sau Steam.
- Nu au fost conduse fizic toate cele șase probe până la ultima tură; infrastructura comună este verificată prin cele două tururi de integrare, iar condițiile și recompensele prin verificările de domeniu.

## Verificarea surselor actualizate în timpul buildului

Cât timp Editorul procesează shader-ele, compilatorul Roslyn inclus cu Unity a fost rulat separat cu copiile fișierelor de răspuns generate de Unity pentru `Assembly-CSharp` Player Development, `Assembly-CSharp` Editor și `Assembly-CSharp-Editor`. Ieșirile sunt izolate în `/private/tmp/FormulaCodeValidation`; fișierele generate din `Library` nu sunt înlocuite. Compilarea celor trei ținte a terminat cu 0 erori. Avertismentele și hash-urile surselor sunt în `CompilerValidation.json`.

`ProgressionValidation.Managed.json` conține 22 verificări trecute pe assembly-ul actualizat, încărcat într-un proces .NET separat. Catalogul este încărcat din JSON; copia validatorului înlocuiește numai atributele de meniu, logarea, versiunea Editorului și serializarea raportului, evitând API-urile native Unity. Regulile, migrarea și stocarea sunt cele din assembly-ul compilat. Include profil nou cu 0 CR / 0 XP / nivel 1, ignorând moneda și numele template-ului vechi, cu păstrarea setărilor implicite. Cele 22 de verificări au trecut acum și nativ în Editor.

Cele 15 tipuri menționate în `Assets/Formula/link.xml` au fost rezolvate prin reflecție în assembly (`LinkerDescriptorValidation.json`). Asta verifică numele și structura descriptorului, fără a valida un build IL2CPP.


## Build desktop executat și ultimul pas rămas

Buildul Development macOS / Mono a produs aplicația de aproximativ 1,38 GB în 3.255 secunde. Unity raportează `Succeeded`, dar raportul conține 1 eroare de shader și 85 de avertismente; aceasta nu este o validare curată. Eroarea este în `Assets/BunnyGirl/Materials/Character 1.shader`, pasul ShadowCaster: `LerpWhiteTo` nu era declarat. Sursa include acum explicit Core.hlsl și CommonMaterial.hlsl înainte de ShadowCasterPass.hlsl. Proprietățile materialului sunt păstrate. Corecția necesită reconfirmare prin build.

Buildul s-a executat înaintea ultimei revizuiri a surselor și nu include noua paletă UI. Capturile Home, Development și Liveries au fost regenerate în Editor. Captura Results anterioară nu este dovadă a rezultatelor: reluarea completă s-a oprit la turul invalid, înainte de etapa de captură. Reconfirmarea shaderului printr-un build nou rămâne deschisă.

Au fost eliminate puntea temporară de validare și cererea sa, precum și modificările incidentale ale setărilor grafice și fișierele de configurare generate de build. Modificările de produs și rapoartele de diagnostic sunt păstrate.
