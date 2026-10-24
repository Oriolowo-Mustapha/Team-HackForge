/** Accept only normalized, same-origin application routes after authentication. */
export function safeNext(value: string | string[] | undefined): string | null {
  const next = Array.isArray(value) ? value[0] : value;
  if (!next || !next.startsWith("/") || /[\u0000-\u0020\u007f\\]/.test(next)) return null;
  // Reject encoded controls and separators as well; routers may decode them before navigation.
  if (/%(?:0[0-9a-f]|1[0-9a-f]|7f)/i.test(next)) return null;
  try {
    const base = "https://farmhand.invalid";
    const parsed = new URL(next, base);
    if (parsed.origin !== base) return null;
    if (parsed.pathname.startsWith("//")) return null;
    if (/%(?:2f|5c)/i.test(parsed.pathname)) return null;
    if (/^\/(?:login|register|api)(?:\/|$)/i.test(parsed.pathname)) return null;
    return `${parsed.pathname}${parsed.search}${parsed.hash}`;
  } catch {
    return null;
  }
}
