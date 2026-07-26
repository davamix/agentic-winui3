# Experiments

Hands-on experiments for **Track B** — a native [A2UI](../research.md#3-a2ui--agent-to-ui-protocol) renderer for WinUI 3. Each experiment proves *one* claim against fixed inputs, kept deliberately minimal so it is easy to see how each piece works.

## How to add an experiment

Follow **[experiment-setup-guide.md](./experiment-setup-guide.md)**. In short: copy [`_template.md`](./_template.md) to `experiment-NN-<slug>.md`, fill the definition sections, add any fixtures under [`samples/`](../../samples/), and register a row below.

## Index

| # | Experiment | Proves | Status |
| --- | --- | --- | --- |
| 01 | [static-render](./experiment-01-static-render.md) | An A2UI message stream renders as native WinUI controls | In progress |
| 02 | binding-and-actions *(planned)* | Two-way data binding + action round-trip | — |
| 03 | live-stream *(planned)* | File→SSE transport, incremental updates, UI-thread marshalling | — |
| 04 | real-producer *(planned)* | An agent (Agent Framework, optionally via MCP) emits the A2UI stream | — |

## Roadmap rationale

Experiments build strictly on each other, adding the components from [research.md §8](../research.md#8-feasibility-analysis-for-winui-3) one layer at a time. Everything not needed to prove the current claim — MCP, a real LLM backend, streaming, diffing, a custom Fluent catalog, the AG-UI envelope — is deferred to the experiment that first requires it.
