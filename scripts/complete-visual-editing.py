"""Apply guarded source-only integration; workflow changes use the repository API."""
from pathlib import Path
changes = {}
def read(path): return changes.get(path, Path(path).read_text())
def replace(path, old, new, count=1):
    text = read(path)
    if text.count(old) != count: raise RuntimeError((path, text.count(old), old[:120]))
    changes[path] = text.replace(old, new)
replace('src/TextSpace.Core/EquationNode.cs', 'node.Text.Any(c => char.IsControl(c)) || !WellFormed(node.Text)', '!VisualTextRules.IsValid(node.Text, MaximumCharacters, false)')
replace('src/TextSpace.OpenXml/DocxWriter.Visuals.cs', 'new XAttribute("standardX", Invariant(', 'new XAttribute("standardWidth", Invariant(block.Width)), new XAttribute("standardHeight", Invariant(block.Height)),\n                new XAttribute("standardFloating", block.Placement.Floating ? "1" : "0"),\n                new XAttribute("standardX", Invariant(')
replace('src/TextSpace.OpenXml/DocxReader.Visuals.cs', '        var native = properties?.Element(A + "extLst")', '''        var supportedCoordinateFrame = !floating ||
            (string?)frame.Element(Wp + "positionH")?.Attribute("relativeFrom") == "column"
            && (string?)frame.Element(Wp + "positionV")?.Attribute("relativeFrom") == "paragraph"
            && frame.Element(Wp + "positionH")?.Element(Wp + "align") is null
            && frame.Element(Wp + "positionV")?.Element(Wp + "align") is null;
        if (!supportedCoordinateFrame) Warn("Unsupported drawing coordinate frame was normalized to local column/paragraph offsets; retain the original for exact placement.");
        var native = properties?.Element(A + "extLst")''')
replace('src/TextSpace.OpenXml/DocxReader.Visuals.cs', '            if ((floating || result.Alignment == alignment) && double.IsFinite(nativeX + nativeY + standardX + standardY)', '''            var standardWidth = Number((string?)native.Attribute("standardWidth"), double.NaN);
            var standardHeight = Number((string?)native.Attribute("standardHeight"), double.NaN);
            var sameGeometry = supportedCoordinateFrame && (string?)native.Attribute("standardFloating") == (floating ? "1" : "0")
                && Math.Abs(result.Width - standardWidth) < 0.001 && Math.Abs(result.Height - standardHeight) < 0.001;
            if (sameGeometry && (floating || result.Alignment == alignment) && double.IsFinite(nativeX + nativeY + standardX + standardY)''')
replace('src/TextSpace.Editor/DocumentSurface.Visuals.cs', '    public string? SelectedObjectId => _selectedObjectId;', '    public string? SelectedObjectId => _selectedObjectId;\n    public bool IsBodyInputReadOnly => _input.IsReadOnly;')
replace('src/TextSpace.App/Platforms/WebAssembly/BrowserDiagnostics.Visuals.cs', '        json.WriteStartObject("visuals"); json.WriteString("selected", state.SelectedObjectId);', '        json.WriteStartObject("visuals"); json.WriteString("selected", state.SelectedObjectId);\n        json.WriteBoolean("bodyReadOnly", workbench.Surface.IsBodyInputReadOnly);')
replace('scripts/visual-editing-check.mjs', 'document.activeElement.readOnly === readOnly, readOnly)', '(readOnly === null || document.activeElement.readOnly === readOnly), readOnly)')
replace('scripts/visual-editing-check.mjs', '  await nativeReady(true);', '''  // Observe native focus separately from managed TextBox state, then exercise
  // actual input to prove that object selection cannot mutate body text.
  await nativeReady(null);
  await until(async () => (await state()).visuals.bodyReadOnly, 'Managed body input must be read-only for object selection');
  const before = await state();
  await page.keyboard.insertText('BODY INPUT MUST BE REJECTED');
  await page.waitForTimeout(200);
  assert.equal((await state()).text, before.text, 'Object-mode typing changed body text');
  assert.equal((await state()).revision, before.revision, 'Rejected typing changed document history');
  await until(() => page.evaluate(text => document.activeElement?.value === text, before.text), 'Rejected input was not restored in the native textarea');''')
replace('scripts/visual-editing-check.mjs', "  await check('picture crop is nondestructive and resettable', async () => {", '''  await check('matrix row and column edits have independent local history', async () => {
    const revision = (await state()).revision;
    await click('edit-object'); await click('Equation Matrix');
    await until(async () => (await state()).visuals.slots.length === 5, 'Matrix template was not inserted into the active fraction slot');
    await click('Matrix layout'); await click('Insert Matrix Column Right');
    await until(async () => (await state()).visuals.slots.length === 7, 'Matrix column insertion failed');
    await click('Equation Undo'); await until(async () => (await state()).visuals.slots.length === 5, 'Local matrix undo failed');
    await click('Equation Redo'); await until(async () => (await state()).visuals.slots.length === 7, 'Local matrix redo failed');
    await click('Matrix layout'); await click('Insert Matrix Row Below');
    await until(async () => (await state()).visuals.slots.length === 10, 'Matrix row insertion failed');
    await page.screenshot({ path: output + '/matrix-editor.png' });
    await click('Equation structure operations'); await click('Convert Structure to Linear Text');
    await until(async () => (await state()).visuals.slots.length === 2, 'Linear conversion lost surrounding fraction slots');
    await click('Equation Undo'); await until(async () => (await state()).visuals.slots.length === 10, 'Linear conversion undo failed');
    assert.equal((await state()).revision, revision, 'Local equation edits changed document history before Apply');
    await click('Cancel equation'); await until(async () => !(await state()).visuals.editor, 'Matrix draft did not cancel');
    assert.equal((await object(equationId)).text, '(a+b)/(c)');
  });
  await check('picture crop is nondestructive and resettable', async () => {''')
replace('Directory.Build.props', '<Version>0.5.1-alpha.1</Version>', '<Version>0.6.0-alpha.1</Version>')
replace('README.md', '**Development preview: 0.5.1-alpha.1.**', '**Development preview: 0.6.0-alpha.1.**')
replace('README.md', '## Download\n', '''### Direct object and equation editing

**Insert → Shapes / Equation** adds editable retained content. Select an object on the paper to move, resize or rotate it; use **Picture Format → Crop** for nondestructive picture cropping. Double-click a shape or equation to edit its detached draft. Done applies one document transaction; Escape discards the draft.

The equation editor exposes measured fraction, radical, script, matrix, delimiter, operator and accent slots with local undo/redo. Matrix Layout changes rows and columns without flattening the expression. DOCX exports editable DrawingML and Office Math; HTML exports SVG and presentation MathML. Table boundaries can be resized on the page, and Alt-drag selects a cell rectangle.

See [WYSIWYG objects, tables and equations](docs/WYSIWYG-OBJECTS.md) for reusable APIs, keyboard interaction, performance ownership and explicit compatibility boundaries. These features remain subject to exact-commit browser and native build validation; source version alone does not establish a public deployment.

## Download
''')
replace('src/TextSpace.Workbench/WordWorkbench.Dialogs.cs', 'collaboration, macros, equations, footnotes, floating shapes, or lossless DOCX round-tripping.', 'collaboration, macros, footnotes, tight text wrapping, grouped drawings, or lossless DOCX round-tripping.')
replace('src/TextSpace.Workbench/WordWorkbench.Dialogs.cs', 'Version 0.5.0-alpha.1.', 'Version 0.6.0-alpha.1.')
replace('src/TextSpace.Workbench/WordWorkbench.Dialogs.cs', '        await ShowDialogAsync(dialog);\n    }\n    private async Task AboutAsync()', '        dialog.AddDescription("Insert Shapes or Equation for direct object editing. Drag resize/rotation handles; double-click for text or structural math. Matrix Layout inserts and removes rows or columns. Done applies a draft; Escape cancels. Picture Format offers nondestructive Crop and Reset Crop.");\n        await ShowDialogAsync(dialog);\n    }\n    private async Task AboutAsync()')
# Preserve every workflow file: GITHUB_TOKEN cannot rewrite workflow definitions.
# The connected repository API removes this temporary workflow after validation.
for path, text in changes.items():
    assert not path.startswith('.github/'), path
    Path(path).write_text(text)
Path('scripts/complete-visual-editing.py').unlink()
print('Integrated source-only visual validation, placement, input tests and documentation. Workflow definitions unchanged.')
