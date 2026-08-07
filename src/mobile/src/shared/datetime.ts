const BRAZIL_TZ = 'America/Sao_Paulo';

/** Interpreta timestamps da API (UTC) de forma estável no React Native. */
export function parseApiDate(value: string): Date {
  const trimmed = value.trim();
  // ISO sem fuso: backend grava UtcNow — tratar como UTC.
  if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$/.test(trimmed)) {
    return new Date(`${trimmed}Z`);
  }
  return new Date(trimmed);
}

function pad2(n: number): string {
  return String(n).padStart(2, '0');
}

/**
 * Data/hora em horário de Brasília (America/Sao_Paulo).
 * Não depende do fuso do emulador/aparelho — evita +3h quando o device está em UTC.
 */
export function formatDateTimePtBr(value?: string | null): string {
  if (!value) return '—';
  const d = parseApiDate(value);
  if (Number.isNaN(d.getTime())) return '—';

  try {
    const parts = new Intl.DateTimeFormat('en-GB', {
      timeZone: BRAZIL_TZ,
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hourCycle: 'h23',
    }).formatToParts(d);

    const get = (type: Intl.DateTimeFormatPartTypes) =>
      parts.find((p) => p.type === type)?.value ?? '00';

    return `${get('day')}/${get('month')}/${get('year')}, ${get('hour')}:${get('minute')}:${get('second')}`;
  } catch {
    // Fallback: converte UTC → Brasília (−3h, sem horário de verão desde 2019)
    const brazilMs = d.getTime() - 3 * 60 * 60 * 1000;
    const b = new Date(brazilMs);
    return `${pad2(b.getUTCDate())}/${pad2(b.getUTCMonth() + 1)}/${b.getUTCFullYear()}, ${pad2(b.getUTCHours())}:${pad2(b.getUTCMinutes())}:${pad2(b.getUTCSeconds())}`;
  }
}
