namespace MasterUnlock
{
    partial class MainForm
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
            SuspendLayout();
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            ClientSize = new System.Drawing.Size(1200, 760);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            MinimumSize = new System.Drawing.Size(1040, 680);
            Name = "MainForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "Master Unlock";
            ResumeLayout(false);
        }
    }
}
