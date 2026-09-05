namespace frm_avg_unin_support_tool
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            // 1 — AutoScaleMode.Font: form content scales proportionally when the
            //     system DPI differs from the 96 DPI design baseline.
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode       = System.Windows.Forms.AutoScaleMode.Font;

            this.ClientSize      = new System.Drawing.Size(500, 215);
            this.Text            = "AVG Uninstaller Support Tool";
            this.BackColor       = System.Drawing.Color.White;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox     = false;
            this.MinimizeBox     = false;
            this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterScreen;
        }
    }
}
