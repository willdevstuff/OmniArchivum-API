import { test, describe } from "node:test";
import assert from "node:assert/strict";

import { renderMarkdown } from "../frontend/js/markdown.js";

// The renderer's output is injected with innerHTML into note cards whose content is
// supplied by anyone using the public demo. These tests exist mainly to keep that
// safe — the formatting cases are the easy half.

describe("escaping and injection", () => {
  test("script tags are escaped rather than emitted as markup", () => {
    const html = renderMarkdown("<script>alert(1)</script>");
    assert.ok(!html.includes("<script"), "raw <script> reached the output");
    assert.ok(html.includes("&lt;script&gt;"), "script tag was not escaped");
  });

  test("event-handler attributes cannot escape into a real tag", () => {
    const html = renderMarkdown('<img src=x onerror="alert(1)">');
    assert.ok(!html.includes("<img"), "raw <img> reached the output");
    assert.ok(html.includes("&lt;img"), "img tag was not escaped");
    assert.ok(!html.includes("onerror=\""), "unescaped event handler reached the output");
  });

  test("quotes are escaped so they cannot break out of an attribute", () => {
    const html = renderMarkdown(`" onmouseover="alert(1)`);
    assert.ok(!html.includes('onmouseover="alert'), "attribute injection succeeded");
    assert.ok(html.includes("&quot;"), "double quote was not escaped");
  });

  test("javascript: links render as plain text with no anchor", () => {
    const html = renderMarkdown("[click me](javascript:alert(1))");
    assert.ok(!html.includes("<a "), "an anchor was emitted for a javascript: URL");
    assert.ok(!html.includes("javascript:"), "javascript: URL survived into the output");
    assert.ok(html.includes("click me"), "link label should still be shown as text");
  });

  test("data: links render as plain text with no anchor", () => {
    const html = renderMarkdown("[x](data:text/html;base64,PHNjcmlwdD4=)");
    assert.ok(!html.includes("<a "), "an anchor was emitted for a data: URL");
  });

  test("scheme allowlist is case-insensitive", () => {
    const html = renderMarkdown("[x](JaVaScRiPt:alert(1))");
    assert.ok(!html.includes("<a "), "mixed-case javascript: URL slipped through");
  });

  test("a user typing the internal placeholder cannot trigger substitution", () => {
    // Placeholders are inserted after escaping, so a literal "<<B0>>" in user input
    // is escaped and must not be swapped for a code block.
    const html = renderMarkdown("<<B0>> and <<I0>> are not placeholders");
    assert.ok(html.includes("&lt;&lt;B0&gt;&gt;"), "placeholder-looking text was not escaped");
    assert.ok(!html.includes("<pre"), "placeholder collision produced a code block");
  });

  test("markup inside a fenced code block stays inert", () => {
    const html = renderMarkdown("```\n<script>alert(1)</script>\n```");
    assert.ok(!html.includes("<script"), "raw script survived inside a code block");
    assert.ok(html.includes("&lt;script&gt;"), "code block content was not escaped");
  });
});

describe("safe links", () => {
  test("https links become anchors that cannot reach the opener", () => {
    const html = renderMarkdown("[Example](https://example.com)");
    assert.ok(html.includes('href="https://example.com"'), "href missing");
    assert.ok(html.includes('rel="noopener noreferrer"'), "rel missing on external link");
    assert.ok(html.includes('target="_blank"'), "target missing");
  });

  test("http and in-page anchors are allowed", () => {
    assert.ok(renderMarkdown("[a](http://example.com)").includes("<a "));
    assert.ok(renderMarkdown("[a](#section)").includes("<a "));
  });
});

describe("formatting", () => {
  test("bold and italic", () => {
    assert.ok(renderMarkdown("**bold**").includes("<strong>bold</strong>"));
    assert.ok(renderMarkdown("*italic*").includes("<em>italic</em>"));
  });

  test("bold is applied before italic so ** is not eaten", () => {
    const html = renderMarkdown("**bold**");
    assert.ok(!html.includes("<em>"), "bold was partially parsed as italic");
  });

  test("headings map below the page's own h1/h2", () => {
    assert.ok(renderMarkdown("# One").includes("<h3>One</h3>"));
    assert.ok(renderMarkdown("## Two").includes("<h4>Two</h4>"));
    assert.ok(renderMarkdown("### Three").includes("<h5>Three</h5>"));
  });

  test("a # without a space is not a heading", () => {
    const html = renderMarkdown("#nothashtag");
    assert.ok(!html.includes("<h3"), "treated a bare # as a heading");
  });

  test("unordered lists", () => {
    const html = renderMarkdown("- one\n- two");
    assert.ok(html.includes("<ul>"), "no <ul>");
    assert.equal((html.match(/<li>/g) || []).length, 2);
    assert.ok(html.includes("</ul>"), "list was not closed");
  });

  test("ordered lists", () => {
    const html = renderMarkdown("1. one\n2. two");
    assert.ok(html.includes("<ol>"), "no <ol>");
    assert.equal((html.match(/<li>/g) || []).length, 2);
  });

  test("switching list type closes the previous list", () => {
    const html = renderMarkdown("- bullet\n1. numbered");
    assert.ok(html.includes("</ul>"), "unordered list was left open");
    assert.ok(html.includes("<ol>"), "ordered list did not start");
  });

  test("inline code", () => {
    const html = renderMarkdown("use `fetch` here");
    assert.ok(html.includes('<code class="md-code-inline">fetch</code>'));
  });

  test("fenced code blocks, with the language tag consumed", () => {
    const html = renderMarkdown("```csharp\nvar x = 1;\n```");
    assert.ok(html.includes('<pre class="md-code">'), "no code block");
    assert.ok(html.includes("var x = 1;"), "code content missing");
    assert.ok(!html.includes("csharp"), "language tag leaked into output");
  });

  test("markdown inside a code block is left alone", () => {
    const html = renderMarkdown("```\n**not bold**\n```");
    assert.ok(html.includes("**not bold**"), "code block content was transformed");
    assert.ok(!html.includes("<strong>"), "emphasis was applied inside a code block");
  });

  test("a code block on its own line is not wrapped in a paragraph", () => {
    const html = renderMarkdown("```\ncode\n```");
    assert.ok(!html.includes("<p><pre"), "code block was nested inside a paragraph");
  });

  test("consecutive lines join into one paragraph, blank lines split them", () => {
    const html = renderMarkdown("one\ntwo\n\nthree");
    assert.equal((html.match(/<p>/g) || []).length, 2);
  });
});

describe("edge cases", () => {
  test("empty and nullish input", () => {
    assert.equal(renderMarkdown(""), "");
    assert.equal(renderMarkdown(undefined), "");
    assert.equal(renderMarkdown(null), "");
  });

  test("plain text passes through unchanged inside a paragraph", () => {
    assert.equal(renderMarkdown("just text"), "<p>just text</p>");
  });

  test("an unterminated code fence does not swallow the document", () => {
    const html = renderMarkdown("before\n\n```\nunclosed");
    assert.ok(html.includes("before"), "content before an unclosed fence was lost");
  });
});
