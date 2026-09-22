const DEFAULT_ORIGIN = "https://riskai-demo.spaincentral.cloudapp.azure.com";
const IMMUTABLE_SECONDS = 31536000;
const MAX_PATH_LENGTH = 4096;
const CONTROL_OR_BACKSLASH = /[\u0000-\u001f\u007f\\]/;
const RELEASE_PATH = /^\/release-[A-Za-z0-9][A-Za-z0-9_-]{5,79}\//;

function parseOrigin(raw) {
  if (typeof raw !== "string" || raw.length > 256) {
    throw new Error("Invalid origin");
  }
  const origin = new URL(raw);
  if (
    origin.protocol !== "https:" ||
    origin.username ||
    origin.password ||
    origin.pathname !== "/" ||
    origin.search ||
    origin.hash
  ) {
    throw new Error("Origin must be a bare HTTPS origin");
  }
  return origin;
}

function buildTarget(incoming, origin) {
  const { pathname, search } = incoming;
  if (
    !pathname ||
    pathname.length > MAX_PATH_LENGTH ||
    CONTROL_OR_BACKSLASH.test(pathname)
  ) {
    throw new Error("Unsafe path");
  }
  let decoded = pathname;
  try {
    // Decode a bounded number of layers so double-encoded traversal cannot
    // reach an origin that normalizes more aggressively than the Worker.
    for (let i = 0; i < 3; i += 1) {
      const next = decodeURIComponent(decoded);
      if (next === decoded) break;
      decoded = next;
    }
  } catch {
    throw new Error("Malformed path");
  }
  if (decoded.split("/").some((part) => part === "..")) {
    throw new Error("Path traversal");
  }
  // Assign components on a URL already rooted at the pinned origin. Passing
  // `pathname + search` as the first URL argument would interpret `//host` as
  // a new authority and turn the proxy into an SSRF primitive.
  const target = new URL(origin.href);
  target.pathname = pathname;
  target.search = search;
  if (target.origin !== origin.origin) {
    throw new Error("Origin changed");
  }
  return target;
}

function isImmutableAsset(pathname) {
  // Azure's deployer gives every release an immutable, validated prefix. Do
  // not pin a stable `/Build/foo.js` or `/index.html` at the edge.
  return RELEASE_PATH.test(pathname);
}

function setMimes(headers, pathname) {
  const normalized = pathname.endsWith(".unityweb")
    ? pathname.slice(0, -".unityweb".length)
    : pathname;
  if (normalized.endsWith(".wasm")) {
    headers.set("content-type", "application/wasm");
  } else if (normalized.endsWith(".js")) {
    headers.set("content-type", "text/javascript; charset=utf-8");
  }
}

function clientResponse(response, pathname) {
  const headers = new Headers(response.headers);
  if (response.ok && isImmutableAsset(pathname)) {
    headers.set(
      "cache-control",
      `public, max-age=${IMMUTABLE_SECONDS}, immutable`,
    );
  } else if (pathname === "/" || pathname.endsWith(".html")) {
    // The Azure deployer writes release-prefixed asset URLs into this file.
    // Never let the edge pin an index that points at an old release.
    headers.set("cache-control", "no-store, max-age=0");
  }
  setMimes(headers, pathname);
  if (headers.get("content-encoding")) {
    const vary = headers.get("vary");
    if (!vary || !/\baccept-encoding\b/i.test(vary)) {
      headers.set("vary", vary ? `${vary}, Accept-Encoding` : "Accept-Encoding");
    }
  }
  headers.set("x-content-type-options", "nosniff");
  headers.set("referrer-policy", "no-referrer");
  return new Response(response.body, {
    status: response.status,
    statusText: response.statusText,
    headers,
  });
}

function rejected(message, status = 400) {
  const headers = {
    "content-type": "text/plain; charset=utf-8",
    "cache-control": "no-store",
    "x-content-type-options": "nosniff",
  };
  if (status === 405) headers.allow = "GET, HEAD";
  return new Response(`${message}\n`, {
    status,
    headers,
  });
}

export default {
  async fetch(request, env) {
    if (request.method !== "GET" && request.method !== "HEAD") {
      return rejected("Method Not Allowed", 405);
    }

    const incoming = new URL(request.url);
    if (incoming.protocol !== "https:") {
      return Response.redirect(
        `https://${incoming.host}${incoming.pathname}${incoming.search}`,
        301,
      );
    }
    if (incoming.hostname.toLowerCase() === "www.riesgus.com") {
      return Response.redirect(
        `https://riesgus.com${incoming.pathname}${incoming.search}`,
        301,
      );
    }

    let origin;
    let target;
    try {
      origin = parseOrigin(env?.AZURE_ORIGIN || DEFAULT_ORIGIN);
      target = buildTarget(incoming, origin);
    } catch {
      return rejected("Bad Request", 400);
    }

    const headers = new Headers(request.headers);
    // The origin host is selected from the pinned URL above, never from client
    // input. Forwarding gzip avoids buffering the Unity bundles in the Worker.
    headers.delete("host");
    headers.delete("content-length");
    headers.set("accept-encoding", "gzip");
    const options = {
      method: request.method,
      headers,
      redirect: "manual",
    };
    if (isImmutableAsset(incoming.pathname)) {
      options.cf = {
        cacheEverything: true,
        cacheTtlByStatus: { "200-299": IMMUTABLE_SECONDS, "404": 0, "500-599": 0 },
      };
    }

    try {
      const response = await globalThis.fetch(target, options);
      // Do not leak a Location header that points clients at the Azure origin.
      if (response.status >= 300 && response.status < 400 && response.status !== 304) {
        return rejected("Bad Gateway", 502);
      }
      return clientResponse(response, incoming.pathname);
    } catch {
      return rejected("Bad Gateway", 502);
    }
  },
};
