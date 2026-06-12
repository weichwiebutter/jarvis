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
  knowledge_validation_queue_missing: {
    title: 'Validierungswarteschlange fehlt',
    meaning: 'Hermes hat Wissen erkannt, aber keine passende Prüfwarteschlange gefunden.',
    action: 'Knowledge Validation Pipeline prüfen.',
    severity: 'warn',
    whatHermesDoes: 'sucht nach einer passenden Validierungswarteschlange',
    franksAction: 'Ja, Konfiguration prüfen',
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
    match: /storage_cleanup_candidates/i,
    value: OPERATOR_TRANSLATIONS.storage_cleanup_candidates,
  },
  {
    match: /oos_data_missing/i,
    value: OPERATOR_TRANSLATIONS.oos_data_missing,
  },
  {
    match: /knowledge_validation_queue_missing/i,
    value: OPERATOR_TRANSLATIONS.knowledge_validation_queue_missing,
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
