# Experiment 01 — Static render

- **Status:** Planned
- **Track:** B — native A2UI renderer for WinUI 3
- **Started:** 2026-07-25 · **Completed:** —
- **Depends on:** none (this is the first experiment)

## 1. Goal
Render a hand-written A2UI message stream as native WinUI 3 controls on screen.

## 2. Hypothesis
An A2UI message stream (`createSurface` → `updateComponents` → `beginRendering`) can be read from a static file and turned into a live tree of native WinUI controls, with no LLM, no MCP, and no network.

## 3. Scope

### In scope
- Read a canned `.jsonl` A2UI stream from disk.
- Reconstruct the component tree from the flat adjacency list starting at `root`.
- Map four component types to native WinUI controls via a small catalog.
- Build the control tree once, on the UI thread, when `beginRendering` arrives.
- Display the result inside the host window's surface area.

### Out of scope (deferred)
- **Data model + JSON-Pointer binding** → experiment 02. The fixture uses **literal** property values only; there is no `updateDataModel` and no `{path}`.
- **User actions / the return channel** → experiment 02. The `Submit` button renders but does nothing.
- **Streaming, incremental `updateComponents`, diffing** → experiment 03. The whole stream is read at once and rendered once.
- **Real producer (agent / MCP)** → experiment 04. The "backend" is the static file.
- Custom Fluent catalog, theming, templates/repeaters, `deleteSurface`, graceful degradation of unknown components.

## 4. Components involved

| Component | Role in this experiment | New / reused / stubbed |
| --- | --- | --- |
| Host shell (WinUI 3) | One `Window` with a surface-host panel + a small log pane | New |
| Protocol model | Records for `createSurface` / `updateComponents` / `beginRendering` + component (id, type, props, children); `System.Text.Json` deserialize | New (partial — only 3 messages) |
| Message reader | Read the `.jsonl` file, yield one message per line | New (file source) |
| Dispatcher | Route each message to the SurfaceManager | New (minimal) |
| SurfaceManager | Hold the adjacency list keyed by id; rebuild the tree from `root` | New (minimal) |
| Catalog | Map `Column`/`Text`/`TextField`/`Button` → WinUI controls | New (4 controls) |
| Renderer | Walk the tree, build controls on the `DispatcherQueue` | New (build-once) |
| ~~BindingResolver~~ | — | Deferred → exp 02 |
| ~~ActionChannel~~ | — | Deferred → exp 02 |

## 5. Inputs / fixtures
- [`samples/a2ui/contact-form.jsonl`](../../samples/a2ui/contact-form.jsonl) — three A2UI messages, one per line: create a surface, define five components (a `Column` root containing a title `Text`, two `TextField`s, and a `Button`), begin rendering. Literal values only.

### Catalog mapping used
| A2UI component | Property used | WinUI control | Mapping |
| --- | --- | --- | --- |
| `Column` | `children` | `StackPanel` | `Orientation = Vertical`; children appended in order |
| `Text` | `text` | `TextBlock` | `text` → `Text` |
| `TextField` | `label` | `TextBox` | `label` → `Header` |
| `Button` | `text` | `Button` | `text` → `Content` (no click handler yet) |

The catalog id `local/winui-basic/v0` is a **local, informal** catalog — there is no JSON-Schema validation in this experiment. Formalizing the catalog schema as an allow-list is a later concern.

## 6. Steps
1. Create `src/exp-01-static-render/` (Run phase) — a minimal packaged WinUI 3 desktop app.
2. Add the protocol records and a `System.Text.Json` reader for the three message types.
3. Implement SurfaceManager: apply the messages, expose the resolved `root` tree.
4. Implement the catalog (4 factories) and the renderer (tree walk → controls).
5. On window load: read `contact-form.jsonl`, dispatch messages, render into the surface host on `beginRendering`.
6. Log each parsed message to the log pane.

## 7. Expected result
The window shows a vertical stack: the heading **"Contact us"**, a **Name** text box, an **Email** text box, and a **Submit** button — all native WinUI controls. The log pane lists the three messages read. The button is inert.

## 8. Success criteria
- [ ] All three messages parse without error.
- [ ] The tree is reconstructed from the adjacency list (children resolved by id).
- [ ] Four native WinUI controls render in the correct order inside a `StackPanel`.
- [ ] No binding, action, or streaming code is required to get there (confirms the scope line held).

## 9. Results
### Actual result
_TBD — filled during the run._

### ✅ What worked
_TBD._

### ❌ What didn't work
_TBD._

### Open questions
_TBD._

## 10. Outcome & next
_TBD. Expected next: experiment 02 — binding-and-actions (introduce the data model, JSON-Pointer `BindingResolver`, and the action return channel)._
