# Native RichEditor

`Microsoft.Maui.Controls.RichEditor` is a separate native editing control.
It does not extend or replace `Editor`, and its document is not a plain string.
Android uses AppCompatEditText with spans, iOS and Mac Catalyst use UITextView,
and Windows uses RichEditBox.

## Usage

```csharp
var editor = new RichEditor
{
    Document = RichTextDocument.FromText("Select and format this text.")
};
editor.Select(0, 6);
editor.ApplyBold();
editor.ApplyFont(null, 24);
editor.ApplyList(RichTextListKind.Numbered);

string markdown = editor.GetMarkdown();
editor.LoadMarkdown(markdown);
```

Use the [RichEditor gallery page](../../src/Controls/samples/Controls.Sample/Pages/Controls/RichEditorPage.xaml)
to exercise formatting, lists, selection, undo/redo and Markdown conversion.
Formatting UI is supplied by the application; it is not built into the editor.

## Document and selection

`RichTextDocument` is an immutable collection of `RichTextParagraph` objects.
A paragraph has immutable `RichTextRun` objects with `RichTextStyle` formatting,
plus a list kind, zero-based list level and positive starting number.
Collections are copied at construction. Adjacent runs with equal styles are
merged. Nested list paragraphs require a preceding parent list paragraph.

`Document.Text` is a plain-text projection. It contains one `\n` between
paragraphs and no list markers. `CursorPosition` and `SelectionLength` use
UTF-16 offsets into this projection. Use `Select(start, length)` to update both
atomically; ranges must be valid and cannot split a Unicode surrogate pair.
Native paragraph separators and markers are adapter concerns.

`ApplyBold`, `ApplyItalic` and `ApplyUnderline` toggle their respective style.
`ApplyFont` changes only the font override. With no selection these commands
change `TypingStyle`; with a selection they format the selected characters.
A null font family or size restores the platform's default. Font availability,
metrics and visual appearance remain platform-dependent.

`ApplyList` formats selected paragraphs. Use `RichTextListKind.None` to remove
list formatting. Enter continues a list, and Enter on an empty list item
removes its list formatting without adding another paragraph. Removing a
paragraph separator merges content and recomputes numbering.

`InsertText` and native paste insert plain text using the current typing style.
Rich clipboard interchange is intentionally not supported. `IsReadOnly` stops
user editing and formatting commands, but applications can still replace
`Document`. Editing commands on read-only or disabled editors throw
`InvalidOperationException`.

## History and binding

`Document` and selection properties support two-way binding. Replacing the
document from application code clears undo/redo history and clamps the selection.
Do not bind a second independently editable string as the rich-content source
of truth.

`Undo` and `Redo` restore document, selection and typing style together.
`CanUndo` and `CanRedo` notify bindings when history changes. Consecutive native
typing is grouped until a selection change or another editing command.
Native adapters defer document replacement during IME composition; a pending
application replacement takes precedence once composition ends.

`TextChanged` signals document changes, including formatting-only changes.

## Markdown dialect

`LoadMarkdown` imports transactionally; parsing errors leave the document
unchanged. `GetMarkdown` exports explicitly. The supported dialect covers:

- Plain paragraphs.
- Bold and italic.
- Bulleted and numbered lists, nesting and starting numbers.
- Whitelisted inline `<u>` and `<span>` markup for underline, font family and
  font size.

For example:

```markdown
Some **bold** and *italic* text.

- First item
- <u>Underlined item</u>

<span style="font-family: 'serif'; font-size: 24px">Larger text</span>
```

Standard Markdown cannot preserve underline, font families or sizes; exported
inline HTML preserves those supported styles. Sizes in this dialect use `px`,
mapped to the document's device-independent size values, not raw device pixels.
Only the documented font properties are accepted; arbitrary CSS is not allowed.

Headings, links, code, images, tables, arbitrary HTML, external resources and
scripts are outside this first version. Unsupported constructs and invalid
styles produce descriptive `FormatException` errors rather than silent loss.
Multiline list-item paragraphs are not supported. The converter limits nesting
to 64 levels and Markdown ordered-list markers to nine digits. Quoted font
family values support escaping quotes and backslashes.
HTML is parsed into the document; it is never executed or loaded into a WebView.

## Validation

Shared editing and conversion tests are in
[RichEditorTests](../../src/Controls/tests/Core.UnitTests/RichEditorTests.cs) and
[RichEditorMarkdownTests](../../src/Controls/tests/Core.UnitTests/RichEditorMarkdownTests.cs).
Native synchronization tests use the `RichEditor` category in
[Controls device tests](../../src/Controls/tests/DeviceTests/Elements/RichEditor/RichEditorTests.cs).

Run targeted managed tests from the repository root:

```sh
dotnet test src/Controls/tests/Core.UnitTests/Controls.Core.UnitTests.csproj \
  --filter "FullyQualifiedName~RichEditor" \
  -p:IncludeAndroidTargetFrameworks=false \
  -p:IncludeIosTargetFrameworks=false \
  -p:IncludeMacCatalystTargetFrameworks=false \
  -p:IncludeWindowsTargetFrameworks=false
```

For native tests, use the [device-test runner](../../.github/skills/run-device-tests/SKILL.md):

```sh
pwsh .github/skills/run-device-tests/scripts/Run-DeviceTests.ps1 \
  -Project Controls -Platform android -Configuration Release \
  -TestFilter "Category=RichEditor"
```

Run the same category on `ios`, `maccatalyst`, and on a Windows host using
`windows`. Passing shared tests or compiling a handler is not evidence that
native editing passed on another platform. Validate composition, native
shortcuts, clipboard handling and native selection on the target OS before
declaring cross-platform readiness.

### Initial implementation verification

- Managed model, editing and Markdown tests: 61 passed.
- Android native synchronization, composition, formatting and read-only tests:
  passed on an API 30 ARM64 emulator. The Android gallery also builds.
  The earlier category run included unrelated tests, so its total is not an
  exact RichEditor test count.
- Shared/netstandard, iOS and Mac Catalyst framework builds pass with public API
  validation enabled. Apple framework builds use a command-scoped 27.2 target
  override to match the installed workload; repository platform pins are unchanged.
- Windows handler compilation against WinUI references passes, but a full Windows
  build and native runtime tests still require a Windows host.
- Apple device-test apps build and run with session-only SDK and scene-lifecycle
  adjustments. The three initial Controls tests passed on both iOS and Mac
  Catalyst. An isolated iOS Core handler run passed 55 tests with one skipped.
  On Mac Catalyst, 50 Core RichEditor handler tests passed.
- Additional Mac Catalyst composition cases passed, including external document
  replacement during composition. Regression tests exposed native undo
  availability and disabled-editing gaps; fixes are included, but their final
  native rerun remains pending. The expanded Android disabled-input tests also
  await a rerun.
- iOS clipboard tests may require manually approving the system paste prompt.
  The runner's PowerShell regression tests were not run because Pester is not
  installed; actual isolated Apple execution exercised result discovery.

These results establish an initial implementation, not all-platform release
readiness. In particular, shared tests do not establish native IME, clipboard,
accessibility or undo/redo behavior on untested operating systems.
