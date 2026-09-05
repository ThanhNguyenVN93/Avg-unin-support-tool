using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    public partial class DoneForm : Form
    {
        // Font fields — disposed in OnFormClosed
        private Font _fontTitle;
        private Font _fontMsg;
        private Font _fontBtn;

        public DoneForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildUI();
        }

        // 1 — TableLayoutPanel: percentage-based rows scale correctly at any DPI
        private void BuildUI()
        {
            _fontTitle = new Font("Segoe UI", 18f, FontStyle.Bold);
            _fontMsg   = new Font("Segoe UI", 10f);
            _fontBtn   = new Font("Segoe UI", 9f,  FontStyle.Bold);

            var table = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 1,
                RowCount    = 3,
                Padding     = new Padding(20, 16, 20, 16),
                BackColor   = Color.White,
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 33f)); // title
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 42f)); // message
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 25f)); // button

            var lblTitle = new Label
            {
                Text      = Strings.DoneTitle,
                Dock      = DockStyle.Fill,
                Font      = _fontTitle,
                ForeColor = Color.FromArgb(0, 140, 0),
                TextAlign = ContentAlignment.MiddleCenter,
            };
            table.Controls.Add(lblTitle, 0, 0);

            var lblMsg = new Label
            {
                Text      = Strings.DoneMessage,
                Dock      = DockStyle.Fill,
                Font      = _fontMsg,
                TextAlign = ContentAlignment.MiddleCenter,
            };
            table.Controls.Add(lblMsg, 0, 1);

            // Centre the Close button using a 3-column nested table (spacer|btn|spacer)
            var btnRow = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 3,
                RowCount    = 1,
                BackColor   = Color.Transparent,
            };
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            btnRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            btnRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var btnClose = new Button
            {
                Text      = Strings.BtnClose,
                Dock      = DockStyle.Fill,
                Margin    = new Padding(0, 4, 0, 0),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 140, 0),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = _fontBtn,
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, ev) => Close();

            btnRow.Controls.Add(new Label { BackColor = Color.Transparent }, 0, 0); // left spacer
            btnRow.Controls.Add(btnClose, 1, 0);
            btnRow.Controls.Add(new Label { BackColor = Color.Transparent }, 2, 0); // right spacer

            table.Controls.Add(btnRow, 0, 2);
            Controls.Add(table);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _fontTitle?.Dispose();
            _fontMsg?.Dispose();
            _fontBtn?.Dispose();
            base.OnFormClosed(e);
        }
    }
}
