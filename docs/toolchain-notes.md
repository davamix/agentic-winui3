# Toolchain & environment notes

> Problems that cost time but had **nothing to do with the hypothesis under test** — the WinUI toolchain, the build/run loop, driving and capturing the running app, and the docs themselves. Written down so each one is solved once.

**What belongs here:** anything about the *environment or the tools*. A template that generates contradictory files, a screenshot that captures the wrong window, a Markdown extension with a surprising parser.

**What does not:** findings about A2UI, the renderer's design, or the claim an experiment was testing. Those stay in the experiment write-up, and if they are lasting they get promoted to [research.md §11](./research.md#11-open-questions).

The experiment documents are a chronological log and are never rewritten, so the same problem may also appear in the "❌ What didn't work" section of whichever experiment hit it first. This file is the cross-cutting index; entries link back to where each one surfaced.

## How to add an entry

**Symptom** (what you actually see, so it is searchable) → **Cause** → **Fix**. Say which experiment found it.

If a later experiment finds a better answer, mark the old entry **Superseded** and link forward rather than deleting it — the wrong turn is the part worth keeping, because it is the one you would otherwise take again.

## Index

| # | Symptom | Area |
| --- | --- | --- |
| [1.1](#11-dotnet-new-has-no-winui-template) | `dotnet new winui` → "No templates found" | Project setup |
| [1.2](#12-a-copied-experiment-project-needs-a-fresh-msix-identity) | Two experiment apps fight over one debug identity | Project setup |
| [1.3](#13-the-winui-template-writes-files-it-also-gitignores) | Generated `.pubxml` files can never be committed | Project setup |
| [1.4](#14-the-template-steers-toward-page-navigation) | Scaffold has `Frame` + `MainPage` an A2UI host does not need | Project setup |
| [2.1](#21-dotnet-run-works-on-a-packaged-app) | Assuming Visual Studio is needed for a packaged app | Build & run |
| [2.2](#22-test-the-non-ui-layers-in-a-console-harness) | Debugging parse/state logic through a window | Build & run |
| [3.1](#31-screenshots-come-out-cropped) | Capture is cropped or misaligned | Driving the app |
| [3.2](#32-the-screenshot-captures-the-wrong-window) | Capture shows some other app entirely | Driving the app |
| [3.3](#33-driving-the-ui-with-keystrokes-is-unsafe) | Synthetic keystrokes land in the wrong window | Driving the app |
| [4.1](#41-mermaid-treats--as-a-statement-separator) | `Parse error on line N` in a Mermaid diagram | Docs |
| [4.2](#42-git-warns-lf-will-be-replaced-by-crlf-on-every-commit) | Warning noise on every commit | Docs / git |

---

## 1. Toolchain & project setup

### 1.1 `dotnet new` has no WinUI template

**Symptom** — `dotnet new winui` fails; no WinUI template is listed even with a full Visual Studio 2026 install including `WindowsAppSdkSupport.CSharp`.

**Cause** — The WinUI templates ship as a separate template pack, not with the SDK or the VS workload.

**Fix**
```bash
dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
dotnet new winui -n Exp0N.Slug -o src/exp-0N-slug
```

⚠️ **Reproducibility gap.** That install is **machine-wide and not captured in this repo**, so a fresh clone cannot reproduce the scaffolding step without running it first. Nothing in the build fails as a result — the generated projects are self-contained — but the *scaffold* command is not reproducible from the repo alone.

*Found in [experiment 01](./experiments/experiment-01-static-render.md).*

### 1.2 A copied experiment project needs a fresh MSIX identity

**Symptom** — Two experiment apps behave as one package: registering the debug identity for the newer one displaces the older one, and `dotnet run` may launch the wrong app.

**Cause** — Each experiment is scaffolded by copying the previous one (deliberately — it keeps the diff meaningful). The copied `Package.appxmanifest` carries the *same package identity*, and to Windows two apps sharing an `Identity Name` are the same package.

**Fix** — Generate a new GUID and replace it in **both** places in `Package.appxmanifest`:

```xml
<Identity Name="NEW-GUID-HERE" Publisher="CN=AppPublisher" Version="1.0.0.0" />
<mp:PhoneIdentity PhoneProductId="NEW-GUID-HERE" PhonePublisherId="00000000-0000-0000-0000-000000000000"/>
```

```powershell
[guid]::NewGuid().ToString().ToUpper()
```

Also rename `RootNamespace`, the `.csproj`, the `x:Class` / `xmlns:local` values, `Properties/launchSettings.json` profile names, and the `DisplayName` entries.

> This one was *anticipated rather than observed* — the identity was changed during exp-02's scaffold, so the collision never happened. The reasoning is what is recorded, not a war story.

*Applied in [experiment 02](./experiments/experiment-02-binding-and-actions.md#1-scaffold-srcexp-02-binding-and-actions-done).*

### 1.3 The WinUI template writes files it also gitignores

**Symptom** — `Properties/PublishProfiles/*.pubxml` exists on disk but git ignores it; the files could never be committed.

**Cause** — The template emits publish profiles *and* a nested `.gitignore` that excludes them. It contradicts itself.

**Fix** — Delete `Properties/PublishProfiles/` and the now-dead `<PublishProfile>` property; they are publish-only. Delete the nested `.gitignore` too — the repo-root one already covers `bin/`, `obj/` and MSIX output.

*Found in [experiment 01](./experiments/experiment-01-static-render.md).*

### 1.4 The template steers toward page navigation

**Symptom** — The generated shell is `Window` → `Frame` → `MainPage`, machinery an A2UI host does not use.

**Fix** — Strip to a single `Window`. The root `Grid`'s `Loaded` event replaces `Page.Loaded` as the startup hook.

*Found in [experiment 01](./experiments/experiment-01-static-render.md).*

---

## 2. Build & run loop

### 2.1 `dotnet run` works on a packaged app

Not a problem — a convenience worth not forgetting, because the opposite is easy to assume.

Current templates pull in `Microsoft.Windows.SDK.BuildTools.WinApp`, which hooks the .NET CLI `Run` target to register a debug identity and launch the app *with package identity* (AUMID). **The whole build/run loop is CLI-only; Visual Studio is not needed.**

```bash
dotnet build src/exp-0N-slug/Exp0N.Slug.csproj
dotnet run   --project src/exp-0N-slug/Exp0N.Slug.csproj
```

Launching prints the AUMID and PID:
```
✅ 9A8734DA-…_1z32rh13vfry6 launched (PID: 19032)
```

Killing the app with `Stop-Process` makes the parent `dotnet run` exit **255**. That is expected, not a failure.

*Established in [experiment 01](./experiments/experiment-01-static-render.md).*

### 2.2 Test the non-UI layers in a console harness

**Symptom** — Verifying parsing, tree resolution or state logic by launching the window, clicking, and reading a log pane. Slow, and UI problems get confused with logic problems.

**Fix** — A throwaway console project (in a scratch folder, *not* in the repo) that `Compile`-includes the experiment's UI-free folders:

```xml
<PropertyGroup>
  <OutputType>Exe</OutputType>
  <TargetFramework>net10.0</TargetFramework>
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  <Exp>f:\Development\agentic-winui3\src\exp-0N-slug</Exp>
</PropertyGroup>
<ItemGroup>
  <Compile Include="Program.cs" />
  <Compile Include="$(Exp)\Protocol\*.cs" />
  <Compile Include="$(Exp)\Surfaces\*.cs" />
</ItemGroup>
```

`internal` types are reachable because the sources are compiled *into* the harness assembly, so nothing needs `InternalsVisibleTo`.

Both experiments that used this reported the same benefit: the UI step afterwards was purely about the WinUI mapping, with everything else already proven. Exp-02 ran 24 checks green before any XAML existed.

**Bonus signal:** if a layer *cannot* be compiled into the harness, it has a UI dependency — worth asking whether it needs one. This is what kept `BindingResolver` free of any WinUI type.

*Recommended by [experiment 01](./experiments/experiment-01-static-render.md#-what-worked), repeated in [experiment 02](./experiments/experiment-02-binding-and-actions.md#3-datamodel-done).*

---

## 3. Driving and capturing the running app

### 3.1 Screenshots come out cropped

**Symptom** — The captured PNG is cropped or offset relative to the window.

**Cause** — PowerShell is DPI-unaware. `GetWindowRect` returns physical pixels while the process sees virtualised, scaled coordinates, so the rectangle and the capture disagree.

**Fix** — Call `SetProcessDPIAware()` **before** measuring anything. Still required regardless of which capture method follows.

```powershell
Add-Type @'
using System; using System.Runtime.InteropServices;
public class Cap {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[void][Cap]::SetProcessDPIAware()
```

*Found in [experiment 01](./experiments/experiment-01-static-render.md).*

### 3.2 The screenshot captures the wrong window

**Symptom** — The PNG shows a completely different application: a browser, a video, whatever happened to be in front. No error is raised; the capture simply is not the app.

**Cause** — Bringing the window forward with `SetForegroundWindow` and then screen-scraping with `Graphics.CopyFromScreen`. **Windows refuses foreground activation requested by a background process**, so the call returns without doing anything and the scrape captures whatever is genuinely on top at those coordinates.

**Fix** — Do not compete for focus; capture the window's own composited content instead. `PrintWindow` with `PW_RENDERFULLCONTENT` (**flag `2`** — the flag is required for WinUI 3's composited windows; without it you get a blank or partial bitmap) works while the window is occluded.

```powershell
$rect = New-Object Cap+RECT
[void][Cap]::GetWindowRect($handle, [ref]$rect)
$bmp = New-Object System.Drawing.Bitmap ($rect.Right - $rect.Left), ($rect.Bottom - $rect.Top)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$hdc = $g.GetHdc()
[void][Cap]::PrintWindow($handle, $hdc, 2)   # 2 = PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc); $g.Dispose()
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png); $bmp.Dispose()
```

> **Supersedes the capture method in [experiment 01](./experiments/experiment-01-static-render.md).** §3.1's DPI fix still stands; the foreground-activation part of that approach was never reliable, only lucky — it worked when nothing else happened to be covering the window.

*Found in [experiment 02](./experiments/experiment-02-binding-and-actions.md#-what-didnt-work).*

### 3.3 Driving the UI with keystrokes is unsafe

**Symptom** — Automating "type into the field, press the button" with `WScript.Shell.SendKeys` requires the app to have focus. If activation fails ([§3.2](#32-the-screenshot-captures-the-wrong-window) — it usually does), the keystrokes go to **whatever the user actually has focused**. Test input can end up typed into someone's chat window.

**Fix** — Use **UI Automation**, which needs no foreground focus and addresses controls directly.

```powershell
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes   # works in pwsh 7
$UIA  = [System.Windows.Automation.AutomationElement]
$Tree = [System.Windows.Automation.TreeScope]::Descendants

# The window, by process id
$cond = New-Object System.Windows.Automation.PropertyCondition($UIA::ProcessIdProperty, $proc.Id)
$win  = $UIA::RootElement.FindFirst([System.Windows.Automation.TreeScope]::Children, $cond)

# Set a text box
$edit = $win.FindAll($Tree, (New-Object System.Windows.Automation.PropertyCondition(
    $UIA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)))[0]
$edit.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue('Daniel')

# Press a button by its label
$btn = $win.FindFirst($Tree, (New-Object System.Windows.Automation.PropertyCondition(
    $UIA::NameProperty, 'Submit')))
$btn.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
```

Two things confirmed in exp-02:

- **`ValuePattern.SetValue` does raise `TextChanged`**, so two-way binding sees it exactly as it sees typing. Verified indirectly and strongly: the values arrived in the outbound action's context, which is only populated from the data model.
- **Enumerating `ControlType.Text` afterwards dumps the whole surface *and* the log pane as plain text** — greppable, diffable evidence that a screenshot cannot give you. Capture both.

*Found in [experiment 02](./experiments/experiment-02-binding-and-actions.md#7-verification-method).*

---

## 4. Documentation & diagrams

### 4.1 Mermaid treats `;` as a statement separator

**Symptom** — A diagram that looks fine fails to render, with a parse error pointing at the *middle* of a line:

```
Parse error on line 11:
...lready happened;<br/>controls are subscr
-----------------------^
```

**Cause** — `;` separates statements in Mermaid, exactly like a newline. A semicolon inside `Note` text ends the note, and the remainder of the line is parsed as a new statement — which is nonsense, hence the error.

**Fix** — Use an em dash or a comma in note text. `<br/>` for line breaks is fine and is unaffected.

```
Note over UI,Model: build-once render has already happened —<br/>controls are subscribed to their paths
```

**Check diagrams before committing** — a broken one is otherwise discovered by whoever opens the page. `mermaid.parse` validates without rendering:

```bash
npm i mermaid jsdom
node check.mjs docs/experiments/experiment-0N-slug.md
```

```js
// check.mjs — parses every ```mermaid block in a Markdown file
import fs from 'node:fs';
import { JSDOM } from 'jsdom';

const dom = new JSDOM('<!doctype html><html><body></body></html>');
global.window = dom.window;
global.document = dom.window.document;
// node 24 exposes `navigator` as a getter-only global, so assignment throws
Object.defineProperty(global, 'navigator', { value: dom.window.navigator, configurable: true });

const { default: mermaid } = await import('mermaid');
const md = fs.readFileSync(process.argv[2], 'utf8');
const blocks = [...md.matchAll(/```mermaid\n([\s\S]*?)```/g)].map((m) => m[1]);

mermaid.initialize({ startOnLoad: false });

let failed = 0;
for (const [i, code] of blocks.entries()) {
  try {
    await mermaid.parse(code);
    console.log(`block ${i + 1}: OK`);
  } catch (err) {
    failed++;
    console.log(`block ${i + 1}: FAILED\n${err.message}`);
  }
}
process.exit(failed === 0 ? 0 : 1);
```

Keep this in a scratch folder — it is a check, not a dependency of the repo.

*Found in [experiment 02](./experiments/experiment-02-binding-and-actions.md).*

### 4.2 Git warns "LF will be replaced by CRLF" on every commit

**Symptom** — Every `git add` / `git commit` prints a warning per file. Harmless, but it buries real output.

**Cause** — Files are written with LF, `core.autocrlf` is `true` on this machine, and the repo has no `.gitattributes` to state its intent.

**Status: unfixed, cosmetic.** The fix would be a `.gitattributes` at the repo root:

```gitattributes
* text=auto
```

Not applied yet because it would renormalise existing files and produce a large, content-free diff. Worth doing at a natural break point rather than mid-experiment. Until then, filtering is enough:

```powershell
git commit -m '…' 2>&1 | Select-String -NotMatch 'LF will be replaced'
```
