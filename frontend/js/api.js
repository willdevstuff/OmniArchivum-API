const isLocal = window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1";
const API_BASE = isLocal
  ? "http://localhost:5000/api"
  : "https://omniarchivum-api.proudmoss-5344fb15.uksouth.azurecontainerapps.io/api";

const TOKEN_STORAGE_KEY = "omniarchivum.session";

// Every request carries a session token that tells the API which archive to serve.
// Tokens go in the Authorization header rather than a cookie: the frontend and API are
// on different sites, so a cookie here would be a third-party cookie — blocked by Safari
// and being phased out elsewhere.
let sessionToken = null;
// Shared so several parallel 401s trigger one session request, not one each.
let pendingSession = null;

function readStoredToken() {
  try {
    return window.localStorage.getItem(TOKEN_STORAGE_KEY);
  } catch {
    // Private browsing modes can throw on storage access; falling back to an in-memory
    // token still works, it just doesn't survive a reload.
    return null;
  }
}

function storeToken(token) {
  sessionToken = token;
  try {
    window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
  } catch {
    /* in-memory only */
  }
}

async function startGuestSession() {
  const res = await fetch(`${API_BASE}/session/guest`, { method: "POST" });
  if (!res.ok) {
    throw new Error(`Couldn't start a session (${res.status})`);
  }
  const session = await res.json();
  storeToken(session.token);
  return session.token;
}

function ensureSession() {
  if (!pendingSession) {
    pendingSession = startGuestSession().finally(() => {
      pendingSession = null;
    });
  }
  return pendingSession;
}

async function currentToken() {
  if (sessionToken) return sessionToken;

  sessionToken = readStoredToken();
  if (sessionToken) return sessionToken;

  return ensureSession();
}

async function parse(res) {
  if (res.status === 204) return null;

  const contentType = res.headers.get("content-type") || "";
  const body = contentType.includes("application/json") ? await res.json() : null;

  if (!res.ok) {
    const message = (body && body.message) || res.statusText || `Request failed (${res.status})`;
    throw new Error(message);
  }

  return body;
}

async function request(path, options = {}, isRetry = false) {
  const token = await currentToken();

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      ...(options.headers || {}),
      Authorization: `Bearer ${token}`,
    },
  });

  // An expired or invalidated token looks the same as never having had one. Get a fresh
  // session and replay the request once, so a visitor returning after a week doesn't
  // just see errors.
  if (res.status === 401 && !isRetry) {
    sessionToken = null;
    await ensureSession();
    return request(path, options, true);
  }

  return parse(res);
}

const jsonBody = (payload) => ({
  method: "POST",
  headers: { "Content-Type": "application/json" },
  body: JSON.stringify(payload),
});

export async function getNotes({ page = 1, pageSize = 20, tags = [] } = {}) {
  const params = new URLSearchParams({ page, pageSize });
  tags.forEach((t) => params.append("tag", t));
  return request(`/notes?${params}`);
}

export async function searchNotes({ q, page = 1, pageSize = 20 }) {
  const params = new URLSearchParams({ q, page, pageSize });
  return request(`/notes/search?${params}`);
}

export async function createNote({ title, bodyMarkdown }) {
  return request("/notes", jsonBody({ title, bodyMarkdown }));
}

export async function updateNote(id, { title, bodyMarkdown }) {
  return request(`/notes/${id}`, { ...jsonBody({ title, bodyMarkdown }), method: "PUT" });
}

export async function deleteNote(id) {
  return request(`/notes/${id}`, { method: "DELETE" });
}

export async function getTags() {
  return request("/tags");
}

export async function createTag({ name, category }) {
  return request("/tags", jsonBody({ name, category: category || null }));
}

export async function addTagToNote(tagId, noteId) {
  return request(`/tags/${tagId}/notes/${noteId}`, { method: "POST" });
}

export async function removeTagFromNote(tagId, noteId) {
  return request(`/tags/${tagId}/notes/${noteId}`, { method: "DELETE" });
}

export async function deleteTag(tagId) {
  return request(`/tags/${tagId}`, { method: "DELETE" });
}
