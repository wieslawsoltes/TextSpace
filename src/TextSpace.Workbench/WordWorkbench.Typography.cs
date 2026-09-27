namespace TextSpace.Workbench;

public sealed partial class WordWorkbench
{
    private void InitializeTypography()
    {
        Ribbon.InsertGroup("Layout", 2, () => Group("Text Layout",
            Tool("ruler", "Tabs", "tab-stops", true), Tool("paragraph", "Pagination", "pagination", true)));
        Ribbon.InsertGroup("Insert", 4, () =>
        {
            var menu = new OfficeMenu()
                .Add("Tab character", () => RunEdit("Insert tab", () => Session.InsertText("\t")))
                .Add("Nonbreaking space", () => RunEdit("Nonbreaking space", () => Session.InsertText("\u00a0")))
                .Add("Nonbreaking hyphen", () => RunEdit("Nonbreaking hyphen", () => Session.InsertText("\u2011")))
                .Add("Optional hyphen", () => RunEdit("Optional hyphen", () => Session.InsertText("\u00ad")))
                .Add("Zero-width break", () => RunEdit("Zero-width break", () => Session.InsertText("\u200b")));
            return Group("Special Characters", MenuButton("symbol", "Special Characters", menu));
        });
    }

    private async Task TabStopsAsync()
    {
        var pending = Session.CurrentParagraph.Format.TabStops.ToList();
        var dialog = new OfficeDialog("Tabs", "Apply", 540); dialog.Body.Spacing = 8;
        var interval = dialog.AddField("Default tab interval (points)", N(Session.Document.DefaultTabStop));
        var position = dialog.AddField("Tab stop position (points)", "72");
        var alignment = Choice(dialog, "Tab alignment", Enum.GetNames<TabAlignment>(), "Left");
        var leader = Choice(dialog, "Tab leader", Enum.GetNames<TabLeader>(), "None");
        var decimalMark = Choice(dialog, "Decimal character", [".", ","], ".");
        var relative = new OfficeCheckBox("Position from right edge", false);
        dialog.Body.Children.Add(relative);
        var error = Wrapped("", 11, "#A4262C");
        var items = new StackPanel { Spacing = 2 };
        void Refresh()
        {
            items.Children.Clear();
            foreach (var stop in pending.OrderBy(s => s.Resolve(Session.CurrentSection.Page.ColumnWidth)))
            {
                var selected = stop;
                var label = $"{N(stop.Position)} pt{(stop.RelativeToRightEdge ? " from right" : "")} · {stop.Alignment} · {stop.Leader}";
                items.Children.Add(new OfficeButton(label, () =>
                {
                    position.Text = N(selected.Position); alignment.Value = selected.Alignment.ToString();
                    leader.Value = selected.Leader.ToString(); decimalMark.Value = selected.DecimalCharacter.ToString();
                    relative.IsChecked = selected.RelativeToRightEdge;
                }) { HorizontalContentAlignment = HorizontalAlignment.Left, Height = 27 });
            }
            if (pending.Count == 0) items.Children.Add(Wrapped("No custom stops. The default interval is used.", 11, OfficeTheme.Muted));
        }
        TabStop ReadStop()
        {
            var points = ParseNumber(position.Text);
            if (points is < -4000 or > 4000) throw new InvalidOperationException("Tab positions must be within 4,000 points of the chosen edge.");
            if (!Enum.TryParse<TabAlignment>(alignment.Value, true, out var align) || !Enum.IsDefined(align)
                || !Enum.TryParse<TabLeader>(leader.Value, true, out var trail) || !Enum.IsDefined(trail)
                || decimalMark.Value is not ("." or ",")) throw new InvalidOperationException("Choose a valid alignment, leader, and decimal character.");
            return new() { Position = Math.Round(points * 20) / 20, Alignment = align, Leader = trail,
                RelativeToRightEdge = relative.IsChecked, DecimalCharacter = decimalMark.Value[0] };
        }
        void Edit(Action action)
        {
            try { action(); error.Text = ""; Refresh(); }
            catch (Exception ex) when (ex is InvalidOperationException or FormatException) { error.Text = ex.Message; }
        }
        var set = new OfficeButton("Set tab stop", () => Edit(() =>
        {
            var stop = ReadStop();
            var existing = pending.FindIndex(s => Math.Round(s.Position * 20) == Math.Round(stop.Position * 20) && s.RelativeToRightEdge == stop.RelativeToRightEdge);
            if (existing >= 0) pending[existing] = stop;
            else { if (pending.Count >= 128) throw new InvalidOperationException("A paragraph supports at most 128 stops."); pending.Add(stop); }
        })) { BorderThickness = new(1) };
        var clear = new OfficeButton("Clear tab stop", () => Edit(() =>
        {
            var stop = ReadStop(); pending.RemoveAll(s => Math.Round(s.Position * 20) == Math.Round(stop.Position * 20) && s.RelativeToRightEdge == stop.RelativeToRightEdge);
        })) { BorderThickness = new(1) };
        var clearAll = new OfficeButton("Clear all tab stops", () => Edit(pending.Clear)) { BorderThickness = new(1) };
        dialog.Body.Children.Add(OfficeTheme.Row(set, clear, clearAll));
        dialog.Body.Children.Add(error);
        dialog.Body.Children.Add(new ScrollViewer { Content = items, MaxHeight = 108, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        dialog.AddDescription("Set or clear stops, then Apply to the selected paragraphs. 72 points = 1 inch. The default interval applies throughout the document. Right-edge stops follow column width.");
        Refresh();
        if (await ShowDialogAsync(dialog)) Session.SetTabStops(pending, ParseNumber(interval.Text));
    }

    private async Task PaginationAsync()
    {
        var format = Session.CurrentParagraph.Format;
        var dialog = new OfficeDialog("Line and Page Breaks", "Apply", 470);
        var widow = new OfficeCheckBox("Widow/orphan control", format.WidowControl);
        var next = new OfficeCheckBox("Keep with next", format.KeepWithNext);
        var lines = new OfficeCheckBox("Keep lines together", format.KeepLinesTogether);
        var page = new OfficeCheckBox("Page break before", format.PageBreakBefore);
        dialog.Body.Children.Add(OfficeTheme.Column(widow, next, lines, page));
        dialog.AddDescription("Applies to the selected paragraphs. Widow/orphan control keeps at least two lines at each break where possible. Content taller than the available column still flows to avoid empty-page loops.");
        if (await ShowDialogAsync(dialog)) Session.FormatParagraph("Pagination", p => p with
        {
            WidowControl = widow.IsChecked, KeepWithNext = next.IsChecked,
            KeepLinesTogether = lines.IsChecked, PageBreakBefore = page.IsChecked
        });
    }
}
