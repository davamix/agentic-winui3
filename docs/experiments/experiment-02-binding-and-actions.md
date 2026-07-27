# Experiment 02 — Binding and actions

- **Status:** Planned
- **Track:** B — native A2UI renderer for WinUI 3
- **Started:** 2026-07-27 · **Completed:** —
- **Depends on:** [experiment 01 — static-render](./experiment-01-static-render.md)

## 1. Goal
Make a rendered A2UI surface *reactive*: `{path}` values resolve against a data model, edits flow back into it, and a `Submit` action carries that state out and is answered by an update that changes the live UI.

## 2. Hypothesis
A2UI `{"path": …}` bindings can be resolved against a host-held data model via JSON Pointer, kept in sync **two-way** with native WinUI controls, and a `Button` action can carry the current model state out as an A2UI `action` message and be answered with an `updateDataModel` that updates the **already-built** control tree in place — with no rebuild, no LLM, no MCP, and no network.

The "in place" clause is the falsifiable part that matters. Experiment 01 rendered once and replaced `SurfaceHost.Child` wholesale; if the only way to reflect a data change is to rebuild, this hypothesis is refuted and binding is not really binding.

## 3. Scope

### In scope
- `updateDataModel` → a host-held data model, keyed by surface.
- **JSON Pointer (RFC 6901)** resolution of absolute paths (`/form/email`) for both read and write.
- **One-way** binding: `Text.text` given as `{"path": …}` shows the model value, and re-shows it when the model changes.
- **Two-way** binding: `TextField.value` seeds from the model, and typing writes back to it.
- Literal and bound values coexisting for the same property (`title` is a literal `text`, `status_text` is a bound `text`) with no parser change.
- `action.event.name` + `action.event.context` on `Button`, with `{path}` context entries resolved against the model **at click time**.
- The client→server `action` message: `name`, `surfaceId`, `sourceComponentId`, `timestamp`, `context`.
- Closing the loop: a **scripted responder** maps an action name to a canned `updateDataModel` (see §5), which re-enters the same dispatcher the file stream uses.

### Out of scope (deferred)
- **Relative pointers and collection-iteration scope** (`{"path": "name"}` inside a repeated template). There is no repeater in this fixture, so only absolute pointers are implemented → deferred to whichever experiment introduces templates.
- **Functions in `DynamicString`.** A dynamic property here is *either* a literal *or* `{"path": …}`; the spec's third form (function call) is not implemented.
- **`sendDataModel`.** `createSurface` may request that the full data model accompany every action. This fixture does not set it, so the action carries **only its declared `context`** — the narrower and more interesting case, since it forces the context resolution to actually work.
- **Streaming, incremental `updateComponents`, diffing** → experiment 03. The responder deliberately replies with `updateDataModel` **only**; it never adds or changes a component, which is exactly what keeps re-render/diffing out of this experiment. See [Why the responder may not send `updateComponents`](#why-the-responder-may-not-send-updatecomponents).
- **Real producer (agent / MCP)** → experiment 04. The responder is a canned fixture, not an agent.
- **A real transport.** The action is handed to an in-process channel, not serialized over a socket. The *message* is built and shown in full; only the wire is missing.
- Catalog schema validation, `deleteSurface`, graceful degradation, theming, and the heading-vs-body gap from experiment 01 (still [research.md §11 Q9](../research.md#11-open-questions)).

## 4. Components involved

| Component | Role in this experiment | New / reused / stubbed |
| --- | --- | --- |
| Host shell (WinUI 3) | Same two panes; the log now also shows outbound actions and inbound responses | Reused, extended |
| Protocol model | Adds `updateDataModel`, the `action` declaration on a component, the outbound `action` message, and `DynamicString` (literal \| `{path}`) | Reused, extended |
| Message reader | Unchanged — the responder's canned reply is read by the same reader | Reused as-is |
| Dispatcher | Adds an `updateDataModel` route; still never renders | Reused, extended |
| SurfaceManager / Surface | Surface now owns a `DataModel` alongside its adjacency list | Reused, extended |
| **DataModel** | JSON-Pointer get/set over a mutable JSON object; raises a change event per path | **New** |
| **BindingResolver** | Turns a `DynamicString` into a value, and subscribes a control to later changes of that path | **New** |
| Catalog | Same four component types, now binding-aware: `TextField` binds two-way, `Text` binds one-way, `Button` wires `Click` → action | Reused, extended |
| Renderer | Still build-once; now hands each factory the model so factories can bind | Reused, extended |
| **ActionChannel** | Builds and emits the client→server `action` message | **New** |
| **ScriptedResponder** | Canned action-name → response-fixture map; stands in for experiment 04's agent | **New (stub)** |

### How they interact

Two flows now, not one. The **inbound** flow is experiment 01's, plus a data-model route. The **outbound** flow is new, and it re-enters the inbound flow through the same dispatcher — which is the point: the responder is not privileged, it speaks the same protocol as the file.

```mermaid
sequenceDiagram
    autonumber
    participant User
    participant UI as Rendered controls
    participant Bind as BindingResolver
    participant Model as DataModel
    participant Act as ActionChannel
    participant Resp as ScriptedResponder
    participant Disp as MessageDispatcher

    Note over UI,Model: build-once render has already happened;<br/>controls are subscribed to their paths

    rect rgba(128,128,128,0.08)
    Note right of User: two-way binding
    User->>UI: types "Daniel" in Name
    UI->>Model: Set("/form/name", "Daniel")
    end

    rect rgba(128,128,128,0.08)
    Note right of User: action out
    User->>UI: clicks Submit
    UI->>Act: Send(actionDeclaration, sourceComponentId)
    Act->>Model: resolve each {path} in context
    Model-->>Act: "Daniel", "daniel@example.com"
    Act-->>Disp: action message (logged)
    end

    rect rgba(128,128,128,0.08)
    Note right of Resp: response in — same protocol, same dispatcher
    Act->>Resp: OnAction("submit", context)
    Resp->>Disp: updateDataModel /form/status
    Disp->>Model: Set("/form/status", "Thanks, Daniel! …")
    Model-->>Bind: Changed("/form/status")
    Bind->>UI: TextBlock.Text = new value
    end

    Note over UI: same control instance —<br/>no rebuild, no new SurfaceHost.Child
```

#### Why the responder may not send `updateComponents`

This is the scope line of the whole experiment, so it is worth stating plainly. A responder that replied with `updateComponents` would force a re-render, and re-render is experiment 01's [open question 4](./experiment-01-static-render.md#open-questions): replacing `SurfaceHost.Child` destroys focus, caret and any typed-but-unsent text. Solving that is *diffing*, which is experiment 03.

By restricting the response to `updateDataModel`, the round trip is closed **without** a rebuild — and whether that actually holds is success criterion 5, checked by asserting the root control is the same object before and after.

## 5. Inputs / fixtures

Two fixtures, both under [`samples/a2ui/`](../../samples/a2ui/). Experiment 01's [`contact-form.jsonl`](../../samples/a2ui/contact-form.jsonl) is left untouched so that experiment reproduces exactly.

**1. [`contact-form-bound.jsonl`](../../samples/a2ui/contact-form-bound.jsonl)** — the same contact form, now bound. Four messages:

| # | Message | What it adds over experiment 01 |
| --- | --- | --- |
| 1 | `createSurface` | unchanged |
| 2 | `updateDataModel` | **new** — seeds `/form` with empty `name`, `email`, and a `status` prompt. Sent *before* the components, so the model exists when the tree is built |
| 3 | `updateComponents` | six components: `title` (literal text), two `TextField`s bound to `/form/name` and `/form/email`, `submit_button` carrying an action, and **`status_text`** bound to `/form/status` |
| 4 | `beginRendering` | unchanged |

**2. [`submit-response.jsonl`](../../samples/a2ui/submit-response.jsonl)** — one `updateDataModel` line, replayed when the action named `submit` fires.

> **Note — `${name}` is not A2UI.** The response fixture's value contains `${name}` / `${email}` placeholders that the `ScriptedResponder` substitutes from the action context before dispatching. That substitution is **stub behaviour standing in for an agent**, not part of the protocol; a real producer in experiment 04 would compose the string itself. It earns its place because it makes the round trip *evidential* — seeing your own typed name come back proves the `context` genuinely travelled, where a fixed "Submitted." would not. `${…}` is used rather than `{…}` precisely so it cannot be mistaken for a binding.

### Catalog mapping used

`local/winui-basic/v0`, unchanged in *membership* — still four component types — but three of them gain binding behaviour:

| A2UI component | Property | WinUI control | Mapping in this experiment |
| --- | --- | --- | --- |
| `Column` | `children` | `StackPanel` | unchanged |
| `Text` | `text` | `TextBlock` | literal **or** `{path}` → `Text`, re-applied on model change (**one-way**) |
| `TextField` | `label`, `value` | `TextBox` | `label` → `Header`; `value` `{path}` → `Text`, and `TextChanged` writes back (**two-way**) |
| `Button` | `text`, `action` | `Button` | `text` → `Content`; `action` → `Click` handler that emits the action message |

**Known fidelity gap, carried forward deliberately.** The spec's basic catalog gives `Button` a **`child`** (a referenced component supplies the label), not a `text` property. This local catalog keeps `text`, as in experiment 01, so the fixture diff against experiment 01 is *only* the binding and action additions — which keeps the experiment readable. It is a divergence from the published basic catalog and is recorded as an open question rather than silently absorbed.

## 6. Steps

Planned; refined with what actually happened during the run.

1. Scaffold `src/exp-02-binding-and-actions/` from experiment 01's project, carrying over the shell, protocol, surfaces and rendering folders unchanged, and linking both new fixtures as content.
2. Extend the protocol: `UpdateDataModel`, `DynamicString` (literal \| `{path}`), the `action` declaration on `ComponentNode`, and the outbound `A2uiAction` record.
3. Build `DataModel` — JSON-Pointer get/set with a change event. Verify in a console harness *before any UI*, as experiment 01 found worthwhile.
4. Build `BindingResolver`, and make the catalog binding-aware (two-way `TextField`, one-way `Text`).
5. Build `ActionChannel` + `ScriptedResponder`, wiring the response back into the dispatcher.
6. Wire the host: route `updateDataModel`, log outbound actions and inbound responses, and assert the root control identity is stable across the round trip.
7. Build, run, capture the screenshot evidence.

## 7. Expected result
The window shows the contact form as before, plus a status line reading **"Fill in the form, then press Submit."** — proving that line came from the *data model*, not the component. Typing a name and email, then clicking **Submit**, logs the outbound `action` JSON with both typed values resolved into its `context`, and the status line changes in place to **"Thanks, Daniel! We'll reply to daniel@example.com."** while focus and the typed field contents survive untouched.

## 8. Success criteria
- [ ] `updateDataModel` parses and seeds the model; the initial `/form/status` value is visible on screen (one-way `{path}` → control).
- [ ] Typing in the `Name` field changes `/form/name` in the model (control → model; verified by the value appearing in the action context, not by inspection).
- [ ] Clicking `Submit` produces an `action` message carrying `name`, `surfaceId`, `sourceComponentId`, `timestamp`, and a `context` whose `{path}` entries hold the **current typed** values.
- [ ] The scripted `updateDataModel` reaches the model and the status `TextBlock` shows the new text.
- [ ] **That update happens in place:** the root control is the same instance before and after, and `SurfaceHost.Child` is never reassigned after the initial render.
- [ ] Literal and bound values coexist for the same property (`title` literal, `status_text` bound) with no parser change.

## 9. Results
<!-- Filled during/after the run. -->
### Actual result

### ✅ What worked

### ❌ What didn't work

### Open questions

## 10. Outcome & next
