import { eventLegend, runtimeEvents } from '../fixtures/controlCenterMockData';
import { de as t } from '../i18n/de';
import { Panel, StatusPill, toneClass } from './StatusCard';

function eventSeverityTone(severity) {
  switch (severity) {
    case 'critical':
      return 'danger';
    case 'warning':
      return 'warn';
    default:
      return 'info';
  }
}

function eventSeverityLabel(severity) {
  switch (severity) {
    case 'critical':
      return t.eventTimeline.critical;
    case 'warning':
      return t.eventTimeline.warning;
    default:
      return t.eventTimeline.info;
  }
}

function eventCategoryTone(category) {
  switch (category) {
    case 'trading':
      return 'warn';
    case 'learning':
      return 'good';
    default:
      return 'info';
  }
}

function eventCategoryLabel(category) {
  switch (category) {
    case 'trading':
      return t.eventTimeline.trading;
    case 'learning':
      return t.eventTimeline.learning;
    default:
      return t.eventTimeline.runtime;
  }
}

export function EventTimelinePanel() {
  return (
    <Panel
      eyebrow={t.eventTimeline.eyebrow}
      title={t.eventTimeline.title}
      action={<StatusPill tone="info">{t.eventTimeline.status}</StatusPill>}
      className="event-timeline-panel"
    >
      <div className="event-safety-strip">
        <strong>{t.eventTimeline.autoTradingOff}</strong>
        <strong>{t.eventTimeline.humanReviewRequired}</strong>
      </div>
      <div className="event-filter-legend" aria-label={t.eventTimeline.legend}>
        {eventLegend.map((item) => (
          <span className={`event-filter ${toneClass(item.tone)}`} key={item.label}>
            {item.label}
          </span>
        ))}
      </div>
      <div className="event-timeline-list">
        {runtimeEvents.map((event) => (
          <article
            className={`event-timeline-card severity-${event.severity} category-${event.category}`}
            key={event.id}
          >
            <div className="event-time-block">
              <time>{event.time}</time>
              <span>{event.eventType}</span>
            </div>
            <div className="event-main">
              <div className="event-tags">
                <StatusPill tone={eventSeverityTone(event.severity)}>
                  {eventSeverityLabel(event.severity)}
                </StatusPill>
                <StatusPill tone={eventCategoryTone(event.category)}>
                  {eventCategoryLabel(event.category)}
                </StatusPill>
              </div>
              <strong>{event.eventType}</strong>
              <p>{event.description}</p>
            </div>
            <div className="event-source-block">
              <span>{t.eventTimeline.source}</span>
              <strong>{event.source}</strong>
            </div>
          </article>
        ))}
      </div>
    </Panel>
  );
}
