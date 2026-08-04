const isLocal = window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1";
const API_BASE = isLocal
  ? "http://localhost:5000/api"
  : "https://omniarchivum-api.proudmoss-5344fb15.uksouth.azurecontainerapps.io/api";

async function handleResponse(res) {
  if (res.status === 204) return null;

  const contentType = res.headers.get("content-type") || "";
  const body = contentType.includes("application/json") ? await res.json() : null;

  if (!res.ok) {
    const message = (body && body.message) || res.statusText || `Request failed (${res.status})`;
    throw new Error(message);
  }

  return body;
}

export async function getNotes({ page = 1, pageSize = 20, tags = [] } = {}) {
  const params = new URLSearchParams({ page, pageSize });
  tags.forEach((t) => params.append("tag", t));
  const res = await fetch(`${API_BASE}/notes?${params}`);
  return handleResponse(res);
}

export async function searchNotes({ q, page = 1, pageSize = 20 }) {
  const params = new URLSearchParams({ q, page, pageSize });
  const res = await fetch(`${API_BASE}/notes/search?${params}`);
  return handleResponse(res);
}

export async function createNote({ title, bodyMarkdown }) {
  const res = await fetch(`${API_BASE}/notes`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title, bodyMarkdown }),
  });
  return handleResponse(res);
}

export async function updateNote(id, { title, bodyMarkdown }) {
  const res = await fetch(`${API_BASE}/notes/${id}`, {
    method: "PUT",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ title, bodyMarkdown }),
  });
  return handleResponse(res);
}

export async function deleteNote(id) {
  const res = await fetch(`${API_BASE}/notes/${id}`, { method: "DELETE" });
  return handleResponse(res);
}

export async function getTags() {
  const res = await fetch(`${API_BASE}/tags`);
  return handleResponse(res);
}

export async function createTag({ name, category }) {
  const res = await fetch(`${API_BASE}/tags`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ name, category: category || null }),
  });
  return handleResponse(res);
}

export async function addTagToNote(tagId, noteId) {
  const res = await fetch(`${API_BASE}/tags/${tagId}/notes/${noteId}`, { method: "POST" });
  return handleResponse(res);
}

export async function removeTagFromNote(tagId, noteId) {
  const res = await fetch(`${API_BASE}/tags/${tagId}/notes/${noteId}`, { method: "DELETE" });
  return handleResponse(res);
}

export async function deleteTag(tagId) {
  const res = await fetch(`${API_BASE}/tags/${tagId}`, { method: "DELETE" });
  return handleResponse(res);
}
