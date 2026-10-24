import { test } from "node:test";
import assert from "node:assert/strict";
import { safeNext } from "../../src/lib/safe-next.ts";
import { selectionIndex } from "../../src/lib/selection-keyboard.ts";

test("post-login redirects reject normalized cross-origin and protected destinations", () => {
  for (const next of ["https://evil.invalid", "//evil.invalid", "/\\evil.invalid", "/\t/evil.invalid", "/\n/evil.invalid", "/\r/evil.invalid", "/%2fevil.invalid", "/%5cevil.invalid", "/%0a/evil.invalid", "/..//evil.invalid", "/%2e%2e//evil.invalid", "/.%2e//evil.invalid", "/../api/session", "/api/session", "/register", "/login?next=/home"]) {
    assert.equal(safeNext(next), null, JSON.stringify(next));
  }
});

test("post-login redirects retain valid routes, queries and anchors", () => {
  assert.equal(safeNext("/cases/123?request=1#follow-up"), "/cases/123?request=1#follow-up");
  assert.equal(safeNext("/cases?q=feed%20intake&source=https%3A%2F%2Fexample.invalid"), "/cases?q=feed%20intake&source=https%3A%2F%2Fexample.invalid");
  assert.equal(safeNext(["/reports", "//evil.invalid"]), "/reports");
  assert.equal(safeNext(undefined), null);
});

test("single-selection keyboard controls wrap and support Home/End", () => {
  assert.equal(selectionIndex("ArrowLeft", 0, 3), 2);
  assert.equal(selectionIndex("ArrowUp", 0, 3), 2);
  assert.equal(selectionIndex("ArrowRight", 2, 3), 0);
  assert.equal(selectionIndex("ArrowDown", 2, 3), 0);
  assert.equal(selectionIndex("Home", 2, 3), 0);
  assert.equal(selectionIndex("End", 0, 3), 2);
  assert.equal(selectionIndex("Tab", 0, 3), null);
  assert.equal(selectionIndex("ArrowRight", 0, 0), null);
});
