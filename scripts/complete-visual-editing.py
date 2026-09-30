"""Guarded integration of visual editing checks. Removed after application."""
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
replace('scripts/visual-editing-check.mjs', '  await nativeReady(true);', '''  // Native textarea flags are not a contract of Uno's Skia TextBox. Observe
  // focus separately and verify that actual body input is rejected in object mode.
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
replace('.github/workflows/build.yml', '          cat artifacts/tests/tab-validation.json\n', '''          cat artifacts/tests/tab-validation.json
      - name: Record visual typography and query samples
        shell: bash
        run: |
          set -euo pipefail
          dotnet build benchmarks/TextSpace.VisualPerformance -c Release
          DOTNET_TieredCompilation=0 DOTNET_ReadyToRun=0 dotnet run --project benchmarks/TextSpace.VisualPerformance -c Release --no-build > artifacts/tests/visual-performance.json
          cat artifacts/tests/visual-performance.json
''')
replace('Directory.Build.props', '<Version>0.5.1-alpha.1</Version>', '<Version>0.6.0-alpha.1</Version>')
replace('.github/workflows/release.yml', 'default: 0.5.1-alpha.1', 'default: 0.6.0-alpha.1')
for path, text in changes.items(): Path(path).write_text(text)
Path('scripts/complete-visual-editing.py').unlink()
Path('.github/workflows/complete-visual-editing.yml').unlink()
print('Integrated visual validation, standard-first placement, input ownership and benchmark. Temporary files removed.')
