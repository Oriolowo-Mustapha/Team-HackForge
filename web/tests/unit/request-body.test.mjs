import { test } from "node:test";
import assert from "node:assert/strict";
import {
  BodyTooLargeError,
  readBoundedBody,
} from "../../src/lib/server/request-body.ts";

test("reads chunked uploads up to the limit", async () => {
  const request = new Request("http://localhost", {
    method: "POST",
    body: new ReadableStream({
      start(controller) {
        controller.enqueue(new Uint8Array([1, 2]));
        controller.enqueue(new Uint8Array([3]));
        controller.close();
      },
    }),
    duplex: "half",
  });
  assert.deepEqual(
    [...new Uint8Array(await readBoundedBody(request, 3))],
    [1, 2, 3],
  );
});

test("cancels oversized chunked uploads without trusting Content-Length", async () => {
  let cancelled = false;
  const request = new Request("http://localhost", {
    method: "POST",
    body: new ReadableStream({
      start(controller) {
        controller.enqueue(new Uint8Array(6));
      },
      cancel() {
        cancelled = true;
      },
    }),
    duplex: "half",
  });
  await assert.rejects(() => readBoundedBody(request, 5), BodyTooLargeError);
  assert.equal(cancelled, true);
});

test("rejects an oversized declared body", async () => {
  const request = new Request("http://localhost", {
    method: "POST",
    headers: { "content-length": "100" },
    body: "test",
  });
  await assert.rejects(() => readBoundedBody(request, 5), BodyTooLargeError);
});
