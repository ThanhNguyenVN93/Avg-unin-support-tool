using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    public partial class DoneForm : Form
    {
        private const string RepoUrl = "https://github.com/ThanhNguyenVN93/Avg-unin-support-tool";

        // Controls refreshed on language toggle
        private Label  _lblTitle;
        private Label  _lblMsg;
        private Label  _lblDonate;
        private Button _btnClose;
        private Button _btnDonate;
        private Button _btnLang;

        // Fonts — disposed in OnFormClosed
        private Font _fontTitle;
        private Font _fontMsg;
        private Font _fontBtn;
        private Font _fontDonate;
        private Font _fontFooter;

        public DoneForm() { InitializeComponent(); }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildUI();
        }

        private void BuildUI()
        {
            _fontTitle  = new Font("Segoe UI", 18f, FontStyle.Bold);
            _fontMsg    = new Font("Segoe UI", 10f);
            _fontBtn    = new Font("Segoe UI", 9f,  FontStyle.Bold);
            _fontDonate = new Font("Segoe UI", 9f);
            _fontFooter = new Font("Segoe UI", 8f);

            var table = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                RowCount    = 4,
                Padding     = new Padding(20, 12, 20, 8),
                BackColor   = Color.White,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent,  28f)); // title
            table.RowStyles.Add(new RowStyle(SizeType.Percent,  36f)); // message
            table.RowStyles.Add(new RowStyle(SizeType.Percent,  22f)); // donate prompt + buttons
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f)); // footer

            // Row 0 — title
            _lblTitle = new Label
            {
                Text      = Strings.DoneTitle,
                Dock      = DockStyle.Fill,
                Font      = _fontTitle,
                ForeColor = Color.FromArgb(0, 140, 0),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            table.SetColumnSpan(_lblTitle, 2);
            table.Controls.Add(_lblTitle, 0, 0);

            // Row 1 — message
            _lblMsg = new Label
            {
                Text      = Strings.DoneMessage,
                Dock      = DockStyle.Fill,
                Font      = _fontMsg,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            table.SetColumnSpan(_lblMsg, 2);
            table.Controls.Add(_lblMsg, 0, 1);

            // Row 2 — Close button (left) + Donate button (right)
            _btnClose = new Button
            {
                Text      = Strings.BtnClose,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(0, 4, 6, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 140, 0),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = _fontBtn,
            };
            _btnClose.FlatAppearance.BorderSize = 0;
            _btnClose.Click += (s, ev) => Close();
            table.Controls.Add(_btnClose, 0, 2);

            _btnDonate = new Button
            {
                Text      = Strings.BtnDonate,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(6, 4, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(255, 160, 20),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = _fontDonate,
            };
            _btnDonate.FlatAppearance.BorderSize = 0;
            _btnDonate.Click += (s, ev) =>
            {
                try { Process.Start(RepoUrl + "#donate"); }
                catch (Exception ex) { Logger.WriteException("DoneForm.Donate", ex); }
            };
            table.Controls.Add(_btnDonate, 1, 2);

            // Row 3 — footer (language toggle | log path label)
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
            _btnLang.Click += (s, ev) =>
            {
                Strings.ToggleLanguage();
                RefreshLabels();
            };
            table.Controls.Add(_btnLang, 0, 3);

            _lblDonate = new Label
            {
                Text      = Strings.DonatePrompt,
                Dock      = DockStyle.Fill,
                Font      = _fontFooter,
                ForeColor = Color.FromArgb(120, 120, 120),
                TextAlign = ContentAlignment.MiddleRight,
            };
            table.Controls.Add(_lblDonate, 1, 3);

            Controls.Add(table);
        }

        private void RefreshLabels()
        {
            _lblTitle.Text  = Strings.DoneTitle;
            _lblMsg.Text    = Strings.DoneMessage;
            _lblDonate.Text = Strings.DonatePrompt;
            _btnClose.Text  = Strings.BtnClose;
            _btnDonate.Text = Strings.BtnDonate;
            _btnLang.Text   = Strings.LangButtonText;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fontTitle?.Dispose();
            _fontMsg?.Dispose();
            _fontBtn?.Dispose();
            _fontDonate?.Dispose();
            _fontFooter?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
