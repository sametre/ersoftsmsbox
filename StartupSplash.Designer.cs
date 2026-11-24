namespace ERSoftSMSBox;

partial class StartupSplash
{
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        animationTimer = new System.Windows.Forms.Timer(components);
        SuspendLayout();
        animationTimer.Interval = 16;
        animationTimer.Tick += AnimationTick;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(18, 25, 42);
        ClientSize = new Size(520, 320);
        ControlBox = false;
        FormBorderStyle = FormBorderStyle.None;
        Name = "StartupSplash";
        Opacity = 0;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    private System.ComponentModel.IContainer components;
    private System.Windows.Forms.Timer animationTimer;
}
