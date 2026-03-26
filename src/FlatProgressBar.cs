using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.ComponentModel;
using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;

namespace DarkModeForms
{
    public class FlatProgressBar : ProgressBar
    {
        private Timer marqueeTimer;
        private float marqueePosition = 0f;
        private ProgressBarStyle style = ProgressBarStyle.Blocks;

        [DefaultValue(ProgressBarStyle.Blocks)]
        public new ProgressBarStyle Style
        {
            get { return style; }
            set
            {
                style = value;
                if (style == ProgressBarStyle.Marquee)
                {
                    marqueeTimer.Start();
                }
                else
                {
                    marqueeTimer.Stop();
                }
                this.Invalidate();
            }
        }

        public FlatProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            marqueeTimer = new Timer();
            marqueeTimer.Interval = 16; // ~60 fps
            marqueeTimer.Tick += MarqueeTimer_Tick;
        }

        private void MarqueeTimer_Tick(object? sender, EventArgs e)
        {
            marqueePosition += 3f;
            if (marqueePosition > this.Width + this.Width / 3f)
            {
                marqueePosition = -this.Width / 3f;
            }
            this.Invalidate();
        }

        private int min = 0;
        private int max = 100;
        private int val = 0;

        [DefaultValue(typeof(Color), "SteelBlue")]
        public Color BarColor { get; set; } = Color.SteelBlue;

        [DefaultValue(6)]
        public int CornerRadius { get; set; } = 6;

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            this.Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = this.ClientRectangle;
            int r = Math.Min(CornerRadius, Math.Min(rect.Width, rect.Height) / 2);

            // Background
            using (GraphicsPath bgPath = CreateRoundedPath(rect, r))
            using (SolidBrush backBrush = new SolidBrush(this.BackColor))
            {
                g.FillPath(backBrush, bgPath);
            }

            // Foreground bar
            if (Style == ProgressBarStyle.Marquee)
            {
                int marqueeWidth = this.Width / 3;
                int mx = (int)marqueePosition;
                Rectangle marqueeRect = new Rectangle(mx, 0, marqueeWidth, this.Height);
                Rectangle clippedRect = Rectangle.Intersect(rect, marqueeRect);
                if (clippedRect.Width > 0 && clippedRect.Height > 0)
                {
                    using (GraphicsPath fgPath = CreateRoundedPath(clippedRect, r))
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        clippedRect, GetBarHighlight(BarColor), BarColor, LinearGradientMode.Vertical))
                    {
                        g.FillPath(brush, fgPath);
                    }
                }
            }
            else
            {
                float percent = (max > min)
                    ? Math.Clamp((float)(val - min) / (max - min), 0f, 1f)
                    : 0f;
                int barWidth = (int)(rect.Width * percent);
                if (barWidth > 0)
                {
                    Rectangle fillRect = new Rectangle(rect.X, rect.Y, barWidth, rect.Height);
                    using (GraphicsPath fgPath = CreateRoundedPath(fillRect, r))
                    using (LinearGradientBrush brush = new LinearGradientBrush(
                        fillRect, GetBarHighlight(BarColor), BarColor, LinearGradientMode.Vertical))
                    {
                        g.FillPath(brush, fgPath);
                    }
                }
            }

            // Subtle border
            using (GraphicsPath borderPath = CreateRoundedPath(
                Rectangle.Inflate(rect, -1, -1), Math.Max(r - 1, 0)))
            using (Pen borderPen = new Pen(Color.FromArgb(60, 0, 0, 0), 1f))
            {
                g.DrawPath(borderPen, borderPath);
            }
        }

        private static Color GetBarHighlight(Color color)
        {
            return Color.FromArgb(
                color.A,
                Math.Min(255, color.R + 45),
                Math.Min(255, color.G + 45),
                Math.Min(255, color.B + 45));
        }

        private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0 || rect.Width <= 0 || rect.Height <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            int diameter = radius * 2;
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        [DefaultValue(0)]
        public new int Minimum
        {
            get => min;
            set
            {
                min = value;
                max = Math.Max(max, min);
                val = Math.Max(val, min);

                base.Minimum = min;
                Invalidate();
            }
        }

        [DefaultValue(100)]
        public new int Maximum
        {
            get => max;
            set
            {
                max = value;
                min = Math.Min(min, max);
                val = Math.Min(val, max);

                base.Maximum = max;
                Invalidate();
            }
        }

        [DefaultValue(0)]
        public new int Value
        {
            get => val;
            set
            {
                int oldValue = val;
                val = Math.Clamp(value, min, max);

                base.Value = val;
                if (val != oldValue) Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                marqueeTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
