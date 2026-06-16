const OPERATOR_TRANSLATIONS = {
  ok: {
    title: 'Alles ok',
    meaning: 'Hermes arbeitet im Normalmodus.',
    action: 'Keine Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'arbeitet normal',
    franksAction: 'Nein',
  },
  ready: {
    title: 'Bereit',
    meaning: 'Hermes ist bereit für die nächste Aufgabe.',
    action: 'Keine Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'steht bereit',
    franksAction: 'Nein',
  },
  no_action_required: {
    title: 'Keine Aktion erforderlich',
    meaning: 'Hermes arbeitet selbstständig weiter.',
    action: 'Keine Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'arbeitet selbstständig weiter',
    franksAction: 'Nein',
  },
  continue: {
    title: 'Weiterlaufen',
    meaning: 'Hermes kann normal weiterarbeiten.',
    action: 'Keine Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'arbeitet normal weiter',
    franksAction: 'Nein',
  },
  pause_research: {
    title: 'Forschung pausiert',
    meaning: 'Hermes pausiert sichere Forschungsarbeit vorübergehend.',
    action: 'Keine Aktion erforderlich.',
    severity: 'warn',
    whatHermesDoes: 'wartet auf bessere Ressourcenlage',
    franksAction: 'Nein',
  },
  plan_cleanup: {
    title: 'Cleanup planen',
    meaning: 'Hermes hat erkannt, dass Speicherpflege sinnvoll sein könnte.',
    action: 'Speicherstatus prüfen, bei Bedarf Cleanup planen.',
    severity: 'hint',
    whatHermesDoes: 'plant sichere Speicherpflege',
    franksAction: 'Ja, Speicher prüfen',
  },
  safe_stop: {
    title: 'Sicherer Stopp',
    meaning: 'Hermes wurde aus Sicherheitsgründen angehalten.',
    action: 'Keine Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wartet auf eine neue Freigabe',
    franksAction: 'Nein',
  },
  outside_nightly_window: {
    title: 'Außerhalb des Nachtfensters',
    meaning: 'Hermes befindet sich aktuell außerhalb des erlaubten Nachtlaufs.',
    action: 'Keine Aktion erforderlich. Hermes startet automatisch im nächsten Zeitfenster.',
    severity: 'hint',
    whatHermesDoes: 'wartet auf das nächste erlaubte Zeitfenster',
    franksAction: 'Nein',
  },
  safe_stop_requested: {
    title: 'Sicherer Stopp angefordert',
    meaning: 'Hermes wartet auf einen sicheren Haltepunkt.',
    action: 'Keine Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wartet auf einen sicheren Haltepunkt',
    franksAction: 'Nein',
  },
  storage_cleanup_candidates: {
    title: 'Speicher aufräumen wäre sinnvoll',
    meaning: 'Hermes hat Dateien gefunden, die sicher bereinigt werden könnten.',
    action: 'Nur ausführen, wenn Speicher knapp wird.',
    severity: 'hint',
    whatHermesDoes: 'bereitet mögliche Speicherbereinigung vor',
    franksAction: 'Ja, Speicher prüfen',
  },
  validation_queue_missing: {
    title: 'Validierungswarteschlange fehlt',
    meaning: 'Hermes hat Wissenslücken erkannt, aber keine passende Prüfwarteschlange gefunden.',
    action: 'Knowledge Validation Pipeline prüfen.',
    severity: 'warn',
    whatHermesDoes: 'wartet auf eine passende Validierungswarteschlange',
    franksAction: 'Ja, Konfiguration prüfen',
  },
  oos_data_missing: {
    title: 'Zusätzliche Testdaten werden benötigt',
    meaning: 'Einige Wissenselemente benötigen weitere Out-of-Sample-Validierung.',
    action: 'Keine direkte Aktion erforderlich. Hermes kann weitere Validierungsläufe planen.',
    severity: 'warn',
    whatHermesDoes: 'plant weitere Validierung',
    franksAction: 'Nein',
  },
  human_review_needs_more_evidence: {
    title: 'Hermes sammelt weitere Evidenz',
    meaning: 'Ein Teil des Wissens wartet auf zusätzliche Evidenz und Validierung.',
    action: 'Keine Aktion für Frank. Hermes kann weitere Evidenz- und Validierungsläufe planen.',
    severity: 'warn',
    whatHermesDoes: 'sammelt weitere Evidenz',
    franksAction: 'Nein',
  },
  knowledge_items_need_oos_validation: {
    title: 'Zusätzliche Testdaten werden benötigt',
    meaning: 'Einige Wissenselemente benötigen weitere Out-of-Sample-Validierung.',
    action: 'Hermes plant weitere OOS-Validierungsläufe.',
    severity: 'warn',
    whatHermesDoes: 'plant weitere OOS-Validierung',
    franksAction: 'Nein',
  },
  knowledge_validation_queue_missing: {
    title: 'Validierungswarteschlange fehlt',
    meaning: 'Hermes hat Wissen erkannt, aber keine passende Prüfwarteschlange gefunden.',
    action: 'Knowledge Validation Pipeline prüfen.',
    severity: 'warn',
    whatHermesDoes: 'sucht nach einer passenden Validierungswarteschlange',
    franksAction: 'Ja, Konfiguration prüfen',
  },
  hypotheses_without_validation_queue: {
    title: 'Hypothesen warten auf Validierung',
    meaning: 'Es gibt Hypothesen, aber noch nicht genug passende Queue-Arbeit.',
    action: 'Hermes überführt Hypothesen in die bestehende Validierungswarteschlange.',
    severity: 'warn',
    whatHermesDoes: 'überführt Hypothesen in die Validierungswarteschlange',
    franksAction: 'Nein',
  },
  no_robust_strategies: {
    title: 'Noch keine ausreichend robuste Strategie',
    meaning: 'Es liegt noch keine robuste Strategie im Status vor.',
    action: 'Hermes plant den nächsten Research-/Robustness-Lauf.',
    severity: 'warn',
    whatHermesDoes: 'plant Research- und Robustness-Läufe',
    franksAction: 'Nein',
  },
  knowledge_validation_audit: {
    title: 'Validierung wird ausgewertet',
    meaning: 'Hermes analysiert offene Validierungen und Wissenslücken.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wertet Validation-Lücken aus',
    franksAction: 'Nein',
  },
  validation_queue_refill: {
    title: 'Validation Queue wird aufgefüllt',
    meaning: 'Hermes überführt offene Validierungspläne in konkrete Aufgaben.',
    action: 'Keine Aktion für Frank. Hermes kann die Queue selbst nachfüllen.',
    severity: 'info',
    whatHermesDoes: 'füllt die Validation Queue auf',
    franksAction: 'Nein',
  },
  evidence_validation_runner: {
    title: 'Evidenz wird gesammelt',
    meaning: 'Hermes führt sichere Validierungsaufgaben aus und sammelt Evidenz.',
    action: 'Keine Aktion für Frank. Hermes baut Evidenz weiter aus.',
    severity: 'info',
    whatHermesDoes: 'sammelt Evidenz',
    franksAction: 'Nein',
  },
  autonomous_improvement_queue: {
    title: 'Selbstverbesserung läuft',
    meaning: 'Hermes erzeugt und verfolgt konkrete Verbesserungsaufgaben.',
    action: 'Keine direkte Aktion erforderlich. Hermes arbeitet die Queue selbst ab.',
    severity: 'good',
    whatHermesDoes: 'arbeitet Verbesserungsaufgaben selbstständig ab',
    franksAction: 'Nein',
  },
  gather_more_evidence: {
    title: 'Mehr Evidenz sammeln',
    meaning: 'Hermes erweitert die Evidenzbasis für Wissen und Kandidaten.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'sammelt zusätzliche Evidenz',
    franksAction: 'Nein',
  },
  source_expansion: {
    title: 'Quellen erweitern',
    meaning: 'Hermes sucht zusätzliche und bessere Quellen.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'erweitert die Quellenbasis',
    franksAction: 'Nein',
  },
  schedule_revalidation: {
    title: 'Re-Validierung planen',
    meaning: 'Hermes plant weitere Validierungs- und OOS-Läufe.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'plant Re-Validierungsfenster',
    franksAction: 'Nein',
  },
  wartet_auf_nightly: {
    title: 'Wartet auf Nightly',
    meaning: 'Hermes darf den Lauf erst im erlaubten Nightly-Fenster starten.',
    action: 'Keine Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wartet auf das nächste Nightly-Fenster',
    franksAction: 'Nein',
  },
  resourceguard_signal: {
    title: 'Ressourcenschutz aktiv',
    meaning: 'Hermes wartet auf sichere Ressourcenbedingungen.',
    action: 'Keine Aktion erforderlich.',
    severity: 'warn',
    whatHermesDoes: 'wartet auf bessere Ressourcenlage',
    franksAction: 'Nein',
  },
  contradiction_analysis: {
    title: 'Widersprüche prüfen',
    meaning: 'Hermes untersucht aktive Widersprüche in Wissen und Strategien.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'warn',
    whatHermesDoes: 'analysiert widersprüchliche Signale',
    franksAction: 'Nein',
  },
  validation_queue_repair: {
    title: 'Validation Queue reparieren',
    meaning: 'Hermes stellt die Validierungswarteschlange wieder her.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'warn',
    whatHermesDoes: 'repariert die Validierungswarteschlange',
    franksAction: 'Nein',
  },
  cleanup_plan_update: {
    title: 'Systempflege',
    meaning: 'Hermes aktualisiert den Cleanup-Plan.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'aktualisiert den Cleanup-Plan',
    franksAction: 'Nein',
  },
  evidenz_sammeln: {
    title: 'Evidenz sammeln',
    meaning: 'Hermes sammelt zusätzliche Evidenz.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'sammelt Evidenz',
    franksAction: 'Nein',
  },
  quellen_erweitern: {
    title: 'Quellen erweitern',
    meaning: 'Hermes erweitert die Quellenbasis.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'erweitert Quellen',
    franksAction: 'Nein',
  },
  re_validierung: {
    title: 'Re-Validierung',
    meaning: 'Hermes plant weitere Validierungsläufe.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'plant Re-Validierung',
    franksAction: 'Nein',
  },
  widersprueche_pruefen: {
    title: 'Widersprüche prüfen',
    meaning: 'Hermes analysiert widersprüchliche Hinweise.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'warn',
    whatHermesDoes: 'prüft Widersprüche',
    franksAction: 'Nein',
  },
  systempflege: {
    title: 'Systempflege',
    meaning: 'Hermes hält die interne Pflege in Ordnung.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'führt Systempflege aus',
    franksAction: 'Nein',
  },
  autonomous_improvement_execution: {
    title: 'Selbstverbesserung wird ausgeführt',
    meaning: 'Hermes arbeitet sichere Verbesserungsaufgaben nacheinander ab.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'führt sichere Verbesserungsaufgaben aus',
    franksAction: 'Nein',
  },
  work_area_policy: {
    title: 'Arbeitsbereichs-Policy',
    meaning: 'Hermes prüft, wann Selbstverbesserung automatisch laufen darf.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'prüft Ausführungsfenster und Ressourcenschutz',
    franksAction: 'Nein',
  },
  work_area_executor_policy: {
    title: 'Arbeitsbereichs-Ausführungsregeln',
    meaning: 'Hermes entscheidet je Arbeitsbereich über Ausführung, Planung und Fenster.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wendet die Ausführungsregeln für Selbstverbesserung an',
    franksAction: 'Nein',
  },
  automatisch_erlaubt: {
    title: 'Automatisch erlaubt',
    meaning: 'Diese Aufgabe darf Hermes selbstständig ausführen.',
    action: 'Keine Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'führt erlaubte Arbeit selbstständig aus',
    franksAction: 'Nein',
  },
  geplant: {
    title: 'Geplant',
    meaning: 'Die Aufgabe ist vorgesehen, aber noch nicht gestartet.',
    action: 'Keine Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'hält die Aufgabe in der Planung',
    franksAction: 'Nein',
  },
  ausgefuehrt: {
    title: 'Ausgeführt',
    meaning: 'Die Aufgabe wurde in einem sicheren Rahmen ausgeführt.',
    action: 'Keine Aktion erforderlich.',
    severity: 'good',
    whatHermesDoes: 'hat die Aufgabe ausgeführt',
    franksAction: 'Nein',
  },
  trusted_knowledge_review_gate: {
    title: 'Trusted-Freigabe wird vorbereitet',
    meaning: 'Hermes hat mögliche Trusted-Kandidaten erkannt, gibt sie aber nicht selbst frei.',
    action: 'Im Prüfzentrum prüfen.',
    severity: 'action',
    whatHermesDoes: 'bereitet Trusted-Kandidaten für die menschliche Freigabe vor',
    franksAction: 'Ja, im Prüfzentrum',
  },
  knowledge_trust_improvement_plan: {
    title: 'Vertrauensverbesserungen laufen',
    meaning: 'Hermes arbeitet daran, Trust-Kandidaten aufzubauen.',
    action: 'Keine direkte Aktion erforderlich.',
    severity: 'warn',
    whatHermesDoes: 'plant Evidenz-, Validierungs- und Quellenarbeit',
    franksAction: 'Nein',
  },
  human_review_required: {
    title: 'Menschliche Entscheidung erforderlich',
    meaning: 'Hermes darf diese Vertrauensstufe nicht selbst freigeben.',
    action: 'Im Prüfzentrum entscheiden.',
    severity: 'action',
    whatHermesDoes: 'wartet auf eine Freigabe im Prüfzentrum',
    franksAction: 'Ja, im Prüfzentrum',
  },
  review_required: {
    title: 'Entscheidungen erforderlich',
    meaning: 'Hermes wartet auf eine menschliche Entscheidung.',
    action: 'Im Prüfzentrum entscheiden.',
    severity: 'action',
    whatHermesDoes: 'wartet auf eine menschliche Entscheidung',
    franksAction: 'Ja, im Prüfzentrum',
  },
  evidence_requested: {
    title: 'Mehr Evidenz angefordert',
    meaning: 'Hermes benötigt zusätzliche Evidenz, bevor es weitergeht.',
    action: 'Mehr Evidenz sammeln oder im Prüfzentrum prüfen.',
    severity: 'warn',
    whatHermesDoes: 'sammelt oder erwartet zusätzliche Evidenz',
    franksAction: 'Ja, Konfiguration prüfen',
  },
  deferred_reviews: {
    title: 'Prüfungen zurückgestellt',
    meaning: 'Einige Prüfungen wurden bewusst später eingeplant.',
    action: 'Keine Aktion erforderlich, sofern keine Frist überschritten ist.',
    severity: 'hint',
    whatHermesDoes: 'führt zurückgestellte Prüfungen später fort',
    franksAction: 'Nein',
  },
  outside_work_window: {
    title: 'Außerhalb des Arbeitsfensters',
    meaning: 'Hermes wartet auf das nächste erlaubte Arbeitsfenster.',
    action: 'Keine Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wartet auf das nächste Arbeitsfenster',
    franksAction: 'Nein',
  },
  stopped_by_stop_request: {
    title: 'Sicher angehalten',
    meaning: 'Hermes wurde bewusst angehalten und läuft nicht aktiv.',
    action: 'Keine Aktion erforderlich.',
    severity: 'hint',
    whatHermesDoes: 'wartet auf eine neue Freigabe',
    franksAction: 'Nein',
  },
  storage_cleanup_candidates_pending: {
    title: 'Speicherbereinigung vorbereitbar',
    meaning: 'Hermes hat bereinigbare Dateien gefunden.',
    action: 'Nur bei knapperem Speicher aktivieren.',
    severity: 'hint',
    whatHermesDoes: 'sammelt bereinigbare Dateien',
    franksAction: 'Ja, Speicher prüfen',
  },
};

const FALLBACK_TRANSLATIONS = [
  {
    match: /outside_nightly_window/i,
    value: OPERATOR_TRANSLATIONS.outside_nightly_window,
  },
  {
    match: /safe_stop_requested/i,
    value: OPERATOR_TRANSLATIONS.safe_stop_requested,
  },
  {
    match: /stopped_by_stop_request/i,
    value: OPERATOR_TRANSLATIONS.stopped_by_stop_request,
  },
  {
    match: /storage_cleanup_candidates/i,
    value: OPERATOR_TRANSLATIONS.storage_cleanup_candidates,
  },
  {
    match: /oos_data_missing/i,
    value: OPERATOR_TRANSLATIONS.oos_data_missing,
  },
  {
    match: /human_review_needs_more_evidence/i,
    value: OPERATOR_TRANSLATIONS.human_review_needs_more_evidence,
  },
  {
    match: /knowledge_validation_queue_missing/i,
    value: OPERATOR_TRANSLATIONS.knowledge_validation_queue_missing,
  },
  {
    match: /hypotheses_without_validation_queue/i,
    value: OPERATOR_TRANSLATIONS.hypotheses_without_validation_queue,
  },
  {
    match: /no_robust_strategies/i,
    value: OPERATOR_TRANSLATIONS.no_robust_strategies,
  },
  {
    match: /knowledge_validation_audit/i,
    value: OPERATOR_TRANSLATIONS.knowledge_validation_audit,
  },
  {
    match: /autonomous_improvement_queue/i,
    value: OPERATOR_TRANSLATIONS.autonomous_improvement_queue,
  },
  {
    match: /validation_queue_missing/i,
    value: OPERATOR_TRANSLATIONS.validation_queue_missing,
  },
  {
    match: /human_review_required/i,
    value: OPERATOR_TRANSLATIONS.human_review_required,
  },
  {
    match: /review_required/i,
    value: OPERATOR_TRANSLATIONS.review_required,
  },
  {
    match: /evidence_requested/i,
    value: OPERATOR_TRANSLATIONS.evidence_requested,
  },
  {
    match: /deferred_reviews/i,
    value: OPERATOR_TRANSLATIONS.deferred_reviews,
  },
  {
    match: /outside_work_window/i,
    value: OPERATOR_TRANSLATIONS.outside_work_window,
  },
  {
    match: /autonomous[_\-\s]?improvement[_\-\s]?execution/i,
    value: OPERATOR_TRANSLATIONS.autonomous_improvement_execution,
  },
  {
    match: /gather[_\-\s]?more[_\-\s]?evidence/i,
    value: OPERATOR_TRANSLATIONS.gather_more_evidence,
  },
  {
    match: /source[_\-\s]?expansion/i,
    value: OPERATOR_TRANSLATIONS.source_expansion,
  },
  {
    match: /schedule[_\-\s]?revalidation/i,
    value: OPERATOR_TRANSLATIONS.schedule_revalidation,
  },
  {
    match: /contradiction[_\-\s]?analysis/i,
    value: OPERATOR_TRANSLATIONS.contradiction_analysis,
  },
  {
    match: /validation[_\-\s]?queue[_\-\s]?repair/i,
    value: OPERATOR_TRANSLATIONS.validation_queue_repair,
  },
  {
    match: /cleanup[_\-\s]?plan[_\-\s]?update/i,
    value: OPERATOR_TRANSLATIONS.cleanup_plan_update,
  },
  {
    match: /evidenz[_\-\s]?sammeln/i,
    value: OPERATOR_TRANSLATIONS.evidenz_sammeln,
  },
  {
    match: /quellen[_\-\s]?erweitern/i,
    value: OPERATOR_TRANSLATIONS.quellen_erweitern,
  },
  {
    match: /re[_\-\s]?validierung/i,
    value: OPERATOR_TRANSLATIONS.re_validierung,
  },
  {
    match: /widerspr[\u00fcu]che[_\-\s]?pr[\u00fc]fen/i,
    value: OPERATOR_TRANSLATIONS.widersprueche_pruefen,
  },
  {
    match: /systempflege/i,
    value: OPERATOR_TRANSLATIONS.systempflege,
  },
  {
    match: /trusted[_\-\s]?knowledge[_\-\s]?review[_\-\s]?gate/i,
    value: OPERATOR_TRANSLATIONS.trusted_knowledge_review_gate,
  },
  {
    match: /knowledge[_\-\s]?trust[_\-\s]?improvement[_\-\s]?plan/i,
    value: OPERATOR_TRANSLATIONS.knowledge_trust_improvement_plan,
  },
  {
    match: /stopped_by_stop_request/i,
    value: OPERATOR_TRANSLATIONS.stopped_by_stop_request,
  },
];

function normalizeKey(value) {
  return String(value || '').trim().toLowerCase();
}

export function translateOperatorCode(code, fallback = null) {
  const key = normalizeKey(code);
  if (!key) {
    return fallback || {
      title: 'Technische Information',
      meaning: 'Hermes liefert dazu derzeit nur technische Statusdaten.',
      action: 'Nur bei Bedarf technische Details prüfen.',
      severity: 'hint',
      whatHermesDoes: 'arbeitet mit technischen Statusdaten',
      franksAction: 'Nein',
    };
  }

  if (OPERATOR_TRANSLATIONS[key]) {
    return OPERATOR_TRANSLATIONS[key];
  }

  const found = FALLBACK_TRANSLATIONS.find((entry) => entry.match.test(key));
  if (found) {
    return found.value;
  }

  return fallback || {
    title: String(code),
    meaning: 'Technischer Status ohne hinterlegte Operator-Übersetzung.',
    action: 'Technische Details bei Bedarf öffnen.',
    severity: 'hint',
    whatHermesDoes: 'arbeitet mit einem internen Status',
    franksAction: 'Nein',
  };
}

export function operatorTrafficLight(severity) {
  const normalized = normalizeKey(severity);

  if (normalized === 'action' || normalized === 'warn' || normalized === 'warning') {
    return { label: 'Frank muss entscheiden', tone: 'warn', symbol: '🔴' };
  }

  if (normalized === 'critical' || normalized === 'danger') {
    return { label: 'Frank muss entscheiden', tone: 'danger', symbol: '🔴' };
  }

  if (normalized === 'good' || normalized === 'ok' || normalized === 'ready') {
    return { label: 'Alles ok', tone: 'good', symbol: '🟢' };
  }

  if (normalized === 'hint' || normalized === 'info') {
    return { label: 'Nur technische Information', tone: 'info', symbol: '⚫' };
  }

  return { label: 'Alles ok', tone: 'good', symbol: '🟢' };
}

export function describeMustFrankAct(actionText) {
  const text = normalizeKey(actionText);

  if (!text || text === 'nein' || text === 'keine' || text === 'keine aktion erforderlich.') {
    return 'Nein';
  }

  if (text.includes('speicher')) {
    return 'Ja, Speicher prüfen';
  }

  if (text.includes('konfiguration') || text.includes('validierungs')) {
    return 'Ja, Konfiguration prüfen';
  }

  if (text.includes('prüfzentrum') || text.includes('prufzentrum') || text.includes('entscheidung')) {
    return 'Ja, im Prüfzentrum';
  }

  return 'Ja';
}

export function compactOperatorExplanation(item) {
  return [
    `Bedeutung: ${item.meaning}`,
    `Hermes arbeitet an: ${item.whatHermesDoes}`,
    `Aktion für Frank: ${item.franksAction}`,
  ];
}
