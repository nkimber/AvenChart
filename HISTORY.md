# Workbench and public history boundaries

The AvenChart program workbench preserves the source-development lineage of the two AvenChart applications while keeping private project administration outside the public record. It also separates the fixed evidence from each program phase so later work cannot silently rewrite an earlier result.

## Phase 1 closure

Phase 1, the experimental autonomous build, closed on August 20, 2026. Its application baseline is identified by the annotated Git tag `phase-1-experimental` at revision `d77a8320e6751a2deb2daf14cf1ac5d6b00cb989`.

The Phase 1 workbench data is immutable by policy:

- `.public-history-base` contains the exact closure revision rather than a moving branch or tag;
- the history generator refuses to run without a full, resolvable 40-character revision;
- the generated dataset records the phase status, closure date, named tag, source revision, and approximately 86% functional-coverage estimate; and
- later Phase 2 and Phase 3 commits are intentionally excluded from every Phase 1 metric and history view.

Regenerating the workbench after later development therefore reproduces the same Phase 1 dataset. Changing the Phase 1 boundary requires an explicit edit to the recorded revision and would be a change to this historical policy, not a normal workbench refresh.

## Public source-history boundary

The public history:

- retains 773 commits through the Phase 1 closure boundary;
- preserves commit order, dates, and Neil Kimber's authorship;
- normalizes former internal application paths and product labels to AvenChart;
- keeps application code, migrations, tests, and runtime configuration; and
- links every retained revision from the static [program workbench](https://nkimber.github.io/AvenChart/).

The public history intentionally excludes:

- private planning and project-memory documents;
- operational control-plane source and configuration;
- local development-tool configuration;
- generated logs, screenshots, database dumps, and test artifacts;
- commit bodies, automated co-author trailers, and private repository references; and
- revisions that changed only excluded material.

History rewriting necessarily changes commit hashes. The commits in this repository are the canonical public hashes. The private archive remains separate and is not a public source-distribution endpoint.
