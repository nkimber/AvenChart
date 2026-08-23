# AvenChart agent guidance

## Graphify code-navigation index

The deterministic, code-only Graphify index lives in `.graphify/`. For a codebase, architecture, or change-impact question, consult the graph first and then validate the relevant source files before drawing a conclusion:

```powershell
npm exec --prefix tools/graphify -- graphify query "<question>" --graph .graphify/graph.json
npm exec --prefix tools/graphify -- graphify review-delta --files <changed-file> --graph .graphify/graph.json
```

Use `GRAPH_REPORT.md` only for broad orientation. Do not treat the graph, clustering, node counts, or a query result as a correctness, security, clinical-safety, accessibility, interoperability, or production-readiness conclusion.

The committed graph is intentionally code-only and excludes documentation, generated data, the Phase 1 reference frontend, agent state, and build output. Do not enable Graphify semantic extraction, direct provider backends, corpus ingestion, or `agent-stats` without explicit user approval: those modes have different privacy and provenance implications.

After a meaningful committed-code change, refresh the index and review the resulting graph changes before committing them:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-AvenChartGraph.ps1
```

Before committing the durable graph artifacts, validate their repository portability:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Update-AvenChartGraph.ps1 -Mode PortableCheck
```
