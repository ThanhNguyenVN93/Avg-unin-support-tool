using System;
using System.Drawing;
using System.Windows.Forms;

namespace frm_avg_unin_support_tool
{
    public partial class DoneForm : Form
    {
        public DoneForm()
        {
            InitializeComponent();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            BuildUI();
        }

        private void BuildUI()
        {
            var lblTitle = new Label
            {
                Text      = Strings.DoneTitle,
                Location  = new Point(20, 20),
                Size      = new Size(460, 46),
                Font      = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 140, 0),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            var lblMsg = new Label
            {
                Text      = Strings.DoneMessage,
                Location  = new Point(20, 74),
                Size      = new Size(460, 60),
                Font      = new Font("Segoe UI", 10f),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            var btnClose = new Button
            {
                Text      = Strings.BtnClose,
                Location  = new Point(175, 148),
                Size      = new Size(150, 36),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(0, 140, 0),
                ForeColor = Color.White,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9f, FontStyle.Bold),
            };
            btnClose.FlatAppearance.BorderSize = 0;
            btnClose.Click += (s, ev) => Close();

            Controls.Add(lblTitle);
            Controls.Add(lblMsg);
            Controls.Add(btnClose);
        }
    }
}
