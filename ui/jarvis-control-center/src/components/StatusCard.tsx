export function toneClass(tone) {
  return `tone-${tone || 'info'}`;
}

export function StatusPill({ children, tone = 'info' }) {
  return <span className={`status-pill ${toneClass(tone)}`}>{children}</span>;
}

export function Panel({ eyebrow, title, action, children, className = '' }) {
  return (
    <section className={`panel ${className}`}>
      <div className="panel-header">
        <div>
          <p className="eyebrow">{eyebrow}</p>
          <h2>{title}</h2>
        </div>
        {action}
      </div>
      {children}
    </section>
  );
}

export function MetricGrid({ items }) {
  return (
    <div className="metric-grid">
      {items.map((item) => (
        <div className="metric" key={item.label}>
          <span>{item.label}</span>
          <strong className={toneClass(item.tone)}>{item.value}</strong>
        </div>
      ))}
    </div>
  );
}
