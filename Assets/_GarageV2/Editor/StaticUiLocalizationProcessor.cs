#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class StaticUiLocalizationProcessor
{
    private const string TableName = "UI";
    private const string AutoRunEditorKey = "RacingRCCP.StaticUiLocalizationProcessor.v21";
    private const long LegacyCustomizationId = 298409691963392;
    private const long DuplicateCustomizationId = 45208391306436608;

    // key, English, German, Spanish, French, Italian, Japanese, Korean
    private static readonly string[][] Rows =
    {
        R("Save", "Save <sprite index=0>", "Speichern <sprite index=0>", "Guardar <sprite index=0>", "Enregistrer <sprite index=0>", "Salva <sprite index=0>", "保存 <sprite index=0>", "저장 <sprite index=0>"),
        R("Enter Nickname...", "Enter nickname...", "Spitznamen eingeben...", "Introduce un apodo...", "Saisissez un pseudo...", "Inserisci nickname...", "ニックネームを入力...", "닉네임 입력..."),
        R("Saving", "Saving", "Speichern", "Guardando", "Enregistrement", "Salvataggio", "保存中", "저장 중"),
        R("Back\t", "Back", "Zurück", "Atrás", "Retour", "Indietro", "戻る", "뒤로"),
        R("Play\t\t", "Drive", "Fahren", "Conducir", "Conduire", "Guida", "ドライブ", "주행"),
        R("Settings", "Settings", "Einstellungen", "Ajustes", "Paramètres", "Impostazioni", "設定", "설정"),
        R("Shop", "Shop", "Shop", "Tienda", "Boutique", "Negozio", "ショップ", "상점"),
        R("menu.customization", "Customization", "Anpassung", "Personalización", "Personnalisation", "Personalizzazione", "カスタマイズ", "커스터마이징"),
        R("customization.wheels", "Wheels", "Räder", "Ruedas", "Roues", "Ruote", "ホイール", "휠"),
        R("customization.neon", "Neon", "Neon", "Neón", "Néon", "Neon", "ネオン", "네온"),
        R("customization.spoilers", "Spoilers", "Spoiler", "Alerones", "Ailerons", "Spoiler", "スポイラー", "스포일러"),
        R("customization.upgrade", "Upgrade", "Verbessern", "Mejorar", "Améliorer", "Potenzia", "アップグレード", "업그레이드"),
        R("Drive", "Drive:", "Fahren:", "Conducir:", "Conduire :", "Guida:", "ドライブ：", "주행:"),
        R("CarClass", "Class", "Klasse", "Clase", "Classe", "Classe", "クラス", "등급"),
        R("Power", "Power", "Leistung", "Potencia", "Puissance", "Potenza", "パワー", "출력"),
        R("yes", "Yes", "Ja", "Sí", "Oui", "Sì", "はい", "예"),
        R("No", "No", "Nein", "No", "Non", "No", "いいえ", "아니요"),
        R("Sure", "Are you sure?", "Bist du sicher?", "¿Estás seguro?", "Êtes-vous sûr ?", "Sei sicuro?", "よろしいですか？", "확실합니까?"),
        R("No money", "Not enough money", "Nicht genug Geld", "Dinero insuficiente", "Pas assez d'argent", "Denaro insufficiente", "所持金が足りません", "금액이 부족합니다"),
        R("BuyYes/No", "Buy", "Kaufen", "Comprar", "Acheter", "Acquista", "購入", "구매"),
        R("Purchased", "Purchased", "Gekauft", "Comprado", "Acheté", "Acquistato", "購入済み", "구매 완료"),
        R("Select", "Select", "Auswählen", "Seleccionar", "Sélectionner", "Seleziona", "選択", "선택"),
        R("This item bought", "This item is already purchased", "Dieser Artikel wurde bereits gekauft", "Este artículo ya está comprado", "Cet article est déjà acheté", "Questo articolo è già stato acquistato", "このアイテムは購入済みです", "이미 구매한 아이템입니다"),
        R("MaxLevelUpgrade", "Maximum upgrade level reached", "Maximale Verbesserungsstufe erreicht", "Nivel máximo de mejora alcanzado", "Niveau d'amélioration maximal atteint", "Livello massimo di potenziamento raggiunto", "アップグレードが最大レベルです", "최대 업그레이드 레벨입니다"),
        R("WheelSmokeColor", "Wheel Smoke Colors", "Reifenrauchfarben", "Colores del humo de ruedas", "Couleurs de fumée des roues", "Colori del fumo delle ruote", "タイヤスモークカラー", "타이어 연기 색상"),
        R("Headlight Color", "Headlight Colors", "Scheinwerferfarben", "Colores de los faros", "Couleurs des phares", "Colori dei fari", "ヘッドライトカラー", "헤드라이트 색상"),
        R("Front Camber", "Front Camber", "Vorderer Sturz", "Caída delantera", "Carrossage avant", "Campanatura anteriore", "フロントキャンバー", "전륜 캠버"),
        R("Rear Camber", "Rear Camber", "Hinterer Sturz", "Caída trasera", "Carrossage arrière", "Campanatura posteriore", "リアキャンバー", "후륜 캠버"),
        R("Front Suspensions Spring Force", "Front Suspension Spring Force", "Federkraft vorne", "Fuerza del muelle delantero", "Force des ressorts avant", "Forza molla anteriore", "フロントスプリング強度", "전륜 스프링 강도"),
        R("Rear Suspensions Spring Force", "Rear Suspension Spring Force", "Federkraft hinten", "Fuerza del muelle trasero", "Force des ressorts arrière", "Forza molla posteriore", "リアスプリング強度", "후륜 스프링 강도"),
        R("Front Suspensions", "Front Suspension", "Vordere Aufhängung", "Suspensión delantera", "Suspension avant", "Sospensione anteriore", "フロントサスペンション", "전륜 서스펜션"),
        R("Rear Suspensions", "Rear Suspension", "Hintere Aufhängung", "Suspensión trasera", "Suspension arrière", "Sospensione posteriore", "リアサスペンション", "후륜 서스펜션"),
        R("Front Suspensions Spring Damp", "Front Suspension Damping", "Dämpfung vorne", "Amortiguación delantera", "Amortissement avant", "Smorzamento anteriore", "フロントダンピング", "전륜 댐핑"),
        R("Rear Suspensions Spring Damp", "Rear Suspension Damping", "Dämpfung hinten", "Amortiguación trasera", "Amortissement arrière", "Smorzamento posteriore", "リアダンピング", "후륜 댐핑"),
        R("Driving Assistances", "Driving Assists", "Fahrhilfen", "Asistencias de conducción", "Aides à la conduite", "Assistenze alla guida", "ドライビングアシスト", "주행 보조"),
        R("Steering Helpers", "Steering Assist", "Lenkhilfe", "Asistencia de dirección", "Assistance de direction", "Assistenza sterzo", "ステアリングアシスト", "조향 보조"),
        R("Steering", "Steering", "Lenkung", "Dirección", "Direction", "Sterzo", "ステアリング", "조향"),
        R("Traction", "Traction", "Traktion", "Tracción", "Adhérence", "Trazione", "トラクション", "접지력"),
        R("Angular Drag", "Angular Drag", "Drehwiderstand", "Resistencia angular", "Traînée angulaire", "Resistenza angolare", "角抵抗", "각 저항"),
        R("Turn", "Turn", "Drehen", "Girar", "Tourner", "Sterza", "旋回", "회전"),
        R("Rebind", "Rebind", "Neu belegen", "Reasignar", "Réassigner", "Riassegna", "再割り当て", "재지정"),
        R("Rebind Inputs", "Rebind Controls", "Steuerung neu belegen", "Reasignar controles", "Réassigner les commandes", "Riassegna comandi", "操作を再割り当て", "조작 재지정"),
        R("Audio", "Audio", "Audio", "Audio", "Audio", "Audio", "オーディオ", "오디오"),
        R("Sfx sound", "SFX Volume", "Effektlautstärke", "Volumen de efectos", "Volume des effets", "Volume effetti", "効果音の音量", "효과음 음량"),
        R("Vehicle sound", "Vehicle Volume", "Fahrzeuglautstärke", "Volumen del vehículo", "Volume du véhicule", "Volume veicolo", "車両音の音量", "차량 음량"),
        R("Music sound", "Music Volume", "Musiklautstärke", "Volumen de música", "Volume de la musique", "Volume musica", "音楽の音量", "음악 음량"),
        R("Throttle", "Accelerate", "Beschleunigen", "Acelerar", "Accélérer", "Accelera", "アクセル", "가속"),
        R("Brake", "Brake", "Bremsen", "Frenar", "Freiner", "Freno", "ブレーキ", "브레이크"),
        R("Handbrake", "Handbrake", "Handbremse", "Freno de mano", "Frein à main", "Freno a mano", "ハンドブレーキ", "핸드브레이크"),
        R("NOS", "Nitro", "Nitro", "Nitro", "Nitro", "Nitro", "ニトロ", "니트로"),
        R("Gear_N", "Neutral Gear", "Leerlauf", "Punto muerto", "Point mort", "Folle", "ニュートラル", "중립 기어"),
        R("Change Camera", "Change Camera", "Kamera wechseln", "Cambiar cámara", "Changer de caméra", "Cambia visuale", "カメラ切替", "카메라 변경"),
        R("Look Back", "Look Back", "Nach hinten sehen", "Mirar atrás", "Regarder derrière", "Guarda indietro", "後方を見る", "뒤 보기"),
        R("Low Beam Lights", "Low Beam Lights", "Abblendlicht", "Luces de cruce", "Feux de croisement", "Anabbaglianti", "ロービーム", "하향등"),
        R("High Beam Lights", "High Beam Lights", "Fernlicht", "Luces largas", "Feux de route", "Abbaglianti", "ハイビーム", "상향등"),
        R("Gear Shift Up", "Shift Up", "Hochschalten", "Subir marcha", "Rapport supérieur", "Marcia superiore", "シフトアップ", "기어 올리기"),
        R("Gear Shift Down", "Shift Down", "Herunterschalten", "Bajar marcha", "Rapport inférieur", "Marcia inferiore", "シフトダウン", "기어 내리기"),
        R("Indicator Hazard", "Hazard Lights", "Warnblinker", "Luces de emergencia", "Feux de détresse", "Quattro frecce", "ハザードランプ", "비상등"),
        R("Indicator Left", "Left Indicator", "Blinker links", "Intermitente izquierdo", "Clignotant gauche", "Freccia sinistra", "左ウインカー", "좌측 방향지시등"),
        R("Indicator Right", "Right Indicator", "Blinker rechts", "Intermitente derecho", "Clignotant droit", "Freccia destra", "右ウインカー", "우측 방향지시등"),
        R("Start/Stop Engine", "Start/Stop Engine", "Motor starten/stoppen", "Arrancar/Apagar motor", "Démarrer/Arrêter le moteur", "Avvia/Arresta motore", "エンジン始動／停止", "엔진 시동/정지"),
        R("Reset All", "Reset All", "Alles zurücksetzen", "Restablecer todo", "Tout réinitialiser", "Reimposta tutto", "すべてリセット", "모두 초기화"),
        R("ui.close", "Close", "Schließen", "Cerrar", "Fermer", "Chiudi", "閉じる", "닫기"),
        R("ui.continue", "Continue", "Fortsetzen", "Continuar", "Continuer", "Continua", "続ける", "계속"),
        R("ui.home", "Home", "Hauptmenü", "Inicio", "Accueil", "Home", "ホーム", "홈"),
        R("ui.left", "Left", "Links", "Izquierda", "Gauche", "Sinistra", "左", "왼쪽"),
        R("ui.right", "Right", "Rechts", "Derecha", "Droite", "Destra", "右", "오른쪽"),
        R("ui.front", "Front", "Vorne", "Delante", "Avant", "Anteriore", "フロント", "전면"),
        R("ui.rear", "Rear", "Hinten", "Trasera", "Arrière", "Posteriore", "リア", "후면"),
        R("upgrade.engine", "Engine", "Motor", "Motor", "Moteur", "Motore", "エンジン", "엔진"),
        R("upgrade.handling", "Handling", "Handling", "Manejo", "Tenue de route", "Maneggevolezza", "ハンドリング", "핸들링"),
        R("upgrade.brake", "Brake", "Bremsen", "Frenos", "Freinage", "Freni", "ブレーキ", "브레이크"),
        R("upgrade.speed", "Speed", "Geschwindigkeit", "Velocidad", "Vitesse", "Velocità", "スピード", "속도"),
        R("ui.free", "FREE", "KOSTENLOS", "GRATIS", "GRATUIT", "GRATIS", "無料", "무료"),
        R("ui.selected", "SELECTED", "AUSGEWÄHLT", "SELECCIONADO", "SÉLECTIONNÉ", "SELEZIONATO", "選択中", "선택됨"),
        R("ui.buy_action", "BUY!", "KAUFEN!", "¡COMPRAR!", "ACHETER !", "ACQUISTA!", "購入！", "구매!"),
        R("ui.level", "Level {0}", "Stufe {0}", "Nivel {0}", "Niveau {0}", "Livello {0}", "レベル {0}", "레벨 {0}"),
        R("ui.level_short", "LVL:{0}", "ST:{0}", "NVL:{0}", "NIV:{0}", "LIV:{0}", "LV:{0}", "LV:{0}"),
        R("ui.speed", "Speed", "Geschwindigkeit", "Velocidad", "Vitesse", "Velocità", "スピード", "속도"),
        R("shop.buy_select_confirm", "Buy / Select?", "Kaufen / Auswählen?", "¿Comprar / Seleccionar?", "Acheter / Sélectionner ?", "Acquistare / Selezionare?", "購入／選択しますか？", "구매 / 선택하시겠습니까?"),
        R("ui.owned", "Owned", "Im Besitz", "Adquirido", "Possédé", "Posseduto", "所有済み", "보유 중"),
        R("ui.in_use", "In Use", "In Verwendung", "En uso", "Utilisé", "In uso", "使用中", "사용 중"),
        R("input.waiting", "<Waiting...>", "<Warten...>", "<Esperando...>", "<En attente...>", "<In attesa...>", "<入力待ち...>", "<입력 대기...>"),
        R("controls.title", "CONTROLS", "STEUERUNG", "CONTROLES", "COMMANDES", "COMANDI", "操作方法", "조작"),
        R("controls.subtitle", "BROWSE CONTROLS", "STEUERUNG ANSEHEN", "VER CONTROLES", "VOIR LES COMMANDES", "VEDI I COMANDI", "操作を確認", "조작 보기"),
        R("controls.pause", "Pause", "Pause", "Pausa", "Pause", "Pausa", "ポーズ", "일시 정지"),
        R("controls.camera_change", "Handling / Camera Change", "Lenkung / Kamera wechseln", "Dirección / Cambiar cámara", "Direction / Changer de caméra", "Sterzo / Cambia visuale", "操作／カメラ切替", "조작 / 카메라 변경"),
        R("controls.navigate", "Navigate", "Navigieren", "Navegar", "Naviguer", "Naviga", "移動", "이동"),
        R("controls.respawn", "Respawn", "Zurücksetzen", "Reaparecer", "Réapparaître", "Ricomparsa", "復帰", "재시작"),
        R("ui.choose_style", "CHOOSE YOUR STYLE", "WÄHLE DEINEN STIL", "ELIGE TU ESTILO", "CHOISISSEZ VOTRE STYLE", "SCEGLI IL TUO STILE", "スタイルを選択", "스타일 선택"),
        R("ui.complete", "Complete", "Abgeschlossen", "Completado", "Terminé", "Completato", "完了", "완료"),
        R("ui.failed", "FAILED", "FEHLGESCHLAGEN", "FALLIDO", "ÉCHEC", "FALLITO", "失敗", "실패"),
        R("ui.finish", "FINISH", "ZIEL", "META", "ARRIVÉE", "TRAGUARDO", "フィニッシュ", "피니시"),
        R("ui.winner", "WINNER", "SIEGER", "GANADOR", "VAINQUEUR", "VINCITORE", "勝者", "우승"),
        R("ui.eliminated", "ELIMINATED", "AUSGESCHIEDEN", "ELIMINADO", "ÉLIMINÉ", "ELIMINATO", "脱落", "탈락"),
        R("ui.go", "GO!", "LOS!", "¡YA!", "PARTEZ !", "VIA!", "スタート！", "출발!"),
        R("ui.survivors_format", "SURVIVORS {0}/{1}", "VERBLEIBEND {0}/{1}", "SUPERVIVIENTES {0}/{1}", "SURVIVANTS {0}/{1}", "SUPERSTITI {0}/{1}", "生存者 {0}/{1}", "생존자 {0}/{1}"),
        R("ui.racer_out_format", "{0} OUT", "{0} AUS", "{0} ELIMINADO", "{0} ÉLIMINÉ", "{0} FUORI", "{0} 脱落", "{0} 탈락"),
        R("ui.position_format", "POSITION  {0}/{1}", "POSITION  {0}/{1}", "POSICIÓN  {0}/{1}", "POSITION  {0}/{1}", "POSIZIONE  {0}/{1}", "順位  {0}/{1}", "순위  {0}/{1}"),
        R("ui.time_format", "TIME: {0}", "ZEIT: {0}", "TIEMPO: {0}", "TEMPS : {0}", "TEMPO: {0}", "タイム：{0}", "시간: {0}"),
        R("ui.reward_format", "REWARD: {0:N0}  <color=#FFD21F>CR</color>", "BELOHNUNG: {0:N0}  <color=#FFD21F>CR</color>", "RECOMPENSA: {0:N0}  <color=#FFD21F>CR</color>", "RÉCOMPENSE : {0:N0}  <color=#FFD21F>CR</color>", "RICOMPENSA: {0:N0}  <color=#FFD21F>CR</color>", "報酬：{0:N0}  <color=#FFD21F>CR</color>", "보상: {0:N0}  <color=#FFD21F>CR</color>"),
        R("ui.exp_total_format", "{0:N0} EXP", "{0:N0} EP", "{0:N0} EXP", "{0:N0} EXP", "{0:N0} EXP", "経験値 {0:N0}", "경험치 {0:N0}"),
        R("ui.exp_gain_format", "+{0:N0} EXP", "+{0:N0} EP", "+{0:N0} EXP", "+{0:N0} EXP", "+{0:N0} EXP", "経験値 +{0:N0}", "경험치 +{0:N0}"),
        R("ui.level_up_reward_format", "LEVEL UP REWARD  +{0:N0}  <color=#FFD21F>CR</color>", "STUFENAUFSTIEG  +{0:N0}  <color=#FFD21F>CR</color>", "RECOMPENSA DE NIVEL  +{0:N0}  <color=#FFD21F>CR</color>", "RÉCOMPENSE DE NIVEAU  +{0:N0}  <color=#FFD21F>CR</color>", "PREMIO DI LIVELLO  +{0:N0}  <color=#FFD21F>CR</color>", "レベルアップ報酬  +{0:N0}  <color=#FFD21F>CR</color>", "레벨 업 보상  +{0:N0}  <color=#FFD21F>CR</color>"),
        R("race.classic", "CLASSIC RACE", "KLASSISCHES RENNEN", "CARRERA CLÁSICA", "COURSE CLASSIQUE", "GARA CLASSICA", "クラシックレース", "클래식 레이스"),
        R("race.elimination", "ELIMINATION", "ELIMINIERUNG", "ELIMINACIÓN", "ÉLIMINATION", "ELIMINAZIONE", "エリミネーション", "엘리미네이션"),
        R("race.no_brake", "NO BRAKE CHALLENGE", "OHNE-BREMSE-CHALLENGE", "DESAFÍO SIN FRENOS", "DÉFI SANS FREINS", "SFIDA SENZA FRENI", "ノーブレーキチャレンジ", "노 브레이크 챌린지"),
        R("race.drift_score", "DRIFT SCORE", "DRIFTPUNKTE", "PUNTUACIÓN DE DERRAPE", "SCORE DE DRIFT", "PUNTEGGIO DRIFT", "ドリフトスコア", "드리프트 점수"),
        R("race.target_drift", "TARGET DRIFT", "ZIEL-DRIFT", "DERRAPE OBJETIVO", "OBJECTIF DRIFT", "OBIETTIVO DRIFT", "ターゲットドリフト", "목표 드리프트"),
        R("race.combo_master", "COMBO MASTER", "KOMBO-MEISTER", "MAESTRO DEL COMBO", "MAÎTRE DU COMBO", "MAESTRO COMBO", "コンボマスター", "콤보 마스터"),
        R("race.free_drift", "FREE DRIFT", "FREIES DRIFTEN", "DERRAPE LIBRE", "DRIFT LIBRE", "DRIFT LIBERO", "フリードリフト", "프리 드리프트"),
        R("race.checkpoint_missed", "CHECKPOINT MISSED", "CHECKPOINT VERPASST", "PUNTO DE CONTROL OMITIDO", "CHECKPOINT MANQUÉ", "CHECKPOINT MANCATO", "チェックポイント通過失敗", "체크포인트 놓침"),
        R("race.wrong_direction", "WRONG DIRECTION", "FALSCHE RICHTUNG", "DIRECCIÓN INCORRECTA", "MAUVAISE DIRECTION", "DIREZIONE ERRATA", "逆走", "역주행"),
        R("race.you_missed_checkpoint", "YOU MISSED THE CHECKPOINT", "DU HAST DEN CHECKPOINT VERPASST", "TE SALTASTE EL PUNTO DE CONTROL", "VOUS AVEZ MANQUÉ LE CHECKPOINT", "HAI MANCATO IL CHECKPOINT", "チェックポイントを逃しました", "체크포인트를 놓쳤습니다"),
        R("race.respawn_in_format", "RESPAWN IN {0}", "RESPAWN IN {0}", "REAPARICIÓN EN {0}", "RÉAPPARITION DANS {0}", "RIENTRO TRA {0}", "{0}秒後に復帰", "{0}초 후 복귀"),
        R("ui.paused", "Paused", "Pausiert", "En pausa", "En pause", "In pausa", "一時停止中", "일시 정지"),
        R("ui.restart", "Restart", "Neu starten", "Reiniciar", "Recommencer", "Riavvia", "リスタート", "다시 시작"),
        R("ui.quit", "Quit", "Beenden", "Salir", "Quitter", "Esci", "終了", "종료"),
        R("ui.quality", "Quality", "Qualität", "Calidad", "Qualité", "Qualità", "画質", "품질"),
        R("ui.very_low", "Very Low", "Sehr niedrig", "Muy baja", "Très faible", "Molto bassa", "最低", "매우 낮음"),
        R("ui.auto", "Auto", "Auto", "Auto", "Auto", "Auto", "自動", "자동"),
        R("ui.on", "On", "An", "Activado", "Activé", "Attivo", "オン", "켜짐"),
        R("ui.gearbox", "Gearbox", "Getriebe", "Caja de cambios", "Boîte de vitesses", "Cambio", "ギアボックス", "변속기"),
        R("ui.grip", "Grip", "Grip", "Agarre", "Adhérence", "Aderenza", "グリップ", "그립"),
        R("ui.drift", "Drift", "Drift", "Derrape", "Drift", "Drift", "ドリフト", "드리프트"),
        R("ui.offroad", "Offroad", "Gelände", "Todoterreno", "Tout-terrain", "Fuoristrada", "オフロード", "오프로드"),
        R("ui.racing", "Racing", "Rennen", "Carreras", "Course", "Gara", "レース", "레이싱"),
        R("ui.behaviors", "Behaviors", "Verhalten", "Comportamientos", "Comportements", "Comportamenti", "挙動", "동작"),
        R("ui.mobile_controller", "Mobile Controller", "Mobile Steuerung", "Control móvil", "Commandes tactiles", "Comandi touch", "モバイル操作", "모바일 조작"),
        R("ui.touch", "Touch", "Touch", "Táctil", "Tactile", "Touch", "タッチ", "터치"),
        R("ui.next_tour", "Next tour", "Nächste Runde", "Siguiente ronda", "Tour suivant", "Giro successivo", "次のラウンド", "다음 라운드"),
        R("ui.previous_tour", "Previous tour", "Vorherige Runde", "Ronda anterior", "Tour précédent", "Giro precedente", "前のラウンド", "이전 라운드"),
        R("ui.leaderboard", "Leaderboard", "Bestenliste", "Clasificación", "Classement", "Classifica", "ランキング", "순위표"),
        R("ui.lap_format", "LAP {0}/{1}", "RUNDE {0}/{1}", "VUELTA {0}/{1}", "TOUR {0}/{1}", "GIRO {0}/{1}", "ラップ {0}/{1}", "랩 {0}/{1}"),
        R("ui.no_brake_lap_format", "NO BRAKE  LAP {0}/{1}", "OHNE BREMSE  RUNDE {0}/{1}", "SIN FRENOS  VUELTA {0}/{1}", "SANS FREINS  TOUR {0}/{1}", "SENZA FRENI  GIRO {0}/{1}", "ブレーキなし  ラップ {0}/{1}", "브레이크 없이  랩 {0}/{1}"),
        R("ui.preparing_track", "Preparing Track", "Strecke wird vorbereitet", "Preparando pista", "Préparation du circuit", "Preparazione pista", "コース準備中", "트랙 준비 중"),
        R("shop.subtitle", "TRUCK SALON", "TRUCK-SALON", "SALÓN DE CAMIONES", "SALON DE CAMIONS", "SALONE TRUCK", "トラックショップ", "트럭 상점"),
        R("customization.subtitle", "CUSTOMIZE YOUR TRUCK", "TRUCK ANPASSEN", "PERSONALIZA TU CAMIÓN", "PERSONNALISEZ VOTRE CAMION", "PERSONALIZZA IL TRUCK", "トラックをカスタマイズ", "트럭 꾸미기"),
        R("settings.language", "Language", "Sprache", "Idioma", "Langue", "Lingua", "言語", "언어"),
    };

    private static string[] R(params string[] values) => values;

    [InitializeOnLoadMethod]
    private static void ScheduleFirstRun()
    {
        if (EditorPrefs.GetBool(AutoRunEditorKey, false))
            return;

        EditorApplication.delayCall += RunOnceAfterCompilation;
    }

    private static void RunOnceAfterCompilation()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunOnceAfterCompilation;
            return;
        }

        try
        {
            Process();
            EditorPrefs.SetBool(AutoRunEditorKey, true);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/Localization/Localize Static Game UI")]
    public static void Process()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(TableName);
        if (collection == null)
            throw new InvalidOperationException($"String table collection '{TableName}' was not found.");

        Dictionary<string, long> englishToId = UpdateTables(collection);
        int localizedComponents = 0;

        foreach (string prefabPath in FindGameUiPrefabs())
            localizedComponents += ProcessPrefab(prefabPath, englishToId);

        foreach (string scenePath in FindGameScenes())
            localizedComponents += ProcessScene(scenePath, englishToId);

        ConfigureFontFallbacks();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Static UI localization complete. Added or updated {Rows.Length} table rows and configured {localizedComponents} text components.");
    }

    private static void ConfigureFontFallbacks()
    {
        const string koulenPath = "Assets/_GarageV2/NewUI/Fonts/Koulen/Koulen-Regular SDF.asset";
        const string latinPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        const string koreanSourcePath = "Assets/Racing_Game/Font/gmarket_sans/GmarketSansBold.otf";
        const string koreanAssetPath = "Assets/_GarageV2/NewUI/Fonts/Fallbacks/GmarketSansBold SDF.asset";
        const string japaneseSourcePath = "Assets/_GarageV2/NewUI/Fonts/NotoSansJP-VariableFont_wght.ttf";
        const string japaneseAssetPath = "Assets/_GarageV2/NewUI/Fonts/Fallbacks/NotoSansJP Dynamic SDF.asset";

        TMP_FontAsset koulen = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(koulenPath);
        TMP_FontAsset latin = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(latinPath);
        TMP_FontAsset korean = GetOrCreateDynamicFontAsset(koreanSourcePath, koreanAssetPath);
        TMP_FontAsset japanese = GetOrCreateDynamicFontAsset(japaneseSourcePath, japaneseAssetPath);

        var fallbacks = new List<TMP_FontAsset>();
        AddFallback(fallbacks, latin);
        AddFallback(fallbacks, japanese);
        AddFallback(fallbacks, korean);

        if (koulen != null)
        {
            koulen.fallbackFontAssetTable = new List<TMP_FontAsset>(fallbacks);
            EditorUtility.SetDirty(koulen);
        }

        TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(
            "Assets/TextMesh Pro/Resources/TMP Settings.asset");
        if (settings != null)
        {
            TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>(fallbacks);
            EditorUtility.SetDirty(settings);
        }

        if (japanese == null)
            Debug.LogWarning($"Japanese TMP fallback is pending. Add the OFL font at '{japaneseSourcePath}' and run this processor again.");
    }

    private static TMP_FontAsset GetOrCreateDynamicFontAsset(string sourcePath, string assetPath)
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null)
            return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (source == null)
            return null;

        TMP_FontAsset created = TMP_FontAsset.CreateFontAsset(
            source, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048,
            AtlasPopulationMode.Dynamic, true);
        if (created == null)
            return null;

        Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? string.Empty);
        AssetDatabase.CreateAsset(created, assetPath);
        created.atlasTexture.name = Path.GetFileNameWithoutExtension(assetPath) + " Atlas";
        created.material.name = Path.GetFileNameWithoutExtension(assetPath) + " Material";
        AssetDatabase.AddObjectToAsset(created.atlasTexture, created);
        AssetDatabase.AddObjectToAsset(created.material, created);
        EditorUtility.SetDirty(created);
        return created;
    }

    private static void AddFallback(ICollection<TMP_FontAsset> fallbacks, TMP_FontAsset font)
    {
        if (font != null && !fallbacks.Contains(font))
            fallbacks.Add(font);
    }

    private static Dictionary<string, long> UpdateTables(StringTableCollection collection)
    {
        var result = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        string[] localeCodes = { "en", "de", "es", "fr", "it", "ja", "ko" };

        RemoveDuplicateCustomizationEntry(collection, localeCodes);

        foreach (string[] row in Rows)
        {
            SharedTableData.SharedTableEntry sharedEntry;
            if (row[0] == "menu.customization")
            {
                sharedEntry = collection.SharedData.GetEntry(LegacyCustomizationId);
                if (sharedEntry == null)
                    sharedEntry = collection.SharedData.AddKey(row[0], LegacyCustomizationId);
            }
            else
            {
                sharedEntry = collection.SharedData.GetEntry(row[0]) ?? collection.SharedData.AddKey(row[0]);
            }

            for (int i = 0; i < localeCodes.Length; i++)
            {
                var table = collection.GetTable(new LocaleIdentifier(localeCodes[i])) as StringTable;
                if (table == null)
                    continue;

                StringTableEntry entry = table.GetEntry(sharedEntry.Id) ?? table.AddEntry(sharedEntry.Id, row[i + 1]);
                entry.Value = row[i + 1];
                EditorUtility.SetDirty(table);
            }

            foreach (string alias in EnglishAliases(row))
                result[Normalize(alias)] = sharedEntry.Id;
        }

        EditorUtility.SetDirty(collection.SharedData);
        return result;
    }

    private static void RemoveDuplicateCustomizationEntry(StringTableCollection collection, IEnumerable<string> localeCodes)
    {
        foreach (string localeCode in localeCodes)
        {
            var table = collection.GetTable(new LocaleIdentifier(localeCode)) as StringTable;
            if (table != null && table.Remove(DuplicateCustomizationId))
                EditorUtility.SetDirty(table);
        }

        if (collection.SharedData.GetEntry(DuplicateCustomizationId) != null)
            collection.SharedData.RemoveKey(DuplicateCustomizationId);
    }

    private static IEnumerable<string> EnglishAliases(string[] row)
    {
        yield return row[1];

        switch (row[0])
        {
            case "Save": yield return "Save"; break;
            case "Play\t\t": yield return "Play"; break;
            case "Drive": yield return "Drive"; break;
            case "menu.customization": yield return "Customization"; break;
            case "Purchased":
                yield return "Purchadsed";
                break;
            case "Sure":
                yield return "You Are Sure?";
                break;
            case "Handbrake":
                yield return "Hand Brake";
                yield return "hand brake";
                break;
            case "controls.camera_change":
                yield return "Handling / Camera Change";
                yield return "Handling /\nCamera Change";
                break;
            case "ui.preparing_track":
                yield return "Prepairing Track";
                break;
            case "Front Suspensions Spring Force": yield return "Front Suspensions Spring Force"; break;
            case "Rear Suspensions Spring Force": yield return "Rear Suspensions Spring Force"; break;
            case "Front Suspensions": yield return "Front Suspensions"; break;
            case "Rear Suspensions": yield return "Rear Suspensions"; break;
            case "Front Suspensions Spring Damp": yield return "Front Suspensions Spring Damp"; break;
            case "Rear Suspensions Spring Damp": yield return "Rear Suspensions Spring Damp"; break;
            case "Driving Assistances": yield return "Driving Assistances"; break;
            case "Steering Helpers": yield return "Steering Helpers"; break;
            case "Sfx sound": yield return "Sfx sound"; break;
            case "Vehicle sound": yield return "Vehicle sound"; break;
            case "Music sound": yield return "Music sound"; break;
            case "Throttle": yield return "Throttle"; break;
            case "Gear_N": yield return "Gear_N"; break;
            case "Gear Shift Up": yield return "Gear Shift Up"; break;
            case "Gear Shift Down": yield return "Gear Shift Down"; break;
            case "Indicator Hazard": yield return "Indicator Hazard"; break;
            case "Indicator Left": yield return "Indicator Left"; break;
            case "Indicator Right": yield return "Indicator Right"; break;
        }
    }

    private static IEnumerable<string> FindGameScenes()
    {
        return EditorBuildSettings.scenes
            .Where(scene => scene.enabled && File.Exists(scene.path))
            .Select(scene => scene.path);
    }

    private static IEnumerable<string> FindGameUiPrefabs()
    {
        string[] folders =
        {
            "Assets/_GarageV2/Prefabs",
            "Assets/_GarageV2/Resources/UI",
            "Assets/Racing_Game/Prefabs/Manager"
        };

        return AssetDatabase.FindAssets("t:Prefab", folders)
            .Select(AssetDatabase.GUIDToAssetPath)
            .Distinct();
    }

    private static int ProcessPrefab(string path, IReadOnlyDictionary<string, long> englishToId)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            int changed = path.EndsWith("/Controls.prefab", StringComparison.OrdinalIgnoreCase)
                ? EnsureControlsHeader(root)
                : 0;
            changed += ProcessHierarchy(root, englishToId);
            if (changed > 0)
                PrefabUtility.SaveAsPrefabAsset(root, path);
            return changed;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static int EnsureLanguageDropdown(GameObject root)
    {
        LanguageDropdown existing = root.GetComponentInChildren<LanguageDropdown>(true);
        if (existing != null)
        {
            TMP_Dropdown existingDropdown = existing.GetComponent<TMP_Dropdown>();
            existingDropdown.ClearOptions();
            existingDropdown.AddOptions(new List<string>
            {
                "English", "Deutsch", "Español", "Français", "Italiano", "日本語", "한국어"
            });
            existingDropdown.RefreshShownValue();
            existing.ConfigureFont(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/_GarageV2/NewUI/Fonts/Koulen/Koulen-Regular SDF.asset"));
            EditorUtility.SetDirty(existingDropdown);
            EditorUtility.SetDirty(existing);
            return 1;
        }

        GameObject qualityGroup = root.GetComponentsInChildren<Transform>(true)
            .Select(item => item.gameObject)
            .FirstOrDefault(item => item.name == "Quality" &&
                                    item.GetComponentInChildren<TMP_Dropdown>(true) != null);
        if (qualityGroup == null)
            return 0;

        TMP_Dropdown qualityDropdown = qualityGroup.GetComponentInChildren<TMP_Dropdown>(true);
        TMP_Text qualityLabel = qualityGroup.GetComponentsInChildren<TMP_Text>(true)
            .FirstOrDefault(text => !text.transform.IsChildOf(qualityDropdown.transform));
        if (qualityLabel == null)
            return 0;

        GameObject labelObject = UnityEngine.Object.Instantiate(qualityLabel.gameObject, qualityGroup.transform);
        labelObject.name = "Language";
        TMP_Text groupLabel = labelObject.GetComponent<TMP_Text>();
        groupLabel.text = "Language";
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(labelRect.anchoredPosition.x, -96f);

        foreach (LocalizeStringEvent localizer in labelObject.GetComponents<LocalizeStringEvent>())
            UnityEngine.Object.DestroyImmediate(localizer, true);

        GameObject dropdownObject = UnityEngine.Object.Instantiate(qualityDropdown.gameObject, qualityGroup.transform);
        dropdownObject.name = "Language Dropdown";
        TMP_Dropdown dropdown = dropdownObject.GetComponent<TMP_Dropdown>();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string>
        {
            "English", "Deutsch", "Español", "Français", "Italiano", "日本語", "한국어"
        });
        RectTransform dropdownRect = dropdownObject.GetComponent<RectTransform>();
        dropdownRect.anchoredPosition = new Vector2(dropdownRect.anchoredPosition.x, -160f);
        dropdown.onValueChanged = new TMP_Dropdown.DropdownEvent();
        LanguageDropdown languageDropdown = dropdown.gameObject.AddComponent<LanguageDropdown>();
        languageDropdown.ConfigureFont(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_GarageV2/NewUI/Fonts/Koulen/Koulen-Regular SDF.asset"));

        foreach (LocalizeStringEvent localizer in dropdown.GetComponentsInChildren<LocalizeStringEvent>(true))
            UnityEngine.Object.DestroyImmediate(localizer, true);

        RectTransform groupRect = qualityGroup.GetComponent<RectTransform>();
        groupRect.sizeDelta = new Vector2(groupRect.sizeDelta.x, groupRect.sizeDelta.y + 130f);
        groupRect.anchoredPosition = new Vector2(groupRect.anchoredPosition.x, groupRect.anchoredPosition.y - 65f);

        return 1;
    }

    private static int EnsureControlsHeader(GameObject root)
    {
        const string headerName = "Localized Controls Header";
        Image background = root.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(image => image.sprite != null &&
                                     image.sprite.name.Equals("Controls", StringComparison.OrdinalIgnoreCase));
        if (background == null)
            return 0;

        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_GarageV2/NewUI/Fonts/Koulen/Koulen-Regular SDF.asset");

        Transform existing = root.GetComponentsInChildren<Transform>(true)
            .FirstOrDefault(item => item.name == headerName);
        GameObject header = existing != null
            ? existing.gameObject
            : new GameObject(headerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        header.transform.SetParent(background.transform, false);
        RectTransform headerRect = (RectTransform)header.transform;
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(0f, 1f);
        headerRect.pivot = new Vector2(0f, 1f);
        headerRect.anchoredPosition = new Vector2(67f, -2f);
        headerRect.sizeDelta = new Vector2(225f, 64f);
        Image cover = header.GetComponent<Image>();
        cover.color = new Color32(17, 17, 17, 255);
        cover.raycastTarget = false;

        if (header.transform.Find("Title") == null)
            CreateHeaderText(header.transform, "Title", "CONTROLS", font, 31f,
                new Color32(255, 255, 255, 255), new Vector2(3f, -1f), new Vector2(220f, 37f), 0f);
        if (header.transform.Find("Subtitle") == null)
            CreateHeaderText(header.transform, "Subtitle", "BROWSE CONTROLS", font, 15f,
                new Color32(225, 20, 24, 255), new Vector2(3f, -36f), new Vector2(220f, 24f), 5f);

        foreach (TextMeshProUGUI label in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.enableAutoSizing = true;
            label.fontSizeMin = 7f;
            label.fontSizeMax = Mathf.Max(10f, label.fontSize);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            EditorUtility.SetDirty(label);
        }
        return 1;
    }

    private static void CreateHeaderText(Transform parent, string name, string value, TMP_FontAsset font,
        float fontSize, Color color, Vector2 position, Vector2 size, float characterSpacing)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = fontSize;
        text.characterSpacing = characterSpacing;
        text.raycastTarget = false;
    }

    private static int ProcessScene(string path, IReadOnlyDictionary<string, long> englishToId)
    {
        Scene alreadyLoaded = SceneManager.GetSceneByPath(path);
        bool openedForProcessing = !alreadyLoaded.IsValid() || !alreadyLoaded.isLoaded;
        Scene scene = openedForProcessing ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : alreadyLoaded;
        int changed = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            changed += EnsureBrandedLogos(root);
            changed += ProcessHierarchy(root, englishToId);
        }

        if (changed > 0)
            EditorSceneManager.SaveScene(scene);

        if (openedForProcessing)
            EditorSceneManager.CloseScene(scene, true);

        return changed;
    }

    private static int EnsureBrandedLogos(GameObject root)
    {
        int changed = 0;
        changed += EnsureBrandedLogo(root, "ShopLogo", "Localized Shop Logo", "Shop", "TRUCK SALON", 0.39f);
        changed += EnsureBrandedLogo(root, "CustomizationLogo", "Localized Customization Logo",
            "Customization", "CUSTOMIZE YOUR TRUCK", 0.27f);
        return changed;
    }

    private static int EnsureBrandedLogo(GameObject root, string spriteName, string localizedName,
        string title, string subtitle, float iconWidthRatio)
    {
        Image original = root.GetComponentsInChildren<Image>(true)
            .FirstOrDefault(image => image.sprite != null &&
                                     image.sprite.name.Equals(spriteName, StringComparison.OrdinalIgnoreCase));
        if (original == null)
            return 0;

        Transform existing = original.transform.Find(localizedName);
        if (existing != null)
            return 0;

        var container = new GameObject(localizedName, typeof(RectTransform));
        container.transform.SetParent(original.transform, false);
        RectTransform containerRect = (RectTransform)container.transform;
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = Vector2.zero;
        containerRect.offsetMax = Vector2.zero;

        original.enabled = false;
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_GarageV2/NewUI/Fonts/Koulen/Koulen-Regular SDF.asset");

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(container.transform, false);
        RectTransform iconRect = (RectTransform)icon.transform;
        iconRect.anchorMin = new Vector2(0f, 0f);
        iconRect.anchorMax = new Vector2(iconWidthRatio, 1f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        Image iconImage = icon.GetComponent<Image>();
        iconImage.sprite = GetOrCreateLogoIcon(original.sprite, spriteName);
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        float textStart = iconWidthRatio + 0.02f;
        CreateStretchHeaderText(container.transform, "Title", title, font, 34f, Color.white,
            new Vector2(textStart, 0.42f), new Vector2(1f, 1f), 0f);
        CreateStretchHeaderText(container.transform, "Subtitle", subtitle, font, 15f,
            new Color32(225, 20, 24, 255), new Vector2(textStart, 0f), new Vector2(1f, 0.45f), 4f);
        return 1;
    }

    private static Sprite GetOrCreateLogoIcon(Sprite source, string name)
    {
        string assetPath = $"Assets/_GarageV2/NewUI/Fonts/Fallbacks/{name} Icon.asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        if (existing != null)
            return existing;

        Texture2D texture = source.texture;
        float iconWidth = Mathf.Min(texture.height, texture.width);
        Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, iconWidth, texture.height),
            new Vector2(0.5f, 0.5f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        icon.name = name + " Icon";
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? string.Empty);
        AssetDatabase.CreateAsset(icon, assetPath);
        return icon;
    }

    private static void CreateStretchHeaderText(Transform parent, string name, string value,
        TMP_FontAsset font, float fontSize, Color color, Vector2 anchorMin, Vector2 anchorMax,
        float characterSpacing)
    {
        var child = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        child.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)child.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI text = child.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.enableAutoSizing = true;
        text.fontSizeMin = 7f;
        text.fontSizeMax = fontSize;
        text.enableWordWrapping = false;
        text.characterSpacing = characterSpacing;
        text.raycastTarget = false;
    }

    private static int ProcessHierarchy(GameObject root, IReadOnlyDictionary<string, long> englishToId)
    {
        int changed = 0;

        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            changed += Configure(text.gameObject, text.text, text, englishToId);

        foreach (Text text in root.GetComponentsInChildren<Text>(true))
            changed += Configure(text.gameObject, text.text, text, englishToId);

        return changed;
    }

    private static int Configure(GameObject gameObject, string currentText, Component textComponent,
        IReadOnlyDictionary<string, long> englishToId)
    {
        if (!string.IsNullOrWhiteSpace(currentText) && currentText.All(character => !char.IsLetter(character)))
        {
            LocalizeStringEvent invalidLocalizer = gameObject.GetComponent<LocalizeStringEvent>();
            if (invalidLocalizer != null)
            {
                UnityEngine.Object.DestroyImmediate(invalidLocalizer, true);
                return 1;
            }

            return 0;
        }

        if (string.IsNullOrWhiteSpace(currentText) ||
            !englishToId.TryGetValue(Normalize(currentText), out long entryId))
            return 0;

        LocalizeStringEvent localizer = gameObject.GetComponent<LocalizeStringEvent>();
        bool created = localizer == null;

        if (created)
        {
            localizer = gameObject.AddComponent<LocalizeStringEvent>();
            var setterMethod = textComponent.GetType().GetProperty("text")?.GetSetMethod();
            var setter = setterMethod != null
                ? Delegate.CreateDelegate(typeof(UnityAction<string>), textComponent, setterMethod) as UnityAction<string>
                : null;

            if (setter == null)
                throw new InvalidOperationException($"Could not create a text setter for {textComponent.GetType().Name} on {gameObject.name}.");

            UnityEventTools.AddPersistentListener(localizer.OnUpdateString, setter);
            localizer.OnUpdateString.SetPersistentListenerState(0, UnityEventCallState.EditorAndRuntime);
        }

        bool referenceChanged =
            localizer.StringReference.TableReference.ReferenceType != TableReference.Type.Name ||
            localizer.StringReference.TableReference.TableCollectionName != TableName ||
            localizer.StringReference.TableEntryReference.KeyId != entryId;

        if (referenceChanged)
        {
            localizer.StringReference.TableReference = TableName;
            localizer.StringReference.TableEntryReference = entryId;
            EditorUtility.SetDirty(localizer);
        }

        bool layoutChanged = false;
        if (textComponent is TextMeshProUGUI tmp)
        {
            layoutChanged = !tmp.enableAutoSizing || tmp.fontSizeMin != 7f || tmp.enableWordWrapping;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 7f;
            tmp.fontSizeMax = Mathf.Max(10f, tmp.fontSize);
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            EditorUtility.SetDirty(tmp);
        }

        return created || referenceChanged || layoutChanged ? 1 : 0;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string normalized = value.Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        while (normalized.Contains("  "))
            normalized = normalized.Replace("  ", " ");
        return normalized.TrimEnd(':').Trim();
    }
}
#endif
