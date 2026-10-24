export class BodyTooLargeError extends Error {}

/** Enforce the limit while reading; Content-Length alone can be omitted or forged. */
export async function readBoundedBody(
  request: Request,
  limit: number,
): Promise<ArrayBuffer> {
  if (Number(request.headers.get("content-length")) > limit)
    throw new BodyTooLargeError();
  if (!request.body) return new ArrayBuffer(0);
  const reader = request.body.getReader();
  const chunks: Uint8Array[] = [];
  let size = 0;
  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      size += value.byteLength;
      if (size > limit) {
        await reader.cancel();
        throw new BodyTooLargeError();
      }
      chunks.push(value);
    }
  } finally {
    reader.releaseLock();
  }
  const result = new Uint8Array(size);
  let offset = 0;
  for (const chunk of chunks) {
    result.set(chunk, offset);
    offset += chunk.byteLength;
  }
  return result.buffer;
}
