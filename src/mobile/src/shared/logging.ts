/** Nunca logar tokens ou headers Authorization. */
export function safeLogError(context: string, error: unknown): void {
  if (!__DEV__) {
    return;
  }

  const message = error instanceof Error ? error.message : String(error);
  const sanitized = message
    .replace(/Bearer\s+[A-Za-z0-9\-._~+/]+=*/gi, 'Bearer [redacted]')
    .replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '[jwt]');

  // console.warn/error abre LogBox em tela cheia no aparelho — usar só log no Metro.
  // eslint-disable-next-line no-console
  console.log(`[Domus:${context}]`, sanitized);
}
