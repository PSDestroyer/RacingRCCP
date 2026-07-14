# Game Balance Audit

Scop: schema clara pentru economie, preturi, reward-uri si level progression.

## Surse de bani si EXP

### Career missions

Cand jocul porneste o misiune din career, reward-ul vine direct din `MissionSO`:

- CR: `MissionSO.rewardMoney`
- EXP: `MissionSO.rewardExp`
- reward-ul se da doar daca misiunea a fost reusita
- misiunea urmatoare se deblocheaza daca misiunea precedenta este completata

Scripturi implicate:

- `Assets/_GarageV2/Scripts/MissionSO.cs`
- `Assets/_GarageV2/Scripts/GamePlayManager.cs`
- `Assets/_GarageV2/Scripts/CareerMissionProgress.cs`

### Level up

Pe langa reward-ul misiunii, jocul mai da CR la level up:

- `GamePlayManager.expPerLevel = 10000`
- `GamePlayManager.levelUpMoneyReward = 1000`
- `MoneyManager.expPerLevel = 10000`

Important: `expPerLevel` exista in doua locuri. Daca valorile nu raman egale, reward logic si UI-ul de level pot arata diferit.

### Fallback map reward

Daca nu exista `SelectedCareerMission.Mission`, `GamePlayManager` foloseste `GlobalCarData.thismap.price` ca reward. Pentru career flow, acest fallback nu este sursa principala.

### Drift coins

`GamePlayManager` calculeaza `currentDriftCoins` si `totalDriftCoins`, dar in auditul curent nu se vede conversie directa in `SaveManager.saveData.money`. Deci momentan drift coins par mai mult scor/feedback decat valuta reala.

## Unde se cheltuie banii

### Cars

Cumpararea masinilor este in `Assets/_GarageV2/Scripts/CarSelection.cs`.

Date curente:

| Car | ID | Price | Power | Speed | Brake |
| --- | ---: | ---: | ---: | ---: | ---: |
| Proshe | 0 | 0 | 350 | 400 | 4000 |
| Truck1 | 1 | 50 | 250 | 400 | 2000 |
| Truck2 | 2 | 50 | 250 | 400 | 2000 |
| Mka30 | 3 | 50 | 250 | 400 | 2000 |
| Van1 | 4 | 50 | 250 | 400 | 2000 |
| Van | 4 | 50 | 250 | 400 | 2000 |

Probleme:

- aproape toate masinile costa 50 CR, dar o misiune da 500-1080 CR
- `CarSO_  5` are `_id: 4`, duplicat cu `CarSO_  4`
- masina free are stats mai bune decat majoritatea masinilor platite, deci progresia nu are sens economic

### Customization

Pret default in scripturi:

| Element | Script | Default price |
| --- | --- | ---: |
| Wheels | `RCCP_UI_Wheel.cs` | 50 |
| Neons | `RCCP_UI_Neon.cs` | 50 |
| Spoilers | `RCCP_UI_Spoiler.cs` | 50 |
| Decals | `RCCP_UI_Decal.cs` | 50 |
| Upgrades | `RCCP_UI_Upgrade.cs` | 50 |

Aceste preturi pot fi suprascrise in prefab/scena, dar default-ul e prea mic pentru o economie cu reward-uri de sute de CR.

## Mission reward audit

| Tournament | Mission | Type | Reward CR | Reward EXP | Locked by default |
| --- | --- | --- | ---: | ---: | --- |
| Offroad | 01 Offroad1 | Racing | 500 | 100 | No |
| Offroad | 02 Offroad1 | Racing | 500 | 100 | Yes |
| Offroad | 03 Offroad1 | Racing | 500 | 100 | Yes |
| Racing | 01 First Race | Elimination | 777 | 555 | No |
| Racing | 02 Racing2 | Racing | 1080 | 999 | Yes |
| Racing | 03 tretii | Racing | 0 | 0 | Yes |
| Racing | 04 paru | Racing | 0 | 0 | Yes |
| Racing | 05 cincos | Racing | 0 | 0 | Yes |
| Racing | 06 sex | Racing | 0 | 0 | Yes |

Probleme:

- misiunile Racing 03-06 nu dau nimic
- Offroad are reward plat: 500/100 pe toate misiunile
- numele unor misiuni sunt placeholder
- EXP-ul este foarte mic fata de `expPerLevel = 10000`; cu 100 EXP per misiune, level-up-ul vine prea tarziu
- cu masini la 50 CR, prima misiune poate cumpara aproape tot shop-ul basic

## Schema recomandata

### Principiu de pacing

Un jucator ar trebui:

- sa primeasca destul CR ca sa cumpere ceva mic dupa 1 cursa
- sa aiba nevoie de 2-4 curse pentru o masina early
- sa aiba nevoie de 4-7 curse pentru o masina mid
- sa simta ca upgrade-urile sunt utile, dar nu gratis
- sa nu poata cumpara tot dupa primele doua curse

### Starting state

Recomandat:

- starting money: 300-500 CR
- currentLevel: 1
- starter car: free si cumparata din default save

Observatie: in `Menu.unity`, default save data are 417 CR, dar SaveManager-ul din scena are si o valoare serialized de 100000 CR. Pentru test e comod, dar pentru build trebuie folosit default-ul real.

### EXP

Valoare aplicata:

- `expPerLevel`: 2500
- level-up money reward: 400 CR

Motiv: cu 10000 EXP per level, misiunile trebuie sa dea mii de EXP ca sa se simta progresia. Pentru un joc racing arcade, level-up-ul ar trebui sa vina la aproximativ 3-5 misiuni la inceput.

### First clear vs replay

Recomandat:

- first clear: 100% CR, 100% EXP
- replay win: 30% CR, 10-20% EXP
- failed mission: 0-15% CR, 0-10% EXP, doar daca vrei recompensa de participare

Momentan `GamePlayManager` plateste reward-ul misiunii de fiecare data cand misiunea este reusita. Asta permite farming complet.

## Tabel aplicat: misiuni

Varianta usoara pentru primul balans:

| Tournament | Mission | Reward CR | Reward EXP |
| --- | --- | ---: | ---: |
| Offroad | 01 | 350 | 450 |
| Offroad | 02 | 500 | 550 |
| Offroad | 03 | 700 | 700 |
| Racing | 01 | 450 | 500 |
| Racing | 02 | 650 | 650 |
| Racing | 03 | 850 | 750 |
| Racing | 04 | 1100 | 900 |
| Racing | 05 | 1400 | 1050 |
| Racing | 06 | 1800 | 1250 |

Cu `expPerLevel = 2500`, jucatorul face level 2 dupa aproximativ 4 curse early sau 2-3 curse mid.

## Tabel aplicat: masini

Roluri si preturi aplicate:

| Car | Price | Rol |
| --- | ---: | --- |
| Proshe | 0 | starter |
| Truck1 | 900 | early alternative |
| Truck2 | 1600 | early-mid |
| Mka30 | 2600 | mid |
| Van1 | 3800 | heavy/stable |
| Van | 5200 | late / special |

Important: stats-urile trebuie sa justifice pretul. Acum masina free este mai puternica decat cele platite, deci ori se reduce starter-ul, ori se cresc celelalte.

## Tabel aplicat: customization

Preturile de cosmetics sunt plate pe categorie, nu cresc pe item:

| Element | Price |
| --- | ---: |
| Wheels | 1000 |
| Neon | 700 |
| Spoiler | 500 |
| Decal | 300 |

Pentru upgrade-uri mecanice:

| Upgrade level | Cost |
| ---: | ---: |
| 1 | 150 |
| 2 | 300 |
| 3 | 550 |
| 4 | 900 |
| 5 | 1400 |

## Reguli de balans recomandate

1. O masina early trebuie sa coste aproximativ 2 misiuni early.
2. O masina mid trebuie sa coste aproximativ 3-5 misiuni mid.
3. Cosmeticile ieftine trebuie sa fie accesibile dupa 1 misiune.
4. Cosmeticile premium trebuie sa concureze cu upgrade-urile, nu sa fie cumparate gratis.
5. Level-up reward-ul nu trebuie sa fie mai mare decat reward-ul unei misiuni mid.
6. Replay farming trebuie redus, altfel misiunea cea mai rapida devine metoda unica de grind.
7. `currentLevel` trebuie pornit de la 1, nu 0.
8. `expPerLevel` trebuie sa fie o singura sursa sau sincronizat intre `MoneyManager` si `GamePlayManager`.

## Checklist de implementare

1. Fix data:
   - `CarSO_  5` -> `_id: 5`
   - seteaza preturi reale la masini
   - seteaza reward CR/EXP pentru toate misiunile
   - seteaza `currentLevel` default la 1

2. Fix reward logic:
   - first clear reward 100%
   - replay reward redus
   - optional failed reward mic

3. Fix level:
   - decide `expPerLevel`
   - sincronizeaza `MoneyManager.expPerLevel` si `GamePlayManager.expPerLevel`
   - decide `levelUpMoneyReward`

4. Fix shop/customization:
   - seteaza preturi pe categorii
   - verifica daca toate preturile sunt serializate corect in prefabs/scena

5. Test flow:
   - fresh save
   - dupa 1 misiune
   - dupa 3 misiuni
   - dupa primul tournament
   - dupa replay farming 10 minute
