# agentic-winui3

Experiments in building **Agentic User Interfaces** for native Windows desktop applications with **WinUI 3 / Windows App SDK**.

The current generation of agentic-UI protocols — [AG-UI](https://ag-ui.com), [A2UI](https://a2ui.org), [MCP-UI / MCP Apps](https://mcpui.dev) — were all designed with web frontends in mind. This repo investigates how much of that thinking transfers to a native XAML stack, and what has to be invented.

## The question

> Can an AI agent drive, extend, or *generate* the UI of a native WinUI 3 application, using an open protocol rather than bespoke glue?

## Status

Experimenting. Pursuing **Track B — a native A2UI renderer for WinUI 3**: the agent emits A2UI declarative JSON, which is validated against a catalog and mapped to native WinUI controls, with no code execution. Two experiments done — a static A2UI stream renders as native controls, and a bound surface completes a two-way binding and action round-trip.

## Documentation

| Doc | What it covers |
| --- | --- |
| [docs/research.md](docs/research.md) | Full research dossier: protocol specs, .NET/WinUI ecosystem, research papers, feasibility analysis, and links |
| [docs/experiments/](docs/experiments/) | The experiments — one claim each, against fixed inputs. Start with the [index](docs/experiments/README.md) |
| [docs/toolchain-notes.md](docs/toolchain-notes.md) | Environment and tooling gotchas, kept separate from the experiments so they are solved once |

## License

MIT (see [LICENSE](LICENSE)).
