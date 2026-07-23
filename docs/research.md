# Agentic UI for WinUI 3 — Research Dossier

> Last updated: 2026-07-21
>
> Goal: understand how the emerging agentic-UI protocols (AG-UI, A2UI, MCP-UI / MCP Apps) work, what they assume about the host, and how they could be applied to a **native WinUI 3 / Windows App SDK** desktop application.

---

## Table of contents

1. [The landscape in one picture](#1-the-landscape-in-one-picture)
2. [AG-UI — Agent–User Interaction Protocol](#2-ag-ui--agentuser-interaction-protocol)
3. [A2UI — Agent-to-UI Protocol](#3-a2ui--agent-to-ui-protocol)
4. [MCP-UI and MCP Apps (SEP-1865)](#4-mcp-ui-and-mcp-apps-sep-1865)
5. [Adjacent protocols worth knowing](#5-adjacent-protocols-worth-knowing)
6. [The .NET / WinUI side of the house](#6-the-net--winui-side-of-the-house)
7. [Windows as an agentic OS](#7-windows-as-an-agentic-os)
8. [Feasibility analysis for WinUI 3](#8-feasibility-analysis-for-winui-3)
9. [Research papers and academic prior art](#9-research-papers-and-academic-prior-art)
10. [Reading list / link index](#10-reading-list--link-index)
11. [Open questions](#11-open-questions)

---

## 1. The landscape in one picture

The three protocols people lump together as "agentic UI" solve **different layers**. This is the single most important thing to get straight before designing anything.

```
┌───────────────────────────────────────────────────────────────┐
│  A2UI            WHAT the UI looks like                       │
│                  Declarative component tree + data model      │
│                  (agent → client, JSON, no code execution)    │
├───────────────────────────────────────────────────────────────┤
│  AG-UI           HOW agent and UI talk over time              │
│                  Event stream: tokens, tool calls, state,     │
│                  reasoning, human-in-the-loop interrupts      │
├───────────────────────────────────────────────────────────────┤
│  MCP / MCP Apps  WHAT the agent can DO, and what UI a tool    │
│                  carries with it (ui:// resources + JSON-RPC  │
│                  over postMessage, rendered in a sandbox)     │
└───────────────────────────────────────────────────────────────┘
```

They compose rather than compete. A realistic stack:
**MCP** gives the agent tools → **AG-UI** streams the agent's activity to the app → **A2UI** describes any rich UI the agent wants to render → the app maps that to **native WinUI controls**.

| | AG-UI | A2UI | MCP Apps / MCP-UI |
| --- | --- | --- | --- |
| Layer | Transport / session | Presentation | Tool-attached UI |
| Payload | ~30 typed events | Component tree + data model | HTML resource (`ui://`) |
| Origin | CopilotKit | Google | MCP-UI community → MCP core (OpenAI + Anthropic) |
| License | MIT | Apache-2.0 | MIT / Apache-2.0 |
| Native-friendly? | ✅ Yes, transport-agnostic | ✅ Yes, by design | ❌ Assumes HTML + iframe |
| .NET support today | ✅ Official-ish (Microsoft Agent Framework) | ⚠️ Blazor only (community) | ⚠️ Needs WebView2 |

---

## 2. AG-UI — Agent–User Interaction Protocol

- Home: <https://ag-ui.com> · Docs: <https://docs.ag-ui.com/introduction>
- Repo: <https://github.com/ag-ui-protocol/ag-ui> (MIT, ~14.8k ★)
- Origin: CopilotKit; now a multi-vendor protocol.

### What it is

An **event-sourced** protocol replacing request/response RPC between an agent backend and a frontend. Instead of waiting for a final answer, the agent emits a continuous stream of typed events describing what it is doing. The frontend is a reducer over that stream.

This is a very good fit for MVVM: the event stream *is* a sequence of view-model mutations.

### Transport

Transport-agnostic. Reference implementations use **SSE** and **WebSockets**; webhooks and plain HTTP chunked streaming also work. There is nothing browser-specific in the wire format — it is JSON events over a stream. **This is why AG-UI is the most portable of the three to a native client.**

### Event catalogue (~30 types)

**Lifecycle**
`RunStarted` · `RunFinished` · `RunError` · `StepStarted` · `StepFinished`

**Text messages**
`TextMessageStart` · `TextMessageContent` · `TextMessageEnd` · `TextMessageChunk`

**Tool calls**
`ToolCallStart` · `ToolCallArgs` · `ToolCallEnd` · `ToolCallResult` · `ToolCallChunk`

**State management**
`StateSnapshot` (full state) · `StateDelta` (JSON Patch / RFC 6902) · `MessagesSnapshot`

**Activity**
`ActivitySnapshot` · `ActivityDelta`

**Reasoning**
`ReasoningStart` · `ReasoningMessageStart` · `ReasoningMessageContent` · `ReasoningMessageEnd` · `ReasoningMessageChunk` · `ReasoningEnd` · `ReasoningEncryptedValue`

**Escape hatches**
`Raw` (pass through foreign events) · `Custom` (application-specific extension)

### Capabilities the protocol standardises

- Streaming chat with cancellation
- Multimodality — typed attachments (files, images, audio, transcripts)
- **Generative UI** — both "render this pre-registered component" and declarative variants (this is the seam where A2UI plugs in)
- Shared bidirectional state with conflict resolution
- **Frontend tools** — the agent calls a function that lives in the *client*, not the server. Critically important for us: this is how an agent invokes native app capability.
- Human oversight — interrupts for pause / approve / edit
- Agent composition — sub-agents with scoped state

### SDK status

| Language | Status |
| --- | --- |
| TypeScript, Python | ✅ First-party |
| Kotlin, Go, Dart, Java, Rust, Ruby, C++ | ✅ Supported |
| **.NET** | 🛠️ In progress — [PR #38](https://github.com/ag-ui-protocol/ag-ui/pull/38), [issue #28](https://github.com/ag-ui-protocol/ag-ui/issues/28) |
| Nim | 🛠️ In progress |

**But** — see §6: Microsoft ships `Microsoft.Agents.AI.AGUI` in the Agent Framework, which is the practical .NET path today.

### Learning resources

- [AG-UI Dojo](https://docs.ag-ui.com/dojo) — minimal focused examples of each building block
- [DataCamp tutorial](https://www.datacamp.com/tutorial/ag-ui)
- [AG2 integration docs](https://docs.ag2.ai/latest/docs/user-guide/ag-ui/)

---

## 3. A2UI — Agent-to-UI Protocol

- Home: <https://a2ui.org> · Repo: <https://github.com/a2ui-project/a2ui> (Apache-2.0, ~15.8k ★)
- Announcement: [Google Developers Blog](https://developers.googleblog.com/introducing-a2ui-an-open-project-for-agent-driven-interfaces/) (Dec 2025)
- Spec versions: v0.8, **v0.9.1 stable**, v1.0 in candidate status.

### What it is

A **declarative UI protocol**. The agent emits JSON describing *components, their properties, and a data model* — never HTML, never JavaScript, never executable code. The client maps each abstract component onto its own native widget.

> "How to let remote agents present secure, interactive interfaces **across trust boundaries** without sending executable code."

That framing is exactly the problem a native desktop app has, and it is why A2UI — not MCP-UI — is the most architecturally appropriate model for WinUI 3.

### Core concepts

- **Surface** — a canvas the agent can draw on (a dialog, a sidebar, a main pane). Identified by `surfaceId`.
- **Catalog** — a **JSON Schema** file enumerating which components, functions and themes the agent is allowed to use. This is the contract, and the security boundary: *all agent output is validated against the catalog.*
- **Components** — instances of catalog types, held in a **flat adjacency list** (not a nested tree). Containers reference children by id string. A component with id `root` is the entry point; the client rebuilds the tree by id lookup at render time.
- **Data model** — application state that components bind to reactively.

### Message types

Server → client, one per JSON line (JSONL / SSE):

| Message | Purpose |
| --- | --- |
| `createSurface` | Create a surface, bind it to a `catalogId` |
| `updateComponents` | Add / update component definitions |
| `updateDataModel` | Populate or mutate the backing data |
| `beginRendering` | Signal the client to render |
| `deleteSurface` | Tear down a surface |

Client → server:

| Message | Purpose |
| --- | --- |
| `action` | User interaction: `name`, `surfaceId`, `sourceComponentId`, `timestamp`, contextual data |

### Data binding

Bindings use **JSON Pointer (RFC 6901)**:

- absolute — `/user/name` resolves from the data model root
- relative — `firstName` resolves within a collection-iteration scope

Any dynamic property is a `DynamicString`: a literal, a `{"path": ...}` object, or a function call. Two-way binding updates the local data model immediately; the server only receives full state when an action fires and `sendDataModel` is enabled.

### Wire example

```json
{"version": "v0.9", "createSurface": {
  "surfaceId": "contact_form_1",
  "catalogId": "https://a2ui.org/specification/v0_9/catalogs/basic/catalog.json"
}}
{"version": "v0.9", "updateComponents": {
  "surfaceId": "contact_form_1",
  "components": [
    {"id": "root", "component": "Column", "children": ["email_field", "submit_button"]},
    {"id": "email_field", "component": "TextField", "value": {"path": "/form/email"}},
    {"id": "submit_button", "component": "Button", "text": "Submit",
     "action": {"event": {"name": "submit", "context": {"email": {"path": "/form/email"}}}}}
  ]
}}
```

### Catalogs

- The **basic catalog** ships with the spec: ~16 general-purpose components (Button, TextField, Card, Column, Row, …). Deliberately minimal so it is implementable everywhere.
- Production apps are expected to define a **custom catalog** reflecting their own design system, rather than adapting the basic one. For us that would be *a WinUI catalog* — `NavigationView`, `InfoBar`, `Expander`, `DataGrid`, Fluent-styled primitives.
- A renderer must: reference the catalog, bind schema properties to real components, implement the visuals, and **degrade gracefully** on unknown components or missing properties.

Docs: [Catalogs](https://a2ui.org/concepts/catalogs/) · [Basic catalog implementation guide v1.0](https://a2ui.org/specification/v1.0-basic-catalog-implementation-guide/) · [Renderer development](https://a2ui.org/guides/renderer-development/)

### Renderer ecosystem

| Renderer | Platform | Status |
| --- | --- | --- |
| React | Web | v0.8 / v0.9.1 stable |
| Lit / Web Components | Web | v0.8 / v0.9.1 stable |
| Angular | Web | v0.8 / v0.9.1 stable |
| Flutter GenUI SDK | iOS / Android / Desktop / Web | v0.8 / v0.9.1 stable |
| SwiftUI | iOS / macOS | planned v1.0 |
| Jetpack Compose | Android | planned v1.0 |
| json-render, A2UI-Android, a2ui-react-native, Lynx A2UI | various | community |
| **.NET / WinUI / WPF / MAUI / Avalonia / Uno** | — | **❌ none in the official list** |

The one .NET implementation that exists is community: **[23min/a2ui-blazor](https://github.com/23min/a2ui-blazor)** — Blazor WASM + Server, ships `A2UI.Blazor` (renderer: `JsonlStreamReader` → `MessageDispatcher` → `SurfaceManager` → `ComponentRegistry` → `DataBindingResolver`) and `A2UI.Blazor.Server` (fluent builders + ASP.NET Core middleware). **Its architecture is directly portable to XAML** — the pipeline is UI-framework-agnostic up to the `ComponentRegistry`.

> **This gap is the opportunity.** A WinUI 3 A2UI renderer does not exist. It is the most concrete, publishable thing this repo could produce.

### A2UI ↔ AG-UI

They were designed to interoperate — CopilotKit contributed to A2UI and shipped AG-UI transport for it. See [CopilotKit: Build with Google's new A2UI spec](https://www.copilotkit.ai/blog/build-with-googles-new-a2ui-spec-agent-user-interfaces-with-a2ui-ag-ui) and Oracle's [Agent Spec + A2UI via AG-UI](https://blogs.oracle.com/ai-and-datascience/announcing-agent-spec-for-a2ui-copilotkit-ag-ui).

---

## 4. MCP-UI and MCP Apps (SEP-1865)

- MCP-UI: <https://mcpui.dev> · <https://github.com/MCP-UI-Org/mcp-ui> (Apache-2.0, ~5k ★)
- MCP Apps spec: <https://github.com/modelcontextprotocol/ext-apps> · [SEP-1865](https://modelcontextprotocol.io/seps/1865-mcp-apps-interactive-user-interfaces-for-mcp)
- Blog posts: [Nov 2025 announcement](https://blog.modelcontextprotocol.io/posts/2025-11-21-mcp-apps/) · [Jan 2026 spec release](https://blog.modelcontextprotocol.io/posts/2026-01-26-mcp-apps/)

### What happened

MCP-UI started as a community extension of MCP's embedded-resources spec, adding a `UIResource` type. It has now been **standardised into MCP core as the MCP Apps extension (SEP-1865)**, authored by MCP core maintainers at OpenAI and Anthropic together with the MCP-UI creators and the MCP UI Community Working Group. The npm/PyPI/gem `mcp-ui` packages continue as the community testing ground ahead of the spec.

### How it works

1. Server pre-declares **UI resources** under the `ui://` URI scheme.
2. Tools reference a UI resource via the `_meta` field, so the host knows what to render for that tool.
3. Hosts receive UI templates **during connection setup**, before tool execution.
4. On tool call, the host renders the resource in a **mandatory sandboxed iframe**.
5. The iframe and host communicate with **JSON-RPC 2.0 over `postMessage`** — the same base protocol as MCP itself. The host is always in control.

Initial spec focuses on `text/html;profile=mcp-app`, with a stated path to future content types.

MCP-UI's own `UIResource` supports three delivery modes: `rawHtml`, `externalUrl`, and `remoteDom` (a remote-DOM script that maps to the host's own components — conceptually closest to A2UI).

SDKs: `@mcp-ui/client`, `@mcp-ui/server` (npm), `mcp_ui_server` (Ruby), `mcp-ui-server` (PyPI).

### Implication for WinUI 3

**This is the least portable of the three.** The security model is *defined in terms of* iframe sandboxing, and the content type is HTML. On WinUI 3 there is exactly one honest way to support it: host a **WebView2** and implement the host side of the postMessage JSON-RPC bridge. That's viable — WebView2 is first-class in Windows App SDK — but the result is a web island in a native app, not a native agentic UI.

The `remoteDom` mode, or a future non-HTML profile, would be the escape hatch. Worth tracking the spec.

Useful deep dives: [WorkOS technical overview](https://workos.com/blog/mcp-ui-a-technical-deep-dive-into-interactive-agent-interfaces) · [fka.dev MCP Apps 101](https://blog.fka.dev/blog/2025-11-22-mcp-apps-101-bringing-interactive-uis-to-ai-conversations/) · [CopilotKit: MCP Apps in your own app via AG-UI](https://www.copilotkit.ai/blog/bring-mcp-apps-into-your-own-app-with-copilotkit-and-ag-ui)

---

## 5. Adjacent protocols worth knowing

| Protocol | Role | Link |
| --- | --- | --- |
| **MCP** | Agent ↔ tools/resources/prompts. The substrate everything else assumes. | <https://modelcontextprotocol.io> |
| **A2A** (Agent2Agent) | Agent ↔ agent. A2UI is defined partly as an A2A extension. | <https://a2a-protocol.org> |
| **Open Agent Specification** (Agent Spec) | Portable declarative agent definitions; Oracle shipped AG-UI + A2UI integration. | [Oracle blog](https://blogs.oracle.com/ai-and-datascience/announcing-ag-ui-integration-for-agent-spec) |
| **OpenAI Apps SDK** | Prior art that MCP Apps drew from. | — |
| **MCP Resources spec** | The base spec MCP-UI extends. | <https://modelcontextprotocol.io/specification/draft/server/resources> |

---

## 6. The .NET / WinUI side of the house

### Microsoft Agent Framework — the practical AG-UI path for .NET

Microsoft ships AG-UI support directly:

| Package | Purpose |
| --- | --- |
| `Microsoft.Agents.AI.AGUI` | Client-side AG-UI implementation. MIT. Targets **.NET 8.0, .NET Standard 2.0, .NET Framework 4.7.2**. |
| `Microsoft.Agents.AI.Hosting.AGUI.AspNetCore` | Host/expose agents over AG-UI from ASP.NET Core |
| `Microsoft.Agents.AI` | Core agent abstractions |

- Docs: [AG-UI Integration with Agent Framework](https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/)
- Repo + samples: <https://github.com/microsoft/agent-framework> · [`dotnet/samples/02-agents/AGUI`](https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AGUI)
- Tracking issue: [.NET support for AG-UI protocol #1774](https://github.com/microsoft/agent-framework/issues/1774)

**The .NET Standard 2.0 target matters a lot**: it means the AG-UI client library is consumable from a WinUI 3 desktop app, not just ASP.NET Core. The documentation frames AG-UI as "web-based", but nothing in the client package requires a browser.

Community Blazor implementation: [lionfire/ag-ui-blazor](https://github.com/lionfire/ag-ui-blazor).
Worked example: [El Bruno — AG-UI + Agent Framework + .NET + Aspire](https://elbruno.com/2025/11/18/%F0%9F%9A%80-ag-ui-agent-framework-net-aspire-web-enabling-your-intelligent-agents-blog-demo-code/).

### MCP C# SDK

- NuGet: `ModelContextProtocol` — official, Microsoft + Anthropic partnership.
- Integrates with **`Microsoft.Extensions.AI`**: `McpClientTool` derives from `AIFunction`, so MCP tools drop straight into .NET AI workflows and can be handed to any `IChatClient`.
- Docs: [Build an MCP server in C# (.NET Blog)](https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/) · [Syncfusion guide](https://www.syncfusion.com/blogs/post/model-context-protocol-csharp-sdk)

### Dynamic XAML at runtime

The "generate the UI directly" approach. `XamlReader.Load(string)` parses XAML into a live object tree.

Constraints (from the WinRT docs and WinUI issues):

- Content must be **well-formed XML, valid XAML, a single root element, with a default `xmlns` declared**.
- **No `x:Class`, no code-behind, no event handler attributes** — you wire up events by walking the returned tree.
- Styles and resources referenced from the loaded fragment are a known pain point ([microsoft-ui-xaml#6582](https://github.com/microsoft/microsoft-ui-xaml/issues/6582)).
- Custom controls from other assemblies need an `IXamlMetadataProvider` registered — see [`DynamicXaml.WinUI`](https://www.nuget.org/packages/DynamicXaml.WinUI/) and [microsoft-ui-xaml#4457](https://github.com/microsoft/microsoft-ui-xaml/issues/4457).
- WinUI's `Application.LoadComponent` differs from WPF's — no overload that returns a new initialised instance.

Docs: [XamlReader.Load](https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.markup.xamlreader.load) · [WinUI3: how to load XAML dynamically (Q&A)](https://learn.microsoft.com/answers/questions/1128155/winui3-how-to-load-xaml-file-dynamically.html) · [XAML runtime design tools for WinUI 3](https://learn.microsoft.com/en-us/windows/apps/develop/ui/xaml-runtime-design-tools)

**Assessment:** letting an LLM emit raw XAML into `XamlReader.Load` is the *fastest* prototype and the *worst* production design — it is arbitrary-markup injection with no schema validation, no allow-list, and a hostile-input surface. Use it to prove the loop, then replace it with a catalog-validated component registry (i.e. A2UI).

### Comparison: other XAML stacks

- **Uno Platform** — WinUI/XAML API-compatible, targets Windows/Linux/macOS/Android/iOS/WASM. A WinUI-shaped A2UI renderer written carefully could run everywhere via Uno.
- **Avalonia** — WPF-like XAML, cross-platform, and notably ships **MCP integration for agents**: an agent can inspect the live visual tree over MCP and generate matching XAML. That's *design-time* generative UI (agent writes your code) rather than *runtime* agentic UI (agent drives your running app) — a useful distinction to keep straight, and prior art worth studying.
- Relevant WinUI discussion: [Proposal: next-generation native Windows UI framework for declarative UI and AI automation (#11126)](https://github.com/microsoft/microsoft-ui-xaml/issues/11126)

### Server-driven UI as the design vocabulary

Everything A2UI does, the mobile world already calls **Server-Driven UI (SDUI)**. The established pattern — component registry mapping server names to native components, schema-as-public-API, versioning, fallback behaviour — is exactly what a WinUI A2UI renderer must implement. Good background: [Nativeblocks: What is SDUI](https://nativeblocks.io/blog/server-driven-ui-definition/) · [WeWeb SDUI guide](https://www.weweb.io/blog/server-driven-ui-guide-architecture-examples).

---

## 7. Windows as an agentic OS

Microsoft's own platform moves are directly relevant — they define what an agent can *do* to a Windows app, which is the other half of agentic UI.

### Native MCP support in Windows

Announced at Build 2025, expanded since: Windows 11 exposes an MCP registry and MCP servers for OS capabilities, so agents can discover and invoke device/app capability under permission.

- [Windows Agentic — Microsoft Developer](https://developer.microsoft.com/en-us/windows/agentic)
- [Securing the Model Context Protocol: building a safer agentic future on Windows](https://blogs.windows.com/windowsexperience/2025/05/19/securing-the-model-context-protocol-building-a-safer-agentic-future-on-windows/)
- [Advancing Windows for AI development (Build 2025)](https://blogs.windows.com/windowsdeveloper/2025/05/19/advancing-windows-for-ai-development-new-platform-capabilities-and-tools-introduced-at-build-2025/)

### App Actions on Windows

The supported way to expose an app's features to agents and to the OS. Implement `IActionProvider`; the app must have **package identity** to register.

- [Get started with App Actions on Windows](https://learn.microsoft.com/en-us/windows/ai/app-actions/actions-get-started) — the walkthrough targets a **packaged C# WinUI 3 desktop app**, which is exactly our scenario.
- App Actions are being surfaced through MCP on Windows 11.

This is the natural implementation of AG-UI's **frontend tools** concept on Windows: the agent's "client-side tool call" becomes an App Action, or a locally registered MCP tool backed by the app.

### Local inference

- [Microsoft Foundry on Windows / Windows AI overview](https://learn.microsoft.com/en-us/windows/ai/overview)
- [Foundry Local — get started](https://learn.microsoft.com/en-us/windows/ai/foundry-local/get-started) — runs LLMs on-device; explicitly documented as working "in a console app, a WinUI 3 app, a WPF app, or any other .NET host".
- [Windows ML GA](https://blogs.windows.com/windowsdeveloper/2025/09/23/windows-ml-is-generally-available-empowering-developers-to-scale-local-ai-across-windows-devices/) — ONNX Runtime based, expanded at Build 2026 to broader model architectures with 4-bit quantisation.

An entirely local agentic UI loop — on-device model → AG-UI events → native XAML — is plausible on current hardware.

---

## 8. Feasibility analysis for WinUI 3

### Three candidate architectures

**A. WebView2 host (fastest, least native)**
Embed WebView2, run an existing web renderer (React/Lit A2UI renderer, or `@mcp-ui/client`), bridge to native via `WebMessageReceived` / `CoreWebView2.AddHostObjectToScript`.
✅ Full protocol compatibility today, including MCP Apps.
❌ Not a native UI. Fluent look must be reimplemented in CSS. Two-runtime complexity.
*Use for: supporting MCP Apps content inside an otherwise-native app.*

**B. Native A2UI renderer for WinUI (the interesting one)**
Port the a2ui-blazor pipeline to XAML: JSONL/SSE reader → message dispatcher → surface manager → **component registry mapping catalog types to WinUI controls** → JSON-Pointer data-binding resolver over an `INotifyPropertyChanged` view-model.
✅ Genuinely native Fluent UI, agent output validated against a schema, no code execution, cross-checkable against other renderers.
✅ Fills a real ecosystem gap — no .NET-native A2UI renderer exists.
❌ Real work: data binding, templates/repeaters, styling, graceful degradation.
*Use for: the core of this repo.*

**C. Direct XAML generation (`XamlReader.Load`)**
LLM emits XAML, app parses it.
✅ Trivial to prototype, maximum expressive range.
❌ No validation, no allow-list, brittle, unsafe with untrusted agents, resource/style resolution problems.
*Use for: a throwaway spike to feel the latency and quality of model-generated layout.*

### Recommended direction

Combine: **AG-UI for the session + A2UI for the presentation + MCP/App Actions for capability**, on architecture **B**, with **A** kept in reserve for MCP Apps interop.

```
Agent (Agent Framework, local or cloud)
  │  MCP tools ── ModelContextProtocol C# SDK ── App Actions (IActionProvider)
  │
  └─ AG-UI event stream (SSE/WebSocket)
        │  Microsoft.Agents.AI.AGUI
        ▼
   WinUI 3 app
     ├─ chat / activity / reasoning panes  ← AG-UI events → view models
     └─ agent-driven surfaces              ← A2UI messages → WinUI component catalog
```

### Known hard problems

1. **Catalog design.** A WinUI catalog is the crux. Too small and agents can't build anything useful; too big and models generate invalid UI. The basic catalog's ~16 components is the calibration point.
2. **Threading.** All XAML mutation must marshal to the UI thread via `DispatcherQueue`. Event streams arrive on background threads at token rate — batching/coalescing will be required or the UI will thrash.
3. **Layout stability under streaming.** `updateComponents` arriving incrementally means the visual tree mutates continuously. Needs a diffing strategy, not naive rebuild.
4. **JSON Pointer binding → XAML binding.** WinUI's `{x:Bind}` is compile-time; `{Binding}` is runtime but needs a suitable source object. Likely need a dynamic data-model proxy (`ICustomPropertyProvider` / `DynamicObject`-like) resolving RFC 6901 paths.
5. **Security.** Catalog validation is the trust boundary. Never `XamlReader.Load` agent output in production. Also: what can an agent-rendered surface *read* from app state?
6. **MCP Apps compatibility.** Spec is HTML-only today. Either accept a WebView2 island or track the spec for a non-HTML profile.
7. **Packaging.** App Actions require package identity — MSIX. Constrains distribution.

---

## 9. Research papers and academic prior art

A useful curated index: <https://awesomegenerativeui.com/papers>

### Foundational

- **Generative UI: LLMs are Effective UI Generators** — Leviathan et al., Google Research, 2025/2026. [arXiv 2604.09577](https://arxiv.org/abs/2604.09577). Argues a properly prompted, tool-equipped modern LLM robustly produces high-quality custom UIs for essentially any prompt, overwhelmingly preferred over markdown output. Introduces the PAGEN dataset. *This is the intellectual foundation of A2UI.*
- **Generative Interfaces for Language Models** — Chen J. et al., ACL 2026 Findings. [arXiv 2508.19227](https://arxiv.org/abs/2508.19227). LLMs proactively generate UIs instead of prose; human evaluators preferred generative interfaces by up to **72%** on information-dense and exploratory tasks.
- **Towards a Working Definition of Designing Generative User Interfaces** — Lee K.-H., DIS 2025. [arXiv 2505.15049](https://arxiv.org/abs/2505.15049). First working definition of GenUI: humans and AI collaborate at *design time* to generate interfaces; users interact with AI-generated interfaces at *runtime*. Useful for keeping the Avalonia-style "agent writes your XAML" case separate from the A2UI-style "agent drives your running UI" case.

### Architecture / systems

- **Macaron-A2UI: A Model for Generative UI in Personal Agents** — Kong F. et al., 2026. [arXiv 2605.24830](https://arxiv.org/abs/2605.24830). Large-scale GenUI corpus; models that emit natural language *plus* lightweight executable UI actions for confirmation, preference refinement, and multi-goal coordination.
- **Software as Content: Dynamic Applications as the Human-Agent Interaction Layer** — Xie M. & Xie Y., 2026. [arXiv 2603.21334](https://arxiv.org/abs/2603.21334). Dynamically generated applications replacing chat-only interaction.
- **Portal UX Agent — A Plug-and-Play Engine for Rendering UIs from Natural Language Specifications** — 2025. [arXiv 2511.00843](https://arxiv.org/abs/2511.00843).
- **Generative and Malleable User Interfaces with Generative AI** — Cao Y. et al., CHI 2025. [arXiv 2503.04084](https://arxiv.org/abs/2503.04084). Task-driven data models driving dynamic form and visualisation generation.
- **Gradual Generation of User Interfaces as a Design Method for Malleable Software** — Min B. et al., 2026. [arXiv 2601.17975](https://arxiv.org/abs/2601.17975).
- **BISCUIT: Scaffolding LLM-Generated Code with Ephemeral UIs in Computational Notebooks** — Cheng R. et al. (Apple), VL/HCC 2024. [arXiv 2404.07387](https://arxiv.org/abs/2404.07387). Ephemeral UI layer between user intent and code generation.

### Quality, alignment, evaluation

- **Bridging Gulfs in UI Generation through Semantic Guidance** — 2026. [arXiv 2601.19171](https://arxiv.org/abs/2601.19171).
- **AlignUI: Designing LLM-Generated UIs Aligned with User Preferences** — Liu Y. et al., 2026. [arXiv 2601.17614](https://arxiv.org/abs/2601.17614).
- **Improving User Interface Generation Models from Designer Feedback** — Wu J. et al., 2025. [arXiv 2509.16779](https://arxiv.org/abs/2509.16779).
- **Efficient Personalization of Generative User Interfaces** — Peng Y.-H. et al., 2026. [arXiv 2604.09876](https://arxiv.org/abs/2604.09876).

### Critique and HCI practice

- **The Keyhole Effect: Why Chat Interfaces Fail at Data Analysis** — Mohan Reddy, 2026. [arXiv 2602.00947](https://arxiv.org/abs/2602.00947). The cognitive-science case against linear conversation. Good motivation section material.
- **What does Generative UI mean for HCI Practice?** — Lindley S. et al., Microsoft Research, CHI EA 2026. [Microsoft Research](https://www.microsoft.com/en-us/research/publication/what-does-generative-ui-mean-for-hci-practice/).
- **Rethinking the UI of GenUI: A Tale of Two Designs** — Chen X. et al., 2026. [arXiv 2606.13843v2](https://arxiv.org/abs/2606.13843v2). Prompt-first vs structured design exploration.
- **Generative AI in Multimodal User Interfaces: Trends, Challenges, and Cross-Platform Adaptability** — 2024. [arXiv 2411.10234](https://arxiv.org/pdf/2411.10234).

### Applied / domain

- **TaskLens: Task-Conditioned Scaffolded Interfaces for Learning Professional Creative Software** — Liu Y. et al., DIS 2026. [ACM DL 3800645.3813081](https://dl.acm.org/doi/10.1145/3800645.3813081).
- **Spatula: On-Demand In-Situ Interfaces for Attribute Control** — Li B. et al., 2026. [arXiv 2607.10405](https://arxiv.org/abs/2607.10405).
- **AI Prototyper: Figma Plugin for Decomposition-Based GUI Prototyping with LLMs** — Salangsingha T. et al., 2026. [arXiv 2607.14830](https://arxiv.org/abs/2607.14830).
- **MAIC-UI: Making Interactive Courseware with Generative UI** — Tu S. et al., 2026. [arXiv 2604.25806](https://arxiv.org/abs/2604.25806).
- **Generative UI as an Accessibility Bridge: Lessons from C2C E-Commerce** — Ryskeldiev B., CHI 2026 workshop. [arXiv 2604.25455](https://arxiv.org/abs/2604.25455).
- **The Missing Layer: Why EdTech Needs Design-Time Generative UI** — Neshaei S. P. et al., 2026. [arXiv 2606.15902](https://arxiv.org/abs/2606.15902).

---

## 10. Reading list / link index

### Specs and official docs

- AG-UI docs — <https://docs.ag-ui.com/introduction>
- AG-UI events reference — <https://docs.ag-ui.com/concepts/events>
- AG-UI repo — <https://github.com/ag-ui-protocol/ag-ui>
- A2UI site — <https://a2ui.org/>
- A2UI v0.9 spec — <https://a2ui.org/specification/v0.9-a2ui/>
- A2UI protocol doc (repo) — <https://github.com/a2ui-project/a2ui/blob/main/specification/v0_8/docs/a2ui_protocol.md>
- A2UI renderers — <https://a2ui.org/reference/renderers/>
- A2UI client setup — <https://a2ui.org/guides/client-setup/>
- MCP Apps spec — <https://github.com/modelcontextprotocol/ext-apps/blob/main/specification/2026-01-26/apps.mdx>
- SEP-1865 — <https://modelcontextprotocol.io/seps/1865-mcp-apps-interactive-user-interfaces-for-mcp>
- MCP Resources — <https://modelcontextprotocol.io/specification/draft/server/resources>
- MCP-UI — <https://mcpui.dev>

### .NET / Windows

- AG-UI integration with Agent Framework — <https://learn.microsoft.com/en-us/agent-framework/integrations/ag-ui/>
- `Microsoft.Agents.AI.AGUI` on NuGet — <https://www.nuget.org/packages/Microsoft.Agents.AI.AGUI/>
- microsoft/agent-framework — <https://github.com/microsoft/agent-framework>
- MCP C# SDK / .NET Blog — <https://devblogs.microsoft.com/dotnet/build-a-model-context-protocol-mcp-server-in-csharp/>
- App Actions on Windows — <https://learn.microsoft.com/en-us/windows/ai/app-actions/actions-get-started>
- Windows Agentic developer hub — <https://developer.microsoft.com/en-us/windows/agentic>
- Windows AI / Foundry on Windows — <https://learn.microsoft.com/en-us/windows/ai/overview>
- WinUI 3 docs — <https://learn.microsoft.com/en-us/windows/apps/winui/winui3/>
- XamlReader.Load — <https://learn.microsoft.com/en-us/uwp/api/windows.ui.xaml.markup.xamlreader.load>

### Community implementations to study

- a2ui-blazor (.NET A2UI renderer) — <https://github.com/23min/a2ui-blazor>
- ag-ui-blazor — <https://github.com/lionfire/ag-ui-blazor>
- Agent Framework AG-UI samples — <https://github.com/microsoft/agent-framework/tree/main/dotnet/samples/02-agents/AGUI>

### Commentary and tutorials

- CopilotKit: Build with Google's A2UI spec — <https://www.copilotkit.ai/blog/build-with-googles-new-a2ui-spec-agent-user-interfaces-with-a2ui-ag-ui>
- CopilotKit: MCP Apps via AG-UI — <https://www.copilotkit.ai/blog/bring-mcp-apps-into-your-own-app-with-copilotkit-and-ag-ui>
- Mete Atamel: A2UI with ADK — <https://atamel.dev/posts/2026/03-30_a2ui_with_adk/>
- WorkOS: MCP-UI technical deep dive — <https://workos.com/blog/mcp-ui-a-technical-deep-dive-into-interactive-agent-interfaces>
- Google Developers Blog: Introducing A2UI — <https://developers.googleblog.com/introducing-a2ui-an-open-project-for-agent-driven-interfaces/>
- MarkTechPost: Google introduces A2UI — <https://www.marktechpost.com/2025/12/22/google-introduces-a2ui-agent-to-user-interface-an-open-sourc-protocol-for-agent-driven-interfaces/>

---

## 11. Open questions

1. Does `Microsoft.Agents.AI.AGUI` work cleanly from a WinUI 3 process, or does it assume ASP.NET Core hosting/DI? (Target framework says it should; needs a spike.)
2. What is the right **WinUI catalog** granularity — mirror the A2UI basic catalog first, or go straight to Fluent-specific components?
3. Can a WinUI A2UI renderer be written against Uno-compatible APIs so it runs cross-platform for free?
4. Is there a credible path to a **non-HTML MCP Apps profile**, or is WebView2 the permanent answer for MCP Apps on desktop?
5. How do **App Actions** map onto AG-UI **frontend tools**? Is there a clean adapter?
6. Streaming layout: what is an acceptable coalescing window before UI churn becomes visible?
7. Does an agent-driven surface need its own **permission model** distinct from the app's, and how is that surfaced to the user?
8. Is there value in publishing the WinUI renderer back to the A2UI ecosystem as the missing .NET-native renderer?
