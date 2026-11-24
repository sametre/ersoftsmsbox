using System.Diagnostics;
using System.Drawing.Drawing2D;
using ERSoftSMSBox.Integrations;

namespace ERSoftSMSBox;

internal sealed partial class StartupSplash : Form
{
    private readonly Stopwatch animationClock = new();

    internal StartupSplash()
    {
        InitializeComponent();
        DoubleBuffered = true;
        Text = AccessibleName = IntegrationText.Get("AppTitle");
        AccessibleDescription = IntegrationText.Get("StartupLoading");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        animationClock.Restart();
        animationTimer.Start();
    }

    private void AnimationTick(object? sender, EventArgs e)
    {
        double elapsed = animationClock.Elapsed.TotalMilliseconds;
        if (elapsed >= 1600)
        {
            animationTimer.Stop();
            DialogResult = DialogResult.OK;
            return;
        }

        Opacity = Math.Clamp(Math.Min(elapsed / 250, (1600 - elapsed) / 250), 0, 1);
        Invalidate();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        animationTimer.Stop();
        animationClock.Stop();
        base.OnFormClosed(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        Graphics graphics = e.Graphics;
        GraphicsState state = graphics.Save();
        try
        {
            graphics.ScaleTransform(ClientSize.Width / 520f, ClientSize.Height / 320f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using LinearGradientBrush background = new(new Rectangle(0, 0, 520, 320),
                Color.FromArgb(18, 25, 42), Color.FromArgb(29, 53, 78), 35f);
            graphics.FillRectangle(background, 0, 0, 520, 320);

            float angle = (float)(animationClock.Elapsed.TotalMilliseconds * 0.2);
            using Pen ring = new(Color.FromArgb(62, 205, 224), 3);
            graphics.DrawArc(ring, 203, 30, 114, 114, angle, 250);
            using SolidBrush emblem = new(Color.FromArgb(35, 90, 125));
            graphics.FillEllipse(emblem, 216, 43, 88, 88);
            using Pen envelope = new(Color.White, 3) { LineJoin = LineJoin.Round };
            graphics.DrawRectangle(envelope, 234, 70, 52, 35);
            Point[] flap = [new(234, 70), new(260, 89), new(286, 70)];
            graphics.DrawLines(envelope, flap);

            using Font titleFont = new("Segoe UI", 25, FontStyle.Bold, GraphicsUnit.Pixel);
            using Font subtitleFont = new("Segoe UI", 15, FontStyle.Regular, GraphicsUnit.Pixel);
            using SolidBrush subtitleBrush = new(Color.FromArgb(185, 205, 225));
            using StringFormat centered = new() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            graphics.DrawString(Text, titleFont, Brushes.White, new RectangleF(20, 163, 480, 48), centered);
            graphics.DrawString(AccessibleDescription, subtitleFont, subtitleBrush,
                new RectangleF(20, 217, 480, 44), centered);

            using Pen track = new(Color.FromArgb(50, 75, 98), 3);
            graphics.DrawLine(track, 140, 284, 380, 284);
            float progress = (float)Math.Clamp(animationClock.Elapsed.TotalMilliseconds / 1350, 0, 1);
            graphics.DrawLine(ring, 140, 284, 140 + 240 * progress, 284);
        }
        finally
        {
            graphics.Restore(state);
        }
    }
}
