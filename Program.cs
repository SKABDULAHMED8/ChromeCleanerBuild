using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ChromeCacheCleaner;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class MainForm : Form
{
    private readonly string chromeRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Google", "Chrome", "User Data");

    private readonly Label chromeStatus = new();
    private readonly Label cacheSizeLabel = new();
    private readonly Label storageSizeLabel = new();
    private readonly Label activityLabel = new();
    private readonly Label resultLabel = new();
    private readonly ProgressBar progress = new();
    private readonly CheckBox cacheBox = new();
    private readonly CheckBox storageBox = new();
    private readonly Button scanButton = new();
    private readonly Button cleanButton = new();
    private readonly Button closeChromeButton = new();
    private readonly Button closeButton = new();
    private readonly System.Windows.Forms.Timer detector = new();

    private bool chromeRunning;
    private bool busy;

    private static readonly string[] CachePaths =
    [
        "Cache",
        "Code Cache",
        "GPUCache",
        "DawnCache",
        @"Service Worker\CacheStorage",
        @"Service Worker\ScriptCache"
    ];

    private static readonly string[] LocalStoragePaths =
    [
        "Local Storage"
    ];

    public MainForm()
    {
        Text = "Chrome Cache Cleaner";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(760, 560);
        MaximumSize = new Size(760, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(13, 17, 23);
        ForeColor = Color.FromArgb(230, 237, 243);
        Font = new Font("Segoe UI", 10F);
        DoubleBuffered = true;

        BuildUi();

        detector.Interval = 1000;
        detector.Tick += (_, _) => UpdateChromeState(false);
        detector.Start();

        Shown += async (_, _) =>
        {
            UpdateChromeState(true);
            await ScanAsync();
        };
    }

    private void BuildUi()
    {
        var header = new Panel
        {
            Location = new Point(18, 18),
            Size = new Size(724, 90),
            BackColor = Color.FromArgb(20, 26, 34)
        };
        header.Paint += PaintPanelBorder;
        Controls.Add(header);

        var title = new Label
        {
            Text = "CHROME CACHE CLEANER",
            Font = new Font("Segoe UI Semibold", 19F),
            ForeColor = Color.FromArgb(240, 246, 252),
            AutoSize = true,
            Location = new Point(22, 15)
        };
        header.Controls.Add(title);

        var subtitle = new Label
        {
            Text = "LOCAL • OFFLINE • SAFE CLEANUP",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(139, 148, 158),
            AutoSize = true,
            Location = new Point(25, 51)
        };
        header.Controls.Add(subtitle);

        chromeStatus.AutoSize = false;
        chromeStatus.Size = new Size(270, 36);
        chromeStatus.Location = new Point(430, 27);
        chromeStatus.TextAlign = ContentAlignment.MiddleCenter;
        chromeStatus.Font = new Font("Segoe UI Semibold", 10F);
        header.Controls.Add(chromeStatus);

        var info = new Panel
        {
            Location = new Point(18, 125),
            Size = new Size(724, 125),
            BackColor = Color.FromArgb(18, 23, 30)
        };
        info.Paint += PaintPanelBorder;
        Controls.Add(info);

        AddMetric(info, "CACHE", "Scanning...", cacheSizeLabel, 22, 20);
        AddMetric(info, "LOCAL STORAGE", "Scanning...", storageSizeLabel, 380, 20);

        var warning = new Label
        {
            Text = "Cache cleanup does not target bookmarks, history, passwords, cookies or extensions.",
            AutoSize = true,
            ForeColor = Color.FromArgb(139, 148, 158),
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(23, 88)
        };
        info.Controls.Add(warning);

        var selectPanel = new Panel
        {
            Location = new Point(18, 267),
            Size = new Size(724, 105),
            BackColor = Color.FromArgb(18, 23, 30)
        };
        selectPanel.Paint += PaintPanelBorder;
        Controls.Add(selectPanel);

        cacheBox.Text = "Delete Chrome cache";
        cacheBox.Checked = true;
        cacheBox.AutoSize = true;
        cacheBox.Location = new Point(25, 18);
        StyleCheckBox(cacheBox);
        selectPanel.Controls.Add(cacheBox);

        storageBox.Text = "Delete Local Storage";
        storageBox.Checked = false;
        storageBox.AutoSize = true;
        storageBox.Location = new Point(25, 54);
        StyleCheckBox(storageBox);
        selectPanel.Controls.Add(storageBox);

        var storageHint = new Label
        {
            Text = "OFF by default • may remove website offline data or sessions",
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 160, 80),
            Font = new Font("Segoe UI", 8F),
            Location = new Point(225, 57)
        };
        selectPanel.Controls.Add(storageHint);

        var activityPanel = new Panel
        {
            Location = new Point(18, 389),
            Size = new Size(724, 75),
            BackColor = Color.FromArgb(18, 23, 30)
        };
        activityPanel.Paint += PaintPanelBorder;
        Controls.Add(activityPanel);

        activityLabel.Text = "READY";
        activityLabel.Font = new Font("Segoe UI Semibold", 10F);
        activityLabel.ForeColor = Color.FromArgb(87, 227, 137);
        activityLabel.AutoSize = true;
        activityLabel.Location = new Point(20, 13);
        activityPanel.Controls.Add(activityLabel);

        resultLabel.Text = "No cleanup has been performed.";
        resultLabel.ForeColor = Color.FromArgb(139, 148, 158);
        resultLabel.AutoSize = true;
        resultLabel.Location = new Point(20, 40);
        activityPanel.Controls.Add(resultLabel);

        progress.Location = new Point(300, 18);
        progress.Size = new Size(395, 18);
        progress.Style = ProgressBarStyle.Continuous;
        progress.Minimum = 0;
        progress.Maximum = 100;
        progress.Value = 0;
        activityPanel.Controls.Add(progress);

        scanButton.Text = "SCAN";
        StyleButton(scanButton, 105, 42);
        scanButton.Location = new Point(18, 490);
        scanButton.Click += async (_, _) => await ScanAsync();
        Controls.Add(scanButton);

        cleanButton.Text = "CLEAN SELECTED";
        StyleButton(cleanButton, 155, 42, true);
        cleanButton.Location = new Point(135, 490);
        cleanButton.Click += async (_, _) => await CleanAsync();
        Controls.Add(cleanButton);

        closeChromeButton.Text = "CLOSE CHROME";
        StyleButton(closeChromeButton, 140, 42);
        closeChromeButton.Location = new Point(302, 490);
        closeChromeButton.Click += (_, _) => CloseChrome();
        Controls.Add(closeChromeButton);

        closeButton.Text = "CLOSE";
        StyleButton(closeButton, 100, 42);
        closeButton.Location = new Point(640, 490);
        closeButton.Click += (_, _) => Close();
        Controls.Add(closeButton);
    }

    private void AddMetric(Control parent, string heading, string value, Label valueLabel, int x, int y)
    {
        var h = new Label
        {
            Text = heading,
            ForeColor = Color.FromArgb(100, 181, 246),
            Font = new Font("Segoe UI Semibold", 8.5F),
            AutoSize = true,
            Location = new Point(x, y)
        };
        parent.Controls.Add(h);

        valueLabel.Text = value;
        valueLabel.ForeColor = Color.FromArgb(240, 246, 252);
        valueLabel.Font = new Font("Segoe UI Semibold", 17F);
        valueLabel.AutoSize = true;
        valueLabel.Location = new Point(x, y + 27);
        parent.Controls.Add(valueLabel);
    }

    private void StyleCheckBox(CheckBox box)
    {
        box.ForeColor = Color.FromArgb(230, 237, 243);
        box.Font = new Font("Segoe UI Semibold", 9.5F);
        box.FlatStyle = FlatStyle.Flat;
    }

    private void StyleButton(Button b, int width, int height, bool primary = false)
    {
        b.Size = new Size(width, height);
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.BorderColor = primary
            ? Color.FromArgb(72, 187, 120)
            : Color.FromArgb(61, 75, 89);
        b.BackColor = primary
            ? Color.FromArgb(25, 70, 47)
            : Color.FromArgb(25, 32, 41);
        b.ForeColor = primary
            ? Color.FromArgb(115, 239, 157)
            : Color.FromArgb(210, 220, 230);
        b.Font = new Font("Segoe UI Semibold", 9F);
        b.Cursor = Cursors.Hand;
    }

    private void PaintPanelBorder(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel p) return;
        using var pen = new Pen(Color.FromArgb(43, 55, 68));
        e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
    }

    private void UpdateChromeState(bool force)
    {
        if (busy && !force) return;

        var running = IsChromeRunning();
        if (running == chromeRunning && !force) return;

        chromeRunning = running;

        if (running)
        {
            chromeStatus.Text = "●  CHROME RUNNING";
            chromeStatus.BackColor = Color.FromArgb(67, 34, 34);
            chromeStatus.ForeColor = Color.FromArgb(255, 130, 130);

            activityLabel.Text = "WAITING";
            activityLabel.ForeColor = Color.FromArgb(255, 184, 92);
            cleanButton.Enabled = false;
            closeChromeButton.Enabled = true;
        }
        else
        {
            chromeStatus.Text = "●  SAFE TO CLEAN";
            chromeStatus.BackColor = Color.FromArgb(24, 66, 45);
            chromeStatus.ForeColor = Color.FromArgb(100, 238, 151);

            activityLabel.Text = "READY • SAFE TO CLEAN SELECTED";
            activityLabel.ForeColor = Color.FromArgb(87, 227, 137);
            cleanButton.Enabled = !busy;
            closeChromeButton.Enabled = false;
        }
    }

    private async Task ScanAsync()
    {
        if (busy) return;

        busy = true;
        UpdateChromeState(true);
        scanButton.Enabled = false;
        cleanButton.Enabled = false;
        progress.Value = 0;
        activityLabel.Text = "SCANNING...";
        activityLabel.ForeColor = Color.FromArgb(100, 181, 246);

        await Task.Run(() =>
        {
            var cache = GetSizeForAllProfiles(CachePaths);
            var storage = GetSizeForAllProfiles(LocalStoragePaths);

            BeginInvoke(() =>
            {
                cacheSizeLabel.Text = FormatSize(cache);
                storageSizeLabel.Text = FormatSize(storage);
                resultLabel.Text = $"Detected • {FormatSize(cache + storage)} across Chrome profiles";
                progress.Value = 100;
            });
        });

        busy = false;
        UpdateChromeState(true);
        scanButton.Enabled = true;
        if (!chromeRunning)
            cleanButton.Enabled = true;

        activityLabel.Text = chromeRunning
            ? "CHROME OPEN • CLOSE CHROME TO CLEAN"
            : "READY • SAFE TO CLEAN SELECTED";
        activityLabel.ForeColor = chromeRunning
            ? Color.FromArgb(255, 184, 92)
            : Color.FromArgb(87, 227, 137);
    }

    private async Task CleanAsync()
    {
        if (busy) return;

        if (IsChromeRunning())
        {
            UpdateChromeState(true);
            MessageBox.Show(this,
                "Chrome is still running.\r\n\r\nClose Chrome first, then scan again.",
                "Chrome must be closed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!cacheBox.Checked && !storageBox.Checked)
        {
            MessageBox.Show(this, "Select at least one cleanup item.",
                "Nothing selected", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (storageBox.Checked)
        {
            var answer = MessageBox.Show(this,
                "Local Storage may contain website sessions and offline data.\r\n\r\nDelete it?",
                "Confirm Local Storage",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (answer != DialogResult.Yes)
                storageBox.Checked = false;
        }

        if (!cacheBox.Checked && !storageBox.Checked)
            return;

        busy = true;
        scanButton.Enabled = false;
        cleanButton.Enabled = false;
        closeChromeButton.Enabled = false;
        activityLabel.Text = "CLEANING...";
        activityLabel.ForeColor = Color.FromArgb(100, 181, 246);
        resultLabel.Text = "Removing selected cache data...";
        progress.Value = 5;

        var cacheBefore = GetSizeForAllProfiles(CachePaths);
        var storageBefore = GetSizeForAllProfiles(LocalStoragePaths);

        long deleted = 0;

        await Task.Run(() =>
        {
            if (cacheBox.Checked)
            {
                deleted += DeleteForAllProfiles(CachePaths);
                BeginInvoke(() => progress.Value = 55);
            }

            if (storageBox.Checked)
            {
                deleted += DeleteForAllProfiles(LocalStoragePaths);
                BeginInvoke(() => progress.Value = 90);
            }
        });

        var cacheAfter = GetSizeForAllProfiles(CachePaths);
        var storageAfter = GetSizeForAllProfiles(LocalStoragePaths);

        cacheSizeLabel.Text = FormatSize(cacheAfter);
        storageSizeLabel.Text = FormatSize(storageAfter);
        progress.Value = 100;

        busy = false;
        UpdateChromeState(true);
        scanButton.Enabled = true;

        activityLabel.Text = "CLEANUP COMPLETE";
        activityLabel.ForeColor = Color.FromArgb(87, 227, 137);
        resultLabel.Text =
            $"Deleted {FormatSize(deleted)} • Cache {FormatSize(cacheBefore)} → {FormatSize(cacheAfter)}";

        MessageBox.Show(this,
            $"Cleanup completed successfully.\r\n\r\n" +
            $"Deleted: {FormatSize(deleted)}\r\n" +
            $"Cache remaining: {FormatSize(cacheAfter)}\r\n" +
            $"Local Storage remaining: {FormatSize(storageAfter)}",
            "Cleanup Complete",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void CloseChrome()
    {
        var processes = Process.GetProcessesByName("chrome");

        if (processes.Length == 0)
        {
            UpdateChromeState(true);
            return;
        }

        var answer = MessageBox.Show(this,
            "Close all Google Chrome windows now?\r\n\r\n" +
            "Chrome will first be asked to close normally.",
            "Close Chrome",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
            return;

        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                    process.CloseMainWindow();
            }
            catch { }
        }

        var deadline = DateTime.UtcNow.AddSeconds(8);
        while (DateTime.UtcNow < deadline && IsChromeRunning())
        {
            Application.DoEvents();
            Thread.Sleep(200);
        }

        if (IsChromeRunning())
        {
            var force = MessageBox.Show(this,
                "Chrome did not close normally.\r\n\r\n" +
                "Force-close Chrome? Unsaved browser work may be lost.",
                "Chrome still running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (force == DialogResult.Yes)
            {
                foreach (var process in Process.GetProcessesByName("chrome"))
                {
                    try { process.Kill(); } catch { }
                }
            }
        }

        UpdateChromeState(true);
    }

    private bool IsChromeRunning()
    {
        try
        {
            return Process.GetProcessesByName("chrome")
                .Any(p =>
                {
                    try { return !p.HasExited; }
                    catch { return false; }
                });
        }
        catch
        {
            return false;
        }
    }

    private long GetSizeForAllProfiles(string[] relativePaths)
    {
        if (!Directory.Exists(chromeRoot))
            return 0;

        long total = 0;

        foreach (var profile in GetChromeProfiles())
        {
            foreach (var relative in relativePaths)
                total += DirectorySize(Path.Combine(profile, relative));
        }

        return total;
    }

    private long DeleteForAllProfiles(string[] relativePaths)
    {
        if (!Directory.Exists(chromeRoot))
            return 0;

        long deleted = 0;

        foreach (var profile in GetChromeProfiles())
        {
            foreach (var relative in relativePaths)
                deleted += DeleteDirectoryContents(Path.Combine(profile, relative));
        }

        return deleted;
    }

    private IEnumerable<string> GetChromeProfiles()
    {
        if (!Directory.Exists(chromeRoot))
            yield break;

        IEnumerable<string> dirs;
        try { dirs = Directory.EnumerateDirectories(chromeRoot); }
        catch { yield break; }

        foreach (var dir in dirs)
        {
            var name = Path.GetFileName(dir);
            if (name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase))
                yield return dir;
        }
    }

    private static long DirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long total = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { total += new FileInfo(file).Length; }
                catch { }
            }
        }
        catch { }

        return total;
    }

    private static long DeleteDirectoryContents(string path)
    {
        if (!Directory.Exists(path))
            return 0;

        long deleted = 0;

        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var length = new FileInfo(file).Length;
                    File.Delete(file);
                    deleted += length;
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories)
                .OrderByDescending(x => x.Length))
            {
                try { Directory.Delete(dir, false); }
                catch { }
            }
        }
        catch { }

        return deleted;
    }

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        int unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:N2} {units[unit]}";
    }
}
