"use client";

import { useEffect, useRef, useState } from "react";

/**
 * A blob: URL for previewing a local file. The previous URL is revoked when the blob changes; the
 * last one is released by the browser when the page unloads.
 */
export function useObjectUrl(blob: Blob | null): string | null {
  const [entry, setEntry] = useState<{ blob: Blob | null; url: string | null }>({ blob: null, url: null });
  if (entry.blob !== blob) setEntry({ blob, url: blob ? URL.createObjectURL(blob) : null });

  const previous = useRef<string | null>(null);
  useEffect(() => {
    const old = previous.current;
    previous.current = entry.url;
    if (old && old !== entry.url) URL.revokeObjectURL(old);
  }, [entry.url]);

  return entry.url;
}
