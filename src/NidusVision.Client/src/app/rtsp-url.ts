export function redactRtspUrl(url: string, hasStoredCredentials = false): string {
  const trimmed = url.trim();
  const schemeSep = trimmed.indexOf('://');
  if (schemeSep < 0) {
    return trimmed;
  }

  const rest = trimmed.slice(schemeSep + 3);
  const slash = rest.indexOf('/');
  const authority = slash < 0 ? rest : rest.slice(0, slash);
  const path = slash < 0 ? '' : rest.slice(slash);
  const at = authority.lastIndexOf('@');
  const host = at < 0 ? authority : authority.slice(at + 1);
  const stripped = `${trimmed.slice(0, schemeSep + 3)}${host}${path}`;
  if (at < 0 && !hasStoredCredentials) {
    return stripped;
  }

  return `${trimmed.slice(0, schemeSep + 3)}***@${host}${path}`;
}
