using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    public partial class Form1 : Form
    {
        // ── Countdown state ────────────────────────────────────────────────────
        private const int TotalSeconds = 10;
        private DateTime  _startTime;
        private Timer     _timer;
        private bool      _restarting;

        // ── Layout heights ─────────────────────────────────────────────────────
        private const int HeightCollapsed = 215;
        private const int HeightExpanded  = 285;
        private const int FooterHeight    = 28;

        // ── Controls kept as fields for text refresh ───────────────────────────
        private Label         _lblNotice;
        private Label         _lblCountdown;
        private Button        _btnNow;
        private Button        _btnCancel;
        private CheckBox      _chkAutoLogon;
        private Label         _lblPassword;
        private TextBox       _txtPassword;
        private Button        _btnLang;
        private LinkLabel     _lnkUpdate;
        private string        _pendingUpdateTag;
        private TableLayoutPanel _table;

        // ── Fonts (disposed in OnFormClosed) ──────────────────────────────────
        private Font _fontBody;
        private Font _fontCountdown;
        private Font _fontBtnPrimary;
        private Font _fontBtnSecondary;
        private Font _fontFooter;

        public Form1() { InitializeComponent(); }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildUI();
            StartCountdown();
            Updater.CheckAsync(OnUpdateResult);
        }

        // ── UI construction ────────────────────────────────────────────────────

        private void BuildUI()
        {
            _fontBody         = new Font("Segoe UI", 10f);
            _fontCountdown    = new Font("Segoe UI", 10f, FontStyle.Bold);
            _fontBtnPrimary   = new Font("Segoe UI", 9f,  FontStyle.Bold);
            _fontBtnSecondary = new Font("Segoe UI", 9f);
            _fontFooter       = new Font("Segoe UI", 8f);

            _table = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 6,
                Padding     = new Padding(20, 12, 20, 8),
                BackColor   = Color.White,
            };
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60f));
            _table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40f));

            // Row heights (autologon rows start at 0 = hidden)
            _table.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // 0: notice (fills remaining)
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));  // 1: autologon checkbox
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 0f));  // 2: password row
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f)); // 3: countdown
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, 44f)); // 4: buttons
            _table.RowStyles.Add(new RowStyle(SizeType.Absolute, FooterHeight)); // 5: footer

            // Row 0 — notice
            _lblNotice = new Label
            {
                Text      = Strings.SafeModeNotice,
                Dock      = DockStyle.Fill,
                Font      = _fontBody,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            _table.SetColumnSpan(_lblNotice, 2);
            _table.Controls.Add(_lblNotice, 0, 0);

            // Row 1 — autologon checkbox
            _chkAutoLogon = new CheckBox
            {
                Text      = Strings.ChkAutoLogon,
                Dock      = DockStyle.Fill,
                Font      = _fontFooter,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible   = false,
            };
            _chkAutoLogon.CheckedChanged += OnAutoLogonToggled;
            _table.SetColumnSpan(_chkAutoLogon, 2);
            _table.Controls.Add(_chkAutoLogon, 0, 1);

            // Row 2 — password row
            _lblPassword = new Label
            {
                Text      = Strings.LblPassword,
                Dock      = DockStyle.Fill,
                Font      = _fontFooter,
                TextAlign = ContentAlignment.MiddleRight,
                Visible   = false,
            };
            _table.Controls.Add(_lblPassword, 0, 2);

            _txtPassword = new TextBox
            {
                Dock         = DockStyle.Fill,
                PasswordChar = '•',
                Font         = _fontFooter,
                Margin       = new Padding(4, 4, 0, 4),
                Visible      = false,
            };
            _table.Controls.Add(_txtPassword, 1, 2);

            // Row 3 — countdown
            _lblCountdown = new Label
            {
                Text      = Strings.Countdown(TotalSeconds),
                Dock      = DockStyle.Fill,
                Font      = _fontCountdown,
                ForeColor = Color.FromArgb(0, 118, 206),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            _table.SetColumnSpan(_lblCountdown, 2);
            _table.Controls.Add(_lblCountdown, 0, 3);

            // Row 4 — buttons
            _btnNow = new Button
            {
                Text      = Strings.BtnRestartNow,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(0, 4, 6, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 118, 206),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = _fontBtnPrimary,
            };
            _btnNow.FlatAppearance.BorderSize = 0;
            _btnNow.Click += async (s, ev) =>
            {
                try { await DoRestart(); }
                catch (Exception ex) { Logger.WriteException("BtnNow", ex); }
            };
            _table.Controls.Add(_btnNow, 0, 4);

            _btnCancel = new Button
            {
                Text      = Strings.BtnCancel,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(6, 4, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(230, 230, 230),
                ForeColor = Color.Black,
                Cursor    = Cursors.Hand,
                Font      = _fontBtnSecondary,
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += async (s, ev) =>
            {
                try { await DoCancel(); }
                catch (Exception ex) { Logger.WriteException("BtnCancel", ex); }
            };
            _table.Controls.Add(_btnCancel, 1, 4);

            // Row 5 — footer (language toggle | update link)
            _btnLang = new Button
            {
                Text      = Strings.LangButtonText,
                Dock      = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(240, 240, 240),
                ForeColor = Color.FromArgb(80, 80, 80),
                Cursor    = Cursors.Hand,
                Font      = _fontFooter,
                Margin    = new Padding(0, 2, 6, 0),
            };
            _btnLang.FlatAppearance.BorderSize = 0;
            _btnLang.Click += (s, ev) => ToggleLanguage();
            _table.Controls.Add(_btnLang, 0, 5);

            _lnkUpdate = new LinkLabel
            {
                Text      = "",
                Dock      = DockStyle.Fill,
                Font      = _fontFooter,
                TextAlign = ContentAlignment.MiddleRight,
                Visible   = false,
            };
            _lnkUpdate.LinkClicked += OnUpdateLinkClicked;
            _table.Controls.Add(_lnkUpdate, 1, 5);

            Controls.Add(_table);
        }

        // ── Language toggle ────────────────────────────────────────────────────

        private void ToggleLanguage()
        {
            Strings.ToggleLanguage();
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            _lblNotice.Text        = Strings.SafeModeNotice;
            _chkAutoLogon.Text     = Strings.ChkAutoLogon;
            _lblPassword.Text      = Strings.LblPassword;
            _btnNow.Text           = Strings.BtnRestartNow;
            _btnCancel.Text        = Strings.BtnCancel;
            _btnLang.Text          = Strings.LangButtonText;
        }

        // ── AutoLogon section toggle ───────────────────────────────────────────

        private void OnAutoLogonToggled(object sender, EventArgs e)
        {
            bool show = _chkAutoLogon.Checked;
            _lblPassword.Visible = show;
            _txtPassword.Visible = show;
            _table.RowStyles[2]  = new RowStyle(SizeType.Absolute, show ? 36f : 0f);
            if (show && string.IsNullOrEmpty(_txtPassword.Text))
                _txtPassword.Focus();
        }

        // ── Countdown ─────────────────────────────────────────────────────────

        private void StartCountdown()
        {
            _startTime = DateTime.UtcNow;

            // Expand the autologon checkbox row now that the form is visible
            _table.RowStyles[1] = new RowStyle(SizeType.Absolute, 24f);
            _chkAutoLogon.Visible = true;

            _timer = new Timer { Interval = 250 };
            _timer.Tick += async (s, e) =>
            {
                try
                {
                    if (IsDisposed || _lblCountdown == null || _lblCountdown.IsDisposed) return;

                    int elapsed   = (int)(DateTime.UtcNow - _startTime).TotalSeconds;
                    int remaining = Math.Max(0, TotalSeconds - elapsed);
                    _lblCountdown.Text = Strings.Countdown(remaining);

                    if (remaining <= 0)
                    {
                        _timer.Stop();
                        await DoRestart();
                    }
                }
                catch (ObjectDisposedException) { }
                catch (Exception ex) { Logger.WriteException("Form1.TimerTick", ex); }
            };
            _timer.Start();
        }

        // ── Actions ────────────────────────────────────────────────────────────

        private async Task DoRestart()
        {
            if (_restarting) return;
            _restarting = true;
            _timer?.Stop();
            if (!IsDisposed) { _btnNow.Enabled = false; _btnCancel.Enabled = false; }

            // Arm autologon if the user provided a password
            if (_chkAutoLogon.Checked && !string.IsNullOrEmpty(_txtPassword.Text))
            {
                AutoLogon.Setup(AutoLogon.CurrentUser, _txtPassword.Text);
                // Clear password from memory
                _txtPassword.Text = new string('\0', _txtPassword.Text.Length);
            }

            // RestartForced(5) returns immediately; Windows handles the countdown.
            // Do NOT call Close() — avoids GDI cross-thread race during shutdown.
            await Task.Run(() => WindowsSystemCommands.RestartForced(5));
        }

        private async Task DoCancel()
        {
            _timer?.Stop();
            if (!IsDisposed) { _btnNow.Enabled = false; _btnCancel.Enabled = false; }

            bool safeBootReverted = false;
            await Task.Run(() =>
            {
                Program.DeleteRunKey();
                Program.DeleteSafeModeTrigger();
                Program.ResetAttemptCount();
                AutoLogon.Cancel();
                safeBootReverted = WindowsSystemCommands.DisableSafeBoot();
                WindowsSystemCommands.AbortShutdown();
            });

            if (!IsDisposed)
            {
                if (!safeBootReverted)
                    MessageBox.Show(Strings.BcdClearFailedMessage, Strings.AppTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                else
                    MessageBox.Show(Strings.CancelledMessage, Strings.AppTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                Close();
            }
        }

        // ── Updater callback ──────────────────────────────────────────────────

        private void OnUpdateResult(string latestTag, string downloadUrl)
        {
            if (!Updater.IsNewer(latestTag)) return;

            if (IsDisposed || !IsHandleCreated) return;
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed) return;
                _pendingUpdateTag  = latestTag;
                _lnkUpdate.Text    = Strings.UpdateAvailable(latestTag);
                _lnkUpdate.Tag     = downloadUrl;
                _lnkUpdate.Visible = true;
            }));
        }

        private void OnUpdateLinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = _lnkUpdate.Tag as string;
            if (string.IsNullOrEmpty(url))
                return;

            // If the release has no downloadable .exe asset, fall back to opening
            // the release page so the user can grab it manually.
            if (!url.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try { Process.Start(url); }
                catch (Exception ex) { Logger.WriteException("Form1.UpdateLink", ex); }
                return;
            }

            _lnkUpdate.Enabled = false;
            _lnkUpdate.Text    = Strings.UpdateDownloading;

            Updater.DownloadAndApply(url, error =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed) return;
                    _lnkUpdate.Enabled = true;
                    _lnkUpdate.Text    = Strings.UpdateAvailable(_pendingUpdateTag ?? "");
                    MessageBox.Show(Strings.UpdateFailed(error), Strings.AppTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }));
            });
            // On success, Updater.DownloadAndApply calls Application.Exit() itself —
            // this process relaunches as the new version via the staged script.
        }

        // ── Disposal ──────────────────────────────────────────────────────────

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _fontBody?.Dispose();
            _fontCountdown?.Dispose();
            _fontBtnPrimary?.Dispose();
            _fontBtnSecondary?.Dispose();
            _fontFooter?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
