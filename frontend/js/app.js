import {
  getNotes,
  searchNotes,
  createNote,
  updateNote,
  deleteNote,
  getTags,
  createTag,
  addTagToNote,
  removeTagFromNote,
  deleteTag,
} from "./api.js";

const PAGE_SIZE = 20;

const state = {
  mode: "browse", // "browse" | "search"
  page: 1,
  query: "",
  selectedTags: new Set(),
  notes: [],
  tags: [],
  hasNextPage: false,
  editingNoteId: null,
};

const el = {
  searchForm: document.getElementById("search-form"),
  searchInput: document.getElementById("search-input"),
  clearSearchBtn: document.getElementById("clear-search-btn"),
  newNoteBtn: document.getElementById("new-note-btn"),
  tagList: document.getElementById("tag-list"),
  newTagForm: document.getElementById("new-tag-form"),
  newTagName: document.getElementById("new-tag-name"),
  newTagCategory: document.getElementById("new-tag-category"),
  statusBar: document.getElementById("status-bar"),
  notesGrid: document.getElementById("notes-grid"),
  prevPageBtn: document.getElementById("prev-page-btn"),
  nextPageBtn: document.getElementById("next-page-btn"),
  pageIndicator: document.getElementById("page-indicator"),
  newNoteDialog: document.getElementById("new-note-dialog"),
  newNoteForm: document.getElementById("new-note-form"),
  noteDialogTitle: document.getElementById("note-dialog-title"),
  noteSubmitBtn: document.getElementById("note-submit-btn"),
  noteTitleInput: document.getElementById("note-title-input"),
  noteBodyInput: document.getElementById("note-body-input"),
  cancelNewNoteBtn: document.getElementById("cancel-new-note-btn"),
  noteCardTemplate: document.getElementById("note-card-template"),
  confirmDialog: document.getElementById("confirm-dialog"),
  confirmMessage: document.getElementById("confirm-message"),
  confirmCancelBtn: document.getElementById("confirm-cancel-btn"),
  confirmOkBtn: document.getElementById("confirm-ok-btn"),
};

function confirmAction(message) {
  el.confirmMessage.textContent = message;
  el.confirmDialog.showModal();
  return new Promise((resolve) => {
    function settle(result) {
      el.confirmCancelBtn.removeEventListener("click", onCancel);
      el.confirmOkBtn.removeEventListener("click", onOk);
      el.confirmDialog.removeEventListener("cancel", onEscape);
      el.confirmDialog.close();
      resolve(result);
    }
    function onCancel() {
      settle(false);
    }
    function onOk() {
      settle(true);
    }
    function onEscape(e) {
      e.preventDefault();
      settle(false);
    }
    el.confirmCancelBtn.addEventListener("click", onCancel);
    el.confirmOkBtn.addEventListener("click", onOk);
    el.confirmDialog.addEventListener("cancel", onEscape);
  });
}

function showStatus(message, isError = false) {
  el.statusBar.textContent = message;
  el.statusBar.hidden = false;
  el.statusBar.classList.toggle("status-error", isError);
}

function clearStatus() {
  el.statusBar.hidden = true;
  el.statusBar.textContent = "";
}

function formatDate(iso) {
  return new Date(iso).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function categoryColor(category) {
  if (!category) return "var(--text-muted)";
  let hash = 0;
  for (let i = 0; i < category.length; i++) {
    hash = category.charCodeAt(i) + ((hash << 5) - hash);
  }
  return `hsl(${Math.abs(hash) % 360}, 65%, 60%)`;
}

async function refreshTags() {
  try {
    state.tags = await getTags();
    renderTagList();
  } catch (err) {
    showStatus(`Couldn't load tags: ${err.message}`, true);
  }
}

function renderTagList() {
  el.tagList.innerHTML = "";

  if (state.tags.length === 0) {
    const hint = document.createElement("p");
    hint.className = "empty-hint";
    hint.textContent = "No tags yet.";
    el.tagList.appendChild(hint);
    return;
  }

  const groups = new Map();
  for (const tag of state.tags) {
    const key = tag.category || "";
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(tag);
  }

  const categories = [...groups.keys()].sort((a, b) => {
    if (!a) return 1;
    if (!b) return -1;
    return a.localeCompare(b);
  });

  for (const category of categories) {
    const group = document.createElement("div");
    group.className = "tag-category-group";

    const header = document.createElement("div");
    header.className = "tag-category-header";
    const dot = document.createElement("span");
    dot.className = "category-dot";
    dot.style.background = categoryColor(category);
    header.appendChild(dot);
    header.appendChild(document.createTextNode(category || "No category"));
    group.appendChild(header);

    for (const tag of groups.get(category)) {
      const row = document.createElement("div");
      row.className = "tag-row";

      const chip = document.createElement("button");
      chip.type = "button";
      chip.className = "tag-chip";
      chip.classList.toggle("active", state.selectedTags.has(tag.name));
      chip.style.borderLeftColor = categoryColor(tag.category);
      chip.textContent = tag.name;
      chip.addEventListener("click", () => toggleTagFilter(tag.name));
      row.appendChild(chip);

      const deleteBtn = document.createElement("button");
      deleteBtn.type = "button";
      deleteBtn.className = "tag-delete-btn";
      deleteBtn.textContent = "×";
      deleteBtn.setAttribute("aria-label", `Delete tag ${tag.name}`);
      deleteBtn.addEventListener("click", async () => {
        const confirmed = await confirmAction(
          `Delete tag "${tag.name}"? This removes it from every note. This can't be undone.`
        );
        if (!confirmed) return;
        try {
          await deleteTag(tag.id);
          state.selectedTags.delete(tag.name);
          clearStatus();
          await refreshTags();
          await loadNotes();
        } catch (err) {
          showStatus(`Couldn't delete tag: ${err.message}`, true);
        }
      });
      row.appendChild(deleteBtn);

      group.appendChild(row);
    }

    el.tagList.appendChild(group);
  }
}

function toggleTagFilter(tagName) {
  if (state.selectedTags.has(tagName)) {
    state.selectedTags.delete(tagName);
  } else {
    state.selectedTags.add(tagName);
  }
  state.mode = "browse";
  state.query = "";
  el.searchInput.value = "";
  el.clearSearchBtn.hidden = true;
  state.page = 1;
  renderTagList();
  loadNotes();
}

function populateTagSelect(select, note) {
  select.innerHTML = '<option value="">+ add tag&hellip;</option>';
  const attached = new Set(note.tags.map((t) => t.id));
  for (const tag of state.tags) {
    if (attached.has(tag.id)) continue;
    const option = document.createElement("option");
    option.value = tag.id;
    option.textContent = tag.category ? `${tag.name} (${tag.category})` : tag.name;
    select.appendChild(option);
  }
}

function renderNotes() {
  el.notesGrid.innerHTML = "";

  if (state.notes.length === 0) {
    const empty = document.createElement("p");
    empty.className = "empty-hint";
    empty.textContent = state.mode === "search"
      ? `No notes match "${state.query}".`
      : "No notes yet — create one to get started.";
    el.notesGrid.appendChild(empty);
  }

  for (const note of state.notes) {
    const fragment = el.noteCardTemplate.content.cloneNode(true);
    const card = fragment.querySelector(".note-card");
    const titleEl = fragment.querySelector(".note-title");
    const bodyEl = fragment.querySelector(".note-body");
    const expandBtn = fragment.querySelector(".expand-btn");
    const tagsEl = fragment.querySelector(".note-tags");
    const tagSelect = fragment.querySelector(".tag-select");
    const metaEl = fragment.querySelector(".note-meta");
    const editBtn = fragment.querySelector(".edit-btn");
    const deleteBtn = fragment.querySelector(".delete-btn");

    titleEl.textContent = note.title;

    const isLong = note.bodyMarkdown.length > 220;
    bodyEl.textContent = isLong ? `${note.bodyMarkdown.slice(0, 220)}…` : note.bodyMarkdown;
    if (isLong) {
      expandBtn.hidden = false;
      expandBtn.addEventListener("click", () => {
        const expanded = bodyEl.dataset.expanded === "true";
        bodyEl.textContent = expanded ? `${note.bodyMarkdown.slice(0, 220)}…` : note.bodyMarkdown;
        bodyEl.dataset.expanded = expanded ? "false" : "true";
        expandBtn.textContent = expanded ? "Show more" : "Show less";
      });
    }

    for (const tag of note.tags) {
      const tagEl = document.createElement("span");
      tagEl.className = "tag-pill";
      tagEl.style.borderLeftColor = categoryColor(tag.category);

      const tagLabel = document.createElement("span");
      tagLabel.textContent = tag.name;
      tagEl.appendChild(tagLabel);

      const removeTagBtn = document.createElement("button");
      removeTagBtn.type = "button";
      removeTagBtn.className = "tag-remove-btn";
      removeTagBtn.textContent = "×";
      removeTagBtn.setAttribute("aria-label", `Remove tag ${tag.name}`);
      removeTagBtn.addEventListener("click", async () => {
        try {
          await removeTagFromNote(tag.id, note.id);
          clearStatus();
          await loadNotes();
        } catch (err) {
          showStatus(`Couldn't remove tag: ${err.message}`, true);
        }
      });
      tagEl.appendChild(removeTagBtn);

      tagsEl.appendChild(tagEl);
    }

    populateTagSelect(tagSelect, note);
    tagSelect.addEventListener("change", async () => {
      const tagId = tagSelect.value;
      if (!tagId) return;
      try {
        await addTagToNote(tagId, note.id);
        clearStatus();
        await loadNotes();
      } catch (err) {
        showStatus(`Couldn't add tag: ${err.message}`, true);
      }
    });

    metaEl.textContent = `Created ${formatDate(note.createdUtc)} · Updated ${formatDate(note.updatedUtc)}`;

    editBtn.addEventListener("click", () => {
      state.editingNoteId = note.id;
      el.noteDialogTitle.textContent = "Edit note";
      el.noteSubmitBtn.textContent = "Save";
      el.noteTitleInput.value = note.title;
      el.noteBodyInput.value = note.bodyMarkdown;
      el.newNoteDialog.showModal();
    });

    deleteBtn.addEventListener("click", async () => {
      const confirmed = await confirmAction(`Delete "${note.title}"? This can't be undone.`);
      if (!confirmed) return;
      try {
        await deleteNote(note.id);
        clearStatus();
        await loadNotes();
      } catch (err) {
        showStatus(`Couldn't delete note: ${err.message}`, true);
      }
    });

    el.notesGrid.appendChild(fragment);
  }

  el.pageIndicator.textContent = `Page ${state.page}`;
  el.prevPageBtn.disabled = state.page <= 1;
  el.nextPageBtn.disabled = !state.hasNextPage;
}

async function loadNotes() {
  clearStatus();
  try {
    let results;
    if (state.mode === "search") {
      results = await searchNotes({ q: state.query, page: state.page, pageSize: PAGE_SIZE });
    } else {
      results = await getNotes({
        page: state.page,
        pageSize: PAGE_SIZE,
        tags: [...state.selectedTags],
      });
    }
    state.notes = results;
    state.hasNextPage = results.length === PAGE_SIZE;
    renderNotes();
  } catch (err) {
    showStatus(`Couldn't load notes: ${err.message}`, true);
  }
}

el.searchForm.addEventListener("submit", (e) => {
  e.preventDefault();
  const q = el.searchInput.value.trim();
  if (!q) return;
  state.mode = "search";
  state.query = q;
  state.selectedTags.clear();
  state.page = 1;
  el.clearSearchBtn.hidden = false;
  renderTagList();
  loadNotes();
});

el.clearSearchBtn.addEventListener("click", () => {
  state.mode = "browse";
  state.query = "";
  state.page = 1;
  el.searchInput.value = "";
  el.clearSearchBtn.hidden = true;
  loadNotes();
});

el.prevPageBtn.addEventListener("click", () => {
  if (state.page <= 1) return;
  state.page -= 1;
  loadNotes();
});

el.nextPageBtn.addEventListener("click", () => {
  if (!state.hasNextPage) return;
  state.page += 1;
  loadNotes();
});

el.newTagForm.addEventListener("submit", async (e) => {
  e.preventDefault();
  const name = el.newTagName.value.trim();
  const category = el.newTagCategory.value.trim();
  if (!name) return;
  try {
    await createTag({ name, category: category || undefined });
    el.newTagForm.reset();
    clearStatus();
    await refreshTags();
  } catch (err) {
    showStatus(`Couldn't create tag: ${err.message}`, true);
  }
});

el.newNoteBtn.addEventListener("click", () => {
  state.editingNoteId = null;
  el.newNoteForm.reset();
  el.noteDialogTitle.textContent = "New note";
  el.noteSubmitBtn.textContent = "Create";
  el.newNoteDialog.showModal();
});

el.cancelNewNoteBtn.addEventListener("click", () => {
  el.newNoteDialog.close();
});

el.newNoteForm.addEventListener("submit", async (e) => {
  e.preventDefault();
  const title = el.noteTitleInput.value.trim();
  const bodyMarkdown = el.noteBodyInput.value.trim();
  if (!title || !bodyMarkdown) return;
  try {
    if (state.editingNoteId) {
      await updateNote(state.editingNoteId, { title, bodyMarkdown });
    } else {
      await createNote({ title, bodyMarkdown });
      state.mode = "browse";
      state.page = 1;
    }
    el.newNoteDialog.close();
    clearStatus();
    await loadNotes();
  } catch (err) {
    const verb = state.editingNoteId ? "update" : "create";
    showStatus(`Couldn't ${verb} note: ${err.message}`, true);
  }
});

(async function init() {
  await refreshTags();
  await loadNotes();
})();
