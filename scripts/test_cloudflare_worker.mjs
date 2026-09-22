import assert from "node:assert/strict";
import worker from "../deploy/cloudflare/src/worker.js";

const originalFetch = globalThis.fetch;
let calls = 0;
let observed;
try {
  globalThis.fetch = async (request, options) => {
    calls += 1;
    observed = { request, options };
    return new Response("unity bundle", {
      status: 200,
      headers: { "content-encoding": "gzip", "content-type": "application/octet-stream" },
    });
  };

  const bundle = await worker.fetch(
    new Request("https://riesgus.com/release-20260922/Build/game.wasm.unityweb?seed=7"),
    { AZURE_ORIGIN: "https://riskai-demo.spaincentral.cloudapp.azure.com" },
  );
  assert.equal(bundle.status, 200);
  assert.equal(bundle.headers.get("cache-control"), "public, max-age=31536000, immutable");
  assert.equal(bundle.headers.get("content-encoding"), "gzip");
  assert.equal(bundle.headers.get("content-type"), "application/wasm");
  assert.equal(String(observed.request), "https://riskai-demo.spaincentral.cloudapp.azure.com/release-20260922/Build/game.wasm.unityweb?seed=7");
  assert.equal(observed.options.method, "GET");
  assert.equal(observed.options.redirect, "manual");
  assert.equal(observed.options.headers.get("accept-encoding"), "gzip");
  assert.ok(observed.options.cf.cacheEverything);

  const authorityLikePath = await worker.fetch(
    new Request("https://riesgus.com//evil.example/Build/game.js"),
    { AZURE_ORIGIN: "https://riskai-demo.spaincentral.cloudapp.azure.com" },
  );
  assert.equal(authorityLikePath.status, 200);
  assert.equal(String(observed.request), "https://riskai-demo.spaincentral.cloudapp.azure.com//evil.example/Build/game.js");

  const stableScript = await worker.fetch(
    new Request("https://riesgus.com/Build/stable.js"),
    { AZURE_ORIGIN: "https://riskai-demo.spaincentral.cloudapp.azure.com" },
  );
  assert.equal(stableScript.status, 200);
  assert.notEqual(stableScript.headers.get("cache-control"), "public, max-age=31536000, immutable");

  globalThis.fetch = async () => new Response(null, { status: 304 });
  const notModified = await worker.fetch(
    new Request("https://riesgus.com/release-20260922/Build/game.wasm.unityweb"),
    { AZURE_ORIGIN: "https://riskai-demo.spaincentral.cloudapp.azure.com" },
  );
  assert.equal(notModified.status, 304);

  const httpsRedirect = await worker.fetch(new Request("http://riesgus.com/play?seed=7"), {});
  assert.equal(httpsRedirect.status, 301);
  assert.equal(httpsRedirect.headers.get("location"), "https://riesgus.com/play?seed=7");

  const redirect = await worker.fetch(new Request("https://www.riesgus.com/play?seed=7"), {});
  assert.equal(redirect.status, 301);
  assert.equal(redirect.headers.get("location"), "https://riesgus.com/play?seed=7");

  const method = await worker.fetch(new Request("https://riesgus.com/", { method: "POST" }), {});
  assert.equal(method.status, 405);
  assert.equal(method.headers.get("allow"), "GET, HEAD");

  const traversal = await worker.fetch(new Request("https://riesgus.com/a/%2F..%2Fsecrets"), {});
  assert.equal(traversal.status, 400);
  const doubleTraversal = await worker.fetch(new Request("https://riesgus.com/a/%252F..%252Fsecrets"), {});
  assert.equal(doubleTraversal.status, 400);
  assert.equal(calls, 3);
} finally {
  globalThis.fetch = originalFetch;
}
console.log("Cloudflare worker checks passed");
