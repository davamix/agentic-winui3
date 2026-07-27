# Experiment Setup Guide

> How to set up, run, and document an experiment in this repo. Follow this for **every** experiment so they are all shaped the same way and stay comparable over time.

This guide is the process. The per-experiment write-up uses [`_template.md`](./_template.md). The list of all experiments lives in [`README.md`](./README.md).

---

## Lifecycle at a glance

```
DEFINE ──▶ SET UP ──▶ RUN ──▶ LOG ──▶ CONCLUDE
(goal &    (files &   (build  (fill   (status,
 scope)     fixtures)  code)   results) outcome, next)
```

- **Define** and **Set up** happen *before* any code — that is what "setting up an experiment" means here.
- **Run**, **Log**, **Conclude** happen while and after you build.

A doc is created at **Set up** time with status `Planned`, and is filled in as you move through the later phases. Nothing is written retroactively — the log grows with the work.

---

## Conventions

| Thing | Rule |
| --- | --- |
| Experiment doc location | `docs/experiments/experiment-NN-<slug>.md` |
| Number `NN` | Two digits, zero-padded, sequential: `01`, `02`, … |
| `<slug>` | kebab-case, names what is proven, not how: `static-render`, `binding-and-actions` |
| Sample / fixture data | `samples/<protocol>/<name>.<ext>` (e.g. `samples/a2ui/contact-form.jsonl`) |
| Experiment code | `src/exp-NN-<slug>/` — created **only in the Run phase**, one self-contained folder per experiment unless it builds on a shared library (record which in the doc) |
| Status values | `Planned` · `In progress` · `Done ✅` · `Blocked` · `Abandoned` |
| Index | Every experiment has a row in [`README.md`](./README.md) |

We do **not** create empty `src/` folders ahead of time — git does not track empty directories, and an experiment may never need code. Code appears when the Run phase starts.

---

## Step by step

### Phase 1 — Define (before any files)

1. **State the goal in one sentence.** If you can't, the experiment is too big — split it.
2. **Write the hypothesis** as a falsifiable claim ("*X can be done with Y*"), not a task.
3. **Draw the scope line.** List what is explicitly *out* (deferred to a later experiment). Minimalism is the point: an experiment proves *one* thing.

### Phase 2 — Set up (still no code)

4. **Pick `NN` and `<slug>`** using the conventions above.
5. **Copy the template:**
   ```bash
   cp docs/experiments/_template.md docs/experiments/experiment-NN-<slug>.md
   ```
6. **Fill the definition sections** of the new doc: Goal, Hypothesis, Scope, Components involved, Inputs/fixtures, Expected result, Success criteria. Leave the Results and Outcome sections empty (they are filled later).
7. **Create fixture data** under `samples/` if the experiment needs canned input, and link it from the doc.
8. **Register the experiment** in [`README.md`](./README.md) with status `Planned`.
9. **Commit** the setup: doc + fixtures, status `Planned`. Now the experiment is "set up".

### Phase 3 — Run

10. **Create the code** under `src/exp-NN-<slug>/`. Keep it to the *minimum that tests the hypothesis* — no extra features, no polish.
11. Set the doc status to `In progress`.

### Phase 4 — Log (while running)

12. Fill **Steps** with what you actually did (commands, decisions).
13. Fill **Actual result**, then the two discipline sections: **✅ What worked** and **❌ What didn't work**. The second is not optional — it is where the learning is.
14. Record any **Open questions** as they arise.

### Phase 5 — Conclude

15. Write the **Outcome & next**: did the hypothesis hold? Which experiment follows?
16. Set the final **status** (`Done ✅`, `Blocked`, or `Abandoned`) and dates.
17. **Update the index** row in [`README.md`](./README.md).
18. **Feed findings back:** move any lasting open questions into [`../research.md`](../research.md) §11, and if a result changes the project's direction, update the relevant part of `research.md`.
19. **Split off the environment problems.** Anything that cost time but had nothing to do with the hypothesis — a template that generates contradictory files, a screenshot that captures the wrong window, a tool with a surprising parser — goes to [`../toolchain-notes.md`](../toolchain-notes.md). It stays in the experiment's "❌ What didn't work" as well; that section is a log and is not rewritten. The point of copying it out is that the next experiment will not think to read this one's write-up.

---

## The experiment document

Copied from [`_template.md`](./_template.md). Sections and when each is filled:

| Section | Filled during |
| --- | --- |
| Header (status, track, dates, depends-on) | Set up, updated through lifecycle |
| 1. Goal | Define |
| 2. Hypothesis | Define |
| 3. Scope (in / out) | Define |
| 4. Components involved | Set up |
| 5. Inputs / fixtures | Set up |
| 6. Steps | Run / Log |
| 7. Expected result | Set up |
| 8. Success criteria | Set up |
| 9. Results (actual / worked / didn't / open questions) | Log |
| 10. Outcome & next | Conclude |

---

## Definition of Done

An experiment is `Done ✅` when:

- [ ] The hypothesis is answered — confirmed *or* refuted (a refuted hypothesis is a successful experiment).
- [ ] Actual result is recorded, with **✅ what worked** and **❌ what didn't**.
- [ ] Open questions are captured (in the doc, and promoted to `research.md` §11 if lasting).
- [ ] Any **environment / tooling** problems are copied to [`../toolchain-notes.md`](../toolchain-notes.md).
- [ ] Every Mermaid diagram in the write-up **parses** (see [toolchain-notes §4.1](../toolchain-notes.md#41-mermaid-treats--as-a-statement-separator)).
- [ ] The next experiment is named in **Outcome & next**.
- [ ] The index row in `README.md` is updated.

---

## Why this discipline

Each experiment is a controlled test of *one* claim against *fixed* inputs. Keeping the shape identical every time means results are comparable, "what didn't work" is never lost, and a newcomer can read `README.md` top to bottom and understand how the whole thing was learned.
