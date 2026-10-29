## graphify

This project has a knowledge graph at `graphify-out/` (god nodes, community structure, cross-file edges).
Build scope lives in `.graphifyignore` at the project root.

When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.

Rules:
- When `graphify-out/graph.json` exists, run `graphify query "<question>"` before your first grep, search or source-file read for a codebase question, and put this rule in every subagent prompt that explores code. Read raw files only for lines the graph or an earlier read already located. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than `GRAPH_REPORT.md` or raw grep output.
- If a query, path or explain returns nothing useful, check that the graph is current before falling back to grep: `graphify update .` re-extracts code only (AST, no API cost). Say plainly that the graph is stale instead of answering from it.
- After modifying code, run `graphify update .` to keep the graph current.
- Read `graphify-out/GRAPH_REPORT.md` only for broad architecture review or when query/path/explain do not surface enough context.
- A committed `graphify-out/graph.json` is rewritten whole on every build, so a large diff to it in a pull request is expected. Do not flag it as churn or suggest skipping the graph rebuild.

Layout the graph covers: `hs-classifier-api/` (.NET 8 — `HsClassifier.Domain`, `.Application`, `.Infrastructure`, `.API`, `.Tests`) and `hs-classifier/` (React + Vite, `src/`). Docs and specs (`docs/`, `.kiro/specs/`) are indexed too.
Excluded from the graph: `data/*.json`, `*.csv`, `hs-classifier/src/data/*.json`, images, and PDF/DOCX/PPTX (no pypdf/office extras installed here), plus `bin/`, `obj/`, `node_modules/`, `dist/`.
