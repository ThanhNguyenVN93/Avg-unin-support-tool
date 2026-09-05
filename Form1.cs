using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    public partial class Form1 : Form
    {
        // 13 — DateTime-based countdown: accurate regardless of UI-thread load
        private const int TotalSeconds = 10;
        private DateTime  _startTime;
        private Timer     _timer;

        // Keep as fields so DoRestart/DoCancel can disable them
        private Button _btnNow;
        private Button _btnCancel;
        private Label  _lblCountdown;

        private bool _restarting; // guard against double-fire (timer + button race)

        // Font fields — disposed in OnFormClosed
        private Font _fontBody;
        private Font _fontCountdown;
        private Font _fontBtnPrimary;
        private Font _fontBtnSecondary;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildUI();
            StartCountdown();
        }

        // 1 — TableLayoutPanel replaces hardcoded Point/Size coordinates.
        //     Percentage-based rows/columns scale correctly at any DPI.
        private void BuildUI()
        {
            _fontBody         = new Font("Segoe UI", 10f);
            _fontCountdown    = new Font("Segoe UI", 10f, FontStyle.Bold);
            _fontBtnPrimary   = new Font("Segoe UI", 9f,  FontStyle.Bold);
            _fontBtnSecondary = new Font("Segoe UI", 9f);

            var table = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 3,
                Padding     = new Padding(20, 16, 20, 16),
                BackColor   = Color.White,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 55f)); // notice
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 22f)); // countdown
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 23f)); // buttons

            var lblNotice = new Label
            {
                Text      = Strings.SafeModeNotice,
                Dock      = DockStyle.Fill,
                Font      = _fontBody,
                TextAlign = ContentAlignment.MiddleLeft,
            };
            table.SetColumnSpan(lblNotice, 2);
            table.Controls.Add(lblNotice, 0, 0);

            _lblCountdown = new Label
            {
                Text      = Strings.Countdown(TotalSeconds),
                Dock      = DockStyle.Fill,
                Font      = _fontCountdown,
                ForeColor = Color.FromArgb(0, 118, 206),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            table.SetColumnSpan(_lblCountdown, 2);
            table.Controls.Add(_lblCountdown, 0, 1);

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
                try   { await DoRestart(); }
                catch (Exception ex) { Debug.WriteLine("BtnNow: " + ex.Message); }
            };
            table.Controls.Add(_btnNow, 0, 2);

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
                try   { await DoCancel(); }
                catch (Exception ex) { Debug.WriteLine("BtnCancel: " + ex.Message); }
            };
            table.Controls.Add(_btnCancel, 1, 2);

            Controls.Add(table);
        }

        private void StartCountdown()
        {
            _startTime = DateTime.UtcNow;
            _timer = new Timer { Interval = 250 }; // poll every 250 ms for precision
            _timer.Tick += async (s, e) =>
            {
                try
                {
                    // 3 — IsDisposed guard: prevents ObjectDisposedException when
                    //     Close() is called mid-tick (race between timer and user action)
                    if (IsDisposed || _lblCountdown == null || _lblCountdown.IsDisposed)
                        return;

                    int elapsed   = (int)(DateTime.UtcNow - _startTime).TotalSeconds;
                    int remaining = Math.Max(0, TotalSeconds - elapsed);

                    _lblCountdown.Text = Strings.Countdown(remaining);

                    if (remaining <= 0)
                    {
                        _timer.Stop();
                        await DoRestart();
                    }
                }
                catch (ObjectDisposedException) { /* form closed mid-tick — ignore */ }
                catch (Exception ex) { Debug.WriteLine("Timer Tick: " + ex.Message); }
            };
            _timer.Start();
        }

        private async Task DoRestart()
        {
            if (_restarting) return;
            _restarting = true;
            _timer?.Stop();
            if (!IsDisposed) { _btnNow.Enabled = false; _btnCancel.Enabled = false; }

            // 2 — Do NOT call Close() after RestartForced.
            //     Calling Close() while Windows is rendering the logoff screen causes
            //     a GDI cross-thread race (InvalidOperationException / freeze).
            //     Windows will terminate this process when the restart proceeds.
            await Task.Run(() => WindowsSystemCommands.RestartForced(5));
        }

        private async Task DoCancel()
        {
            _timer?.Stop();
            if (!IsDisposed) { _btnNow.Enabled = false; _btnCancel.Enabled = false; }

            // 2 — await ensures all cleanup finishes before the form is touched again.
            //     Also capture DisableSafeBoot() result — if bcdedit fails during cancel,
            //     the machine would boot back into Safe Mode on next restart, so we must
            //     warn the user instead of silently reporting success.
            bool safeBootReverted = false;
            await Task.Run(() =>
            {
                Program.DeleteRunKey();
                Program.DeleteSafeModeTrigger();
                safeBootReverted = WindowsSystemCommands.DisableSafeBoot();
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

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            _fontBody?.Dispose();
            _fontCountdown?.Dispose();
            _fontBtnPrimary?.Dispose();
            _fontBtnSecondary?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
