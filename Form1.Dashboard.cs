using DevExpress.XtraCharts;
using DevExpress.XtraEditors;
using DevExpress.XtraEditors.Controls;
using DevExpress.XtraGrid;
using DevExpress.XtraGrid.Views.Grid;
using ERSoftSMSBox.Data;

namespace ERSoftSMSBox;

public partial class Form1
{
    private readonly PanelControl dashboardPanel = new();
    private readonly LabelControl kpiTotalSmsValue = new();
    private readonly LabelControl kpiMonthSmsValue = new();
    private readonly LabelControl kpiOfficesValue = new();
    private readonly LabelControl kpiCustomersValue = new();
    private readonly LabelControl dashboardStatusLabel = new();
    private readonly SimpleButton dashboardRefreshButton = new();
    private ChartControl? dashboardChart;
    private GridControl? topOfficesGrid;
    private GridView? topOfficesView;
    private bool dashboardBusy;
    private bool dashboardLoaded;

    private PanelControl CreateDashboardPanel()
    {
        dashboardPanel.Name = "dashboardPanel";
        dashboardPanel.Dock = DockStyle.Fill;
        dashboardPanel.BorderStyle = BorderStyles.NoBorder;
        dashboardPanel.AutoScroll = true;
        dashboardPanel.Visible = false;

        // ── Outer root ──────────────────────────────────────────
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Color.Transparent
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // toolbar row
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // KPI tiles
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // status label
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 320)); // chart + offices

        // ── Toolbar row ──────────────────────────────────────────
        FlowLayoutPanel toolbar = new()
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.RightToLeft,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0, 0, 0, 8),
            BackColor = Color.Transparent
        };
        dashboardRefreshButton.Name = "dashboardRefresh";
        dashboardRefreshButton.Text = GetText("DashboardRefresh");
        dashboardRefreshButton.AutoSize = true;
        dashboardRefreshButton.ImageOptions.SvgImage = menuImages["ConnectionSettings"];
        dashboardRefreshButton.ImageOptions.SvgImageSize = new Size(16, 16);
        dashboardRefreshButton.Click += (_, _) => { dashboardLoaded = false; _ = LoadDashboardAsync(); };
        toolbar.Controls.Add(dashboardRefreshButton);
        root.Controls.Add(toolbar, 0, 0);

        // ── KPI tiles row ─────────────────────────────────────────
        TableLayoutPanel kpiRow = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 12),
            BackColor = Color.Transparent
        };
        for (int i = 0; i < 4; i++)
            kpiRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        kpiRow.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        kpiRow.Controls.Add(CreateKpiTile(kpiTotalSmsValue, GetText("DashboardKpiTotalSms"), Color.FromArgb(41, 128, 185)), 0, 0);
        kpiRow.Controls.Add(CreateKpiTile(kpiMonthSmsValue, GetText("DashboardKpiMonthSms"), Color.FromArgb(39, 174, 96)), 1, 0);
        kpiRow.Controls.Add(CreateKpiTile(kpiOfficesValue, GetText("DashboardKpiOffices"), Color.FromArgb(211, 84, 0)), 2, 0);
        kpiRow.Controls.Add(CreateKpiTile(kpiCustomersValue, GetText("DashboardKpiCustomers"), Color.FromArgb(142, 68, 173)), 3, 0);
        root.Controls.Add(kpiRow, 0, 1);

        // ── Status label ──────────────────────────────────────────
        dashboardStatusLabel.Name = "dashboardStatus";
        dashboardStatusLabel.Text = GetText("DashboardNoData");
        dashboardStatusLabel.AutoSizeMode = LabelAutoSizeMode.Vertical;
        dashboardStatusLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        dashboardStatusLabel.Margin = new Padding(0, 4, 0, 8);
        root.Controls.Add(dashboardStatusLabel, 0, 2);

        // ── Chart + Top offices row ───────────────────────────────
        TableLayoutPanel dataRow = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
        };
        dataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65));
        dataRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        dataRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Bar chart
        GroupControl chartGroup = new()
        {
            Text = GetText("DashboardChartTitle"),
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0)
        };
        dashboardChart = new ChartControl
        {
            Name = "dashboardChart",
            Dock = DockStyle.Fill
        };
        dashboardChart.Legend.Visibility = DevExpress.Utils.DefaultBoolean.False;
        chartGroup.Controls.Add(dashboardChart);
        dataRow.Controls.Add(chartGroup, 0, 0);

        // Top offices grid
        GroupControl officesGroup = new()
        {
            Text = GetText("DashboardTopOffices"),
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };
        topOfficesGrid = new GridControl { Name = "topOfficesGrid", Dock = DockStyle.Fill };
        topOfficesView = new GridView(topOfficesGrid)
        {
            Name = "topOfficesView",
            OptionsBehavior = { Editable = false },
            OptionsView = { ShowGroupPanel = false }
        };
        topOfficesGrid.MainView = topOfficesView;
        officesGroup.Controls.Add(topOfficesGrid);
        dataRow.Controls.Add(officesGroup, 1, 0);

        root.Controls.Add(dataRow, 0, 3);
        dashboardPanel.Controls.Add(root);
        return dashboardPanel;
    }

    private static GroupControl CreateKpiTile(LabelControl valueLabel, string description, Color accentColor)
    {
        GroupControl tile = new()
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(4),
            Padding = new Padding(12, 8, 12, 12)
        };

        TableLayoutPanel inner = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Color.Transparent
        };
        inner.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        inner.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        valueLabel.Text = "—";
        valueLabel.Anchor = AnchorStyles.Left;
        valueLabel.Appearance.FontSizeDelta = 18;
        valueLabel.Appearance.FontStyleDelta = FontStyle.Bold;
        valueLabel.Appearance.ForeColor = accentColor;
        valueLabel.AutoSizeMode = LabelAutoSizeMode.None;
        valueLabel.Height = 48;

        LabelControl descLabel = new()
        {
            Text = description,
            Anchor = AnchorStyles.Left | AnchorStyles.Right,
            AutoSizeMode = LabelAutoSizeMode.Vertical
        };
        descLabel.Appearance.ForeColor = Color.Gray;

        inner.Controls.Add(valueLabel, 0, 0);
        inner.Controls.Add(descLabel, 0, 1);
        tile.Controls.Add(inner);
        return tile;
    }

    private void ShowDashboard()
    {
        dashboardPanel.Visible = true;
        dashboardPanel.BringToFront();
        if (connectedDatabase is not null && !dashboardLoaded && !dashboardBusy)
            _ = LoadDashboardAsync();
    }

    private void HideDashboard()
    {
        dashboardPanel.Visible = false;
    }

    private async Task LoadDashboardAsync()
    {
        if (dashboardBusy || connectedDatabase is null) return;
        dashboardBusy = true;
        dashboardRefreshButton.Enabled = false;
        dashboardStatusLabel.Text = GetText("DashboardLoading");
        try
        {
            DatabaseSettings settings = connectedDatabase;
            DateTime today = DateTime.Today;
            DateTime monthStart = new(today.Year, today.Month, 1);
            DateTime thirtyDaysAgo = today.AddDays(-29);

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));

            IReadOnlyList<SentMessageSummary> monthlySummary =
                await repository.ReadSentSummaryAsync(settings,
                    new SentMessageFilter("", "", thirtyDaysAgo, today), cts.Token);

            if (IsDisposed) return;

            // KPI calculations
            int totalSms = officeSummaries.Sum(o => o.SentCount);
            int monthSms = monthlySummary.Sum(r => r.Count);
            int activeOffices = officeSummaries.Count;
            int totalCustomers = officeSummaries.Sum(o => o.CustomerCount);

            kpiTotalSmsValue.Text = totalSms.ToString("n0");
            kpiMonthSmsValue.Text = monthSms.ToString("n0");
            kpiOfficesValue.Text = activeOffices.ToString("n0");
            kpiCustomersValue.Text = totalCustomers.ToString("n0");

            // Bar chart
            BuildBarChart(monthlySummary, thirtyDaysAgo, today);

            // Top offices
            BuildTopOfficesGrid();

            dashboardStatusLabel.Text = string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                GetText("DashboardStatus"),
                DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture));

            dashboardLoaded = true;
        }
        catch (OperationCanceledException) { dashboardStatusLabel.Text = GetText("DatabaseCancelled"); }
        catch (Microsoft.Data.SqlClient.SqlException) { dashboardStatusLabel.Text = GetText("DatabaseSchemaError"); }
        catch (Exception ex) { dashboardStatusLabel.Text = ex.Message; }
        finally
        {
            dashboardBusy = false;
            if (!IsDisposed) dashboardRefreshButton.Enabled = true;
        }
    }

    private void BuildBarChart(IReadOnlyList<SentMessageSummary> data, DateTime from, DateTime to)
    {
        if (dashboardChart is null) return;
        dashboardChart.Series.Clear();

        // Aggregate by day across all offices
        Dictionary<DateTime, int> byDay = [];
        for (DateTime d = from.Date; d <= to.Date; d = d.AddDays(1))
            byDay[d] = 0;
        foreach (SentMessageSummary row in data)
        {
            if (row.SentDate is not null && byDay.ContainsKey(row.SentDate.Value.Date))
                byDay[row.SentDate.Value.Date] += row.Count;
        }

        Series series = new(GetText("DashboardKpiMonthSms"), ViewType.Bar);
        series.ArgumentScaleType = ScaleType.DateTime;
        series.ValueScaleType = ScaleType.Numerical;
        foreach (KeyValuePair<DateTime, int> kv in byDay.OrderBy(k => k.Key))
            series.Points.Add(new SeriesPoint(kv.Key, kv.Value));

        BarSeriesView view = (BarSeriesView)series.View;
        view.Color = Color.FromArgb(41, 128, 185);
        view.FillStyle.FillMode = FillMode.Solid;

        dashboardChart.Series.Add(series);

        if (dashboardChart.Diagram is XYDiagram diagram)
        {
            diagram.AxisX.DateTimeScaleOptions.GridAlignment = DateTimeGridAlignment.Day;
            diagram.AxisX.DateTimeScaleOptions.MeasureUnit = DateTimeMeasureUnit.Day;
            diagram.AxisX.Label.Angle = -45;
            diagram.AxisX.Label.TextPattern = "{A:dd.MM}";
            diagram.AxisY.Label.TextPattern = "{V:n0}";
            diagram.AxisY.WholeRange.AlwaysShowZeroLevel = true;
        }

        dashboardChart.Legend.Visibility = DevExpress.Utils.DefaultBoolean.False;

        if (data.Count == 0 || byDay.Values.All(v => v == 0))
            dashboardStatusLabel.Text = GetText("DashboardNoSms");
    }

    private void BuildTopOfficesGrid()
    {
        if (topOfficesGrid is null || topOfficesView is null) return;

        List<TopOfficeRow> top = officeSummaries
            .OrderByDescending(o => o.SentCount)
            .Take(8)
            .Select(o => new TopOfficeRow(o.OfficeDescription, o.SentCount, o.CustomerCount, o.DebtorCount))
            .ToList();

        // DataSource null yapıp sütunları temizle, sonra sütunları ekle, sonra DataSource ata.
        // Aksi hâlde DevExpress binding zinciri kopup sütunlar boş görünebilir.
        topOfficesGrid.DataSource = null;
        topOfficesView.Columns.Clear();

        AddTopOfficesColumn("OfficeName", GetText("DashboardColOffice"), 160);
        AddTopOfficesColumn("SentCount", GetText("DashboardKpiTotalSms"), 70);
        AddTopOfficesColumn("CustomerCount", GetText("DashboardKpiCustomers"), 70);
        AddTopOfficesColumn("DebtorCount", GetText("DatabaseDebtors"), 70);

        topOfficesGrid.DataSource = top;
        topOfficesView.BestFitColumns();
        topOfficesView.OptionsView.ColumnAutoWidth = true;
    }

    private void AddTopOfficesColumn(string fieldName, string caption, int width)
    {
        if (topOfficesView is null) return;
        var col = topOfficesView.Columns.AddField(fieldName);
        col.Caption = caption;
        col.Width = width;
        col.Visible = true;
        col.OptionsColumn.AllowEdit = false;
    }

    private void InvalidateDashboard() => dashboardLoaded = false;

    private sealed record TopOfficeRow(string OfficeName, int SentCount, int CustomerCount, int DebtorCount);
}
