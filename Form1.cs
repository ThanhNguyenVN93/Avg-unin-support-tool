using System;
using System.Drawing;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    public partial class Form1 : Form
    {
        private int    _countdown = 10;
        private Timer  _timer;
        private Label  _lblNotice;
        private Label  _lblCountdown;
        private Button _btnNow;
        private Button _btnCancel;

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

        private void BuildUI()
        {
            _lblNotice = new Label
            {
                Text      = Strings.SafeModeNotice,
                Location  = new Point(20, 20),
                Size      = new Size(460, 80),
                Font      = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            _lblCountdown = new Label
            {
                Text      = Strings.Countdown(_countdown),
                Location  = new Point(20, 110),
                Size      = new Size(460, 30),
                Font      = new Font("Segoe UI", 10f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 118, 206),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            _btnNow = new Button
            {
                Text      = Strings.BtnRestartNow,
                Location  = new Point(140, 158),
                Size      = new Size(165, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 118, 206),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            _btnNow.FlatAppearance.BorderSize = 0;
            _btnNow.Click += (s, ev) => DoRestart();

            _btnCancel = new Button
            {
                Text      = Strings.BtnCancel,
                Location  = new Point(315, 158),
                Size      = new Size(100, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(230, 230, 230),
                ForeColor = Color.Black,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f),
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, ev) => DoCancel();

            Controls.Add(_lblNotice);
            Controls.Add(_lblCountdown);
            Controls.Add(_btnNow);
            Controls.Add(_btnCancel);
        }

        private void StartCountdown()
        {
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) =>
            {
                _countdown--;
                _lblCountdown.Text = Strings.Countdown(_countdown);
                if (_countdown <= 0)
                {
                    _timer.Stop();
                    DoRestart();
                }
            };
            _timer.Start();
        }

        private void DoRestart()
        {
            _timer?.Stop();
            _btnNow.Enabled    = false;
            _btnCancel.Enabled = false;
            Program.RunSysCmd("shutdown.exe", "/r /t 5");
            Close();
        }

        private void DoCancel()
        {
            _timer?.Stop();
            Program.CancelRunOnce();
            Program.RunSysCmd("bcdedit.exe", "/deletevalue {current} safeboot");
            MessageBox.Show(Strings.CancelledMessage, Strings.AppTitle,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer?.Stop();
            _timer?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
