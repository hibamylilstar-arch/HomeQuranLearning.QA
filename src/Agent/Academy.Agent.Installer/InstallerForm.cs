using System.Drawing.Drawing2D;
using System.Reflection;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed class InstallerForm : Form
{
    private const string LogoResourceName =
        "HomeQuranLearning.ClassroomAgent.Setup.logo.jpg";

    private readonly InstallerMode _mode;
    private readonly InstallCoordinator _coordinator = new();
    private readonly RadioButton _teamsOption = new();
    private readonly RadioButton _zoomOption = new();
    private readonly Button _primaryButton = new();
    private readonly Label _statusLabel = new();
    private readonly ProgressBar _progressBar = new();
    private readonly CancellationTokenSource _cancellation = new();

    public InstallerForm(InstallerMode mode)
    {
        _mode = mode;

        Text =
            mode == InstallerMode.Uninstall
                ? "Remove Home Quran Learning Classroom Agent"
                : "Home Quran Learning Classroom Agent Setup";

        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(760, 560);
        MinimumSize = new Size(680, 520);
        BackColor = Color.FromArgb(246, 248, 244);
        Font = new Font("Segoe UI", 10F);

        BuildLayout();
        LoadDeploymentSummary();
    }

    protected override void OnFormClosing(
        FormClosingEventArgs e)
    {
        _cancellation.Cancel();
        base.OnFormClosing(e);
    }

    private void BuildLayout()
    {
        var header =
            new Panel
            {
                Dock = DockStyle.Top,
                Height = 176,
                BackColor = Color.FromArgb(13, 36, 68)
            };

        var logo =
            new PictureBox
            {
                Location = new Point(42, 29),
                Size = new Size(112, 112),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Transparent,
                Image = LoadLogo()
            };

        var title =
            new Label
            {
                AutoSize = true,
                Location = new Point(180, 30),
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 22F),
                Text = "Home Quran Learning"
            };

        var subtitle =
            new Label
            {
                AutoSize = true,
                Location = new Point(183, 90),
                ForeColor = Color.FromArgb(189, 221, 197),
                Font = new Font("Segoe UI Semibold", 13F),
                Text = "Classroom Agent"
            };

        var developer =
            new Label
            {
                AutoSize = true,
                Location = new Point(184, 119),
                ForeColor = Color.FromArgb(193, 204, 219),
                Font = new Font("Segoe UI", 9F),
                Text =
                    "Developed & owned by Abdul Wahid" +
                    Environment.NewLine +
                    "© 2026 Abdul Wahid. All rights reserved."
            };

        header.Controls.Add(logo);
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        header.Controls.Add(developer);
        Controls.Add(header);

        var content =
            new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(46, 28, 46, 28)
            };

        var heading =
            new Label
            {
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 16F),
                ForeColor = Color.FromArgb(21, 45, 70),
                Text =
                    _mode == InstallerMode.Uninstall
                        ? "Remove Classroom Agent"
                        : "Ready for academy monitoring"
            };

        var explanation =
            new Label
            {
                Location = new Point(0, 45),
                Size = new Size(660, 52),
                ForeColor = Color.FromArgb(78, 91, 103),
                Text =
                    _mode == InstallerMode.Uninstall
                        ? "This removes the managed Agent and startup tasks. Device identity, recordings and evidence remain preserved for audit safety."
                        : "Setup installs the Agent, FFmpeg and automatic Windows-login startup. Microsoft Teams attendance evidence is enabled by default."
            };

        content.Controls.Add(heading);
        content.Controls.Add(explanation);

        if (_mode == InstallerMode.InstallOrRepair)
        {
            var platformLabel =
                new Label
                {
                    AutoSize = true,
                    Location = new Point(0, 110),
                    Font = new Font("Segoe UI Semibold", 10F),
                    ForeColor = Color.FromArgb(21, 45, 70),
                    Text = "Class platform"
                };

            _teamsOption.Location = new Point(0, 140);
            _teamsOption.Size = new Size(320, 42);
            _teamsOption.Text =
                "Microsoft Teams (recommended and default)";
            _teamsOption.Checked = true;

            _zoomOption.Location = new Point(350, 140);
            _zoomOption.Size = new Size(250, 42);
            _zoomOption.Text =
                "Zoom / other platform";

            content.Controls.Add(platformLabel);
            content.Controls.Add(_teamsOption);
            content.Controls.Add(_zoomOption);
        }

        _statusLabel.Location =
            new Point(
                0,
                _mode == InstallerMode.InstallOrRepair
                    ? 203
                    : 125);
        _statusLabel.Size = new Size(660, 50);
        _statusLabel.ForeColor = Color.FromArgb(78, 91, 103);
        _statusLabel.Text = "Loading secure release package...";

        _progressBar.Location =
            new Point(
                0,
                _statusLabel.Top + 55);
        _progressBar.Size = new Size(660, 8);
        _progressBar.Style = ProgressBarStyle.Marquee;
        _progressBar.MarqueeAnimationSpeed = 0;

        _primaryButton.Location =
            new Point(
                0,
                _progressBar.Top + 32);
        _primaryButton.Size = new Size(280, 46);
        _primaryButton.FlatStyle = FlatStyle.Flat;
        _primaryButton.FlatAppearance.BorderSize = 0;
        _primaryButton.BackColor = Color.FromArgb(17, 55, 87);
        _primaryButton.ForeColor = Color.White;
        _primaryButton.Font = new Font("Segoe UI Semibold", 10F);
        _primaryButton.Text =
            _mode == InstallerMode.Uninstall
                ? "Remove Classroom Agent"
                : "Install Classroom Agent";
        _primaryButton.Enabled = false;
        _primaryButton.Click += OnPrimaryButtonClick;

        var note =
            new Label
            {
                Location = new Point(0, _primaryButton.Top + 60),
                Size = new Size(660, 42),
                ForeColor = Color.FromArgb(106, 117, 126),
                Font = new Font("Segoe UI", 8.5F),
                Text =
                    "Administrator approval is required once. No PowerShell window or manual teacher command is needed after installation."
            };

        content.Controls.Add(_statusLabel);
        content.Controls.Add(_progressBar);
        content.Controls.Add(_primaryButton);
        content.Controls.Add(note);
        Controls.Add(content);
    }

    private void LoadDeploymentSummary()
    {
        if (_mode == InstallerMode.Uninstall)
        {
            _statusLabel.Text =
                "The preserved evidence directory will not be deleted.";
            _primaryButton.Enabled = true;
            return;
        }

        try
        {
            DeploymentConfig deployment =
                _coordinator.ReadDeployment();

            _statusLabel.Text =
                $"Secure server: {new Uri(deployment.ApiBaseUrl).Host}    Release: {deployment.Version}";
            _primaryButton.Enabled = true;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            _statusLabel.ForeColor = Color.FromArgb(160, 35, 35);
        }
    }

    private async void OnPrimaryButtonClick(
        object? sender,
        EventArgs e)
    {
        _primaryButton.Enabled = false;
        _teamsOption.Enabled = false;
        _zoomOption.Enabled = false;
        _progressBar.MarqueeAnimationSpeed = 25;

        var progress =
            new Progress<string>(
                message => _statusLabel.Text = message);

        try
        {
            if (_mode == InstallerMode.Uninstall)
            {
                await Task.Run(() => _coordinator.UninstallAsync(
                    progress,
                    _cancellation.Token));
            }
            else
            {
                await Task.Run(() => _coordinator.InstallAsync(
                    _teamsOption.Checked,
                    progress,
                    _cancellation.Token));
            }

            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            _progressBar.MarqueeAnimationSpeed = 0;
            _statusLabel.ForeColor = Color.FromArgb(24, 112, 64);

            _primaryButton.Text = "Close";
            _primaryButton.Enabled = true;
            _primaryButton.Click -= OnPrimaryButtonClick;
            _primaryButton.Click += (_, _) => Close();
        }
        catch (OperationCanceledException)
            when (_cancellation.IsCancellationRequested)
        {
            Close();
        }
        catch (Exception ex)
        {
            _progressBar.MarqueeAnimationSpeed = 0;
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 0;
            _statusLabel.ForeColor = Color.FromArgb(160, 35, 35);
            _statusLabel.Text = ex.Message;
            _primaryButton.Enabled = true;
            _teamsOption.Enabled = true;
            _zoomOption.Enabled = true;
        }
    }

    private static Image? LoadLogo()
    {
        using Stream? stream =
            Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(
                    LogoResourceName);

        if (stream is null)
        {
            return null;
        }

        using Image original = Image.FromStream(stream);
        var cropped =
            new Bitmap(
                112,
                112);

        using Graphics graphics =
            Graphics.FromImage(cropped);

        graphics.SmoothingMode =
            SmoothingMode.AntiAlias;
        graphics.Clear(
            Color.Transparent);

        using var path =
            new GraphicsPath();

        path.AddEllipse(
            0,
            0,
            112,
            112);
        graphics.SetClip(path);
        graphics.DrawImage(
            original,
            new Rectangle(
                0,
                0,
                112,
                112));

        return cropped;
    }
}
