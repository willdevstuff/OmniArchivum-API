// Minimal Markdown renderer — no dependencies, no build step.
//
// Security note: note bodies are user-supplied and this is a public demo, so the
// source is HTML-escaped *first*. Every transform below runs over already-escaped
// text, which means any raw HTML in a note renders as literal characters and the
// only live markup is what this file generates. Link hrefs are additionally checked
// against a scheme allowlist so `javascript:` URLs can't slip through.

const ESCAPE_MAP = {
  "&": "&amp;",
  "<": "&lt;",
  ">": "&gt;",
  '"': "&quot;",
  "'": "&#39;",
};

// Placeholders deliberately contain angle brackets. They're substituted in *after*
// escaping, so a user typing "<<B0>>" in a note has it escaped to "&lt;&lt;B0&gt;&gt;"
// and can never collide with a real placeholder.
const BLOCK = (i) => `<<B${i}>>`;
const INLINE = (i) => `<<I${i}>>`;

function escapeHtml(text) {
  return text.replace(/[&<>"']/g, (char) => ESCAPE_MAP[char]);
}

// Allowlist rather than denylist: anything not explicitly http/https or an in-page
// anchor is rendered as plain text instead of a link.
function isSafeUrl(url) {
  const value = url.trim().toLowerCase();
  return value.startsWith("http://") || value.startsWith("https://") || value.startsWith("#");
}

function renderInline(text) {
  return text
    .replace(/\*\*([^*]+)\*\*/g, "<strong>$1</strong>")
    .replace(/\*([^*]+)\*/g, "<em>$1</em>")
    .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (match, label, url) =>
      isSafeUrl(url)
        ? `<a href="${url}" target="_blank" rel="noopener noreferrer">${label}</a>`
        : label
    );
}

export function renderMarkdown(source) {
  if (!source) return "";

  let text = escapeHtml(source);

  // Pull code out before anything else so its contents are never treated as markup.
  const blocks = [];
  text = text.replace(/```[^\n]*\n([\s\S]*?)```/g, (match, code) => {
    blocks.push(`<pre class="md-code"><code>${code.replace(/\n$/, "")}</code></pre>`);
    return BLOCK(blocks.length - 1);
  });

  const inlines = [];
  text = text.replace(/`([^`\n]+)`/g, (match, code) => {
    inlines.push(`<code class="md-code-inline">${code}</code>`);
    return INLINE(inlines.length - 1);
  });

  const out = [];
  let paragraph = [];
  let listTag = null;

  function flushParagraph() {
    if (paragraph.length === 0) return;
    out.push(`<p>${renderInline(paragraph.join(" "))}</p>`);
    paragraph = [];
  }

  function closeList() {
    if (!listTag) return;
    out.push(`</${listTag}>`);
    listTag = null;
  }

  function openList(tag) {
    if (listTag === tag) return;
    closeList();
    out.push(`<${tag}>`);
    listTag = tag;
  }

  for (const line of text.split("\n")) {
    const trimmed = line.trim();

    // A fenced code block on its own line shouldn't get wrapped in a <p>.
    if (/^<<B\d+>>$/.test(trimmed)) {
      flushParagraph();
      closeList();
      out.push(trimmed);
      continue;
    }

    const heading = /^(#{1,4})\s+(.+)$/.exec(line);
    if (heading) {
      flushParagraph();
      closeList();
      const level = heading[1].length + 2; // "#" maps to h3 — h1/h2 belong to the page
      out.push(`<h${level}>${renderInline(heading[2])}</h${level}>`);
      continue;
    }

    const bullet = /^\s*[-*]\s+(.+)$/.exec(line);
    if (bullet) {
      flushParagraph();
      openList("ul");
      out.push(`<li>${renderInline(bullet[1])}</li>`);
      continue;
    }

    const numbered = /^\s*\d+\.\s+(.+)$/.exec(line);
    if (numbered) {
      flushParagraph();
      openList("ol");
      out.push(`<li>${renderInline(numbered[1])}</li>`);
      continue;
    }

    if (trimmed === "") {
      flushParagraph();
      closeList();
      continue;
    }

    paragraph.push(trimmed);
  }

  flushParagraph();
  closeList();

  return out
    .join("")
    .replace(/<<I(\d+)>>/g, (match, i) => inlines[Number(i)])
    .replace(/<<B(\d+)>>/g, (match, i) => blocks[Number(i)]);
}
