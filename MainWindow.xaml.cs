using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.System;
using Windows.UI;

namespace NeonMicrosoft
{
    public sealed partial class MainWindow : Window
    {
        private Compositor _compositor;
        private ContainerVisual _container;
        private readonly List<Particle> _particles = [];
        private readonly Random _rnd = new();

        private const string Line1 = "MICROSOFT";
        private const string Line2 = "WINUI 3";
        private const string Line3 = "MEHDIMYADI";

        private float _hueShift = 100.0f;
        private bool _particlesCreated = false;
        private Microsoft.UI.Dispatching.DispatcherQueueTimer _renderTimer;

        private const int ParticleSpacing = 12;
        private const int ExtraSpaceBetweenChars = 8;
        private const int LineSpacing = 30;

        public MainWindow()
        {
            this.InitializeComponent();
            RootGrid.SizeChanged += RootGrid_SizeChanged;
            RootGrid.PointerMoved += RootGrid_PointerMoved;
        }

        private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_particlesCreated) return;

            _compositor = ElementCompositionPreview.GetElementVisual(RootGrid).Compositor;
            _container = _compositor.CreateContainerVisual();
            ElementCompositionPreview.SetElementChildVisual(RootGrid, _container);

            CreateTextParticles();
            _particlesCreated = true;

            // start timer after particles exist
            _renderTimer = DispatcherQueue.CreateTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(16);
            _renderTimer.Tick += (s, args) => UpdateParticleColors();
            _renderTimer.Start();
        }

        private void CreateTextParticles()
        {
            var w = (float)RootGrid.ActualWidth;
            var h = (float)RootGrid.ActualHeight;

            string[] lines = [Line1, Line2, Line3];

            // Calculate total height of all lines combined
            // Each character is 7 pixels high, times ParticleSpacing, plus space between lines
            float lineHeight = 7 * ParticleSpacing;
            float totalTextHeight = lines.Length * lineHeight + (lines.Length - 1) * LineSpacing;

            // Start Y so whole block is vertically centered
            float startY = (h - totalTextHeight) / 2;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string text = lines[lineIndex];

                float textWidth = text.Length * (ParticleSpacing * 5 + ExtraSpaceBetweenChars);
                float startX = (w - textWidth) / 2;

                float lineY = startY + lineIndex * (lineHeight + LineSpacing);

                for (int i = 0; i < text.Length; i++)
                {
                    char letter = text[i];
                    for (int px = 0; px < 5; px++)
                    {
                        for (int py = 0; py < 7; py++)
                        {
                            if (CharPixel(letter, px, py))
                            {
                                float x = startX + i * (ParticleSpacing * 5 + ExtraSpaceBetweenChars) + px * ParticleSpacing;
                                float y = lineY + py * ParticleSpacing;
                                CreateParticle(new Vector3(x, y, 0));
                            }
                        }
                    }
                }
            }
        }

        private void CreateParticle(Vector3 targetPos, float scale = 1f)
        {
            var size = 7f * scale;
            var vis = _compositor.CreateSpriteVisual();
            vis.Size = new Vector2(size, size);
            vis.Brush = CreateNeonBrush(0);
            vis.Offset = targetPos + new Vector3(_rnd.Next(-400, 400), _rnd.Next(-400, 400), 0);

            var shadow = _compositor.CreateDropShadow();
            shadow.BlurRadius = 15 * scale;
            shadow.Color = ColorFromHSV(_rnd.Next(0, 360), 1, 1);
            shadow.Opacity = 0.9f;
            vis.Shadow = shadow;

            _container.Children.InsertAtTop(vis);

            var p = new Particle
            {
                Visual = vis,
                TargetPosition = targetPos
            };
            _particles.Add(p);

            AnimateToTarget(p, 1500);
        }

        private void AnimateToTarget(Particle p, int duration)
        {
            var anim = _compositor.CreateVector3KeyFrameAnimation();
            anim.InsertKeyFrame(1f, p.TargetPosition);
            anim.Duration = TimeSpan.FromMilliseconds(duration);
            p.Visual.StartAnimation("Offset", anim);
        }

        private void RootGrid_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var pos = e.GetCurrentPoint(RootGrid).Position;
            var pointer = new Vector3((float)pos.X, (float)pos.Y, 0);

            foreach (var p in _particles)
            {
                var dist = Vector3.Distance(pointer, p.TargetPosition);
                if (dist < 120)
                {
                    var dir = Vector3.Normalize(p.TargetPosition - pointer);
                    var newPos = p.TargetPosition + dir * (200 - dist);

                    var burst = _compositor.CreateVector3KeyFrameAnimation();
                    burst.InsertKeyFrame(1f, newPos);
                    burst.Duration = TimeSpan.FromMilliseconds(200);
                    p.Visual.StartAnimation("Offset", burst);

                    _ = ReturnAfterDelay(p, 200);
                }
            }
        }

        private async System.Threading.Tasks.Task ReturnAfterDelay(Particle p, int delay)
        {
            await System.Threading.Tasks.Task.Delay(delay);
            AnimateToTarget(p, 500);
        }

        private void UpdateParticleColors()
        {
            _hueShift += 0.5f;
            foreach (var p in _particles)
            {
                var brush = CreateNeonBrush(_hueShift + p.TargetPosition.X * 0.2f);
                p.Visual.Brush = brush;

                if (p.Visual.Shadow is DropShadow ds)
                {
                    ds.Color = ColorFromHSV((_hueShift + p.TargetPosition.Y) % 360, 1, 1);
                }
            }
        }

        private CompositionLinearGradientBrush CreateNeonBrush(float hue)
        {
            var brush = _compositor.CreateLinearGradientBrush();
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(0f, ColorFromHSV(hue, 1, 1)));
            brush.ColorStops.Add(_compositor.CreateColorGradientStop(1f, ColorFromHSV(hue + 60, 1, 1)));
            brush.StartPoint = new Vector2(0, 0);
            brush.EndPoint = new Vector2(1, 1);
            return brush;
        }

        private static Color ColorFromHSV(double hue, double saturation, double value)
        {
            hue = (hue % 360 + 360) % 360;
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value *= 255;
            byte v = (byte)(value);
            byte p = (byte)(value * (1 - saturation));
            byte q = (byte)(value * (1 - f * saturation));
            byte t = (byte)(value * (1 - (1 - f) * saturation));

            return hi switch
            {
                0 => Color.FromArgb(255, v, t, p),
                1 => Color.FromArgb(255, q, v, p),
                2 => Color.FromArgb(255, p, v, t),
                3 => Color.FromArgb(255, p, q, v),
                4 => Color.FromArgb(255, t, p, v),
                _ => Color.FromArgb(255, v, p, q)
            };
        }

        private static bool CharPixel(char c, int x, int y)
        {
            var patterns = Font5x7.GetPattern(c);
            return patterns != null && patterns[y][x] == '1';
        }
    }

    public class Particle
    {
        public required SpriteVisual Visual { get; set; }
        public Vector3 TargetPosition { get; set; }
    }

    public static class Font5x7
    {
        public static string[]? GetPattern(char c)
        {
            c = char.ToUpper(c);
            return c switch
            {
                'M' => ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
                'I' => ["11111", "00100", "00100", "00100", "00100", "00100", "11111"],
                'C' => ["01110", "10001", "10000", "10000", "10000", "10001", "01110"],
                'R' => ["11110", "10001", "10001", "11110", "10100", "10010", "10001"],
                'O' => ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
                'S' => ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
                'F' => ["11111", "10000", "10000", "11110", "10000", "10000", "10000"],
                'T' => ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
                'W' => ["10001", "10001", "10001", "10101", "10101", "11011", "10001"],
                'N' => ["10001", "11001", "10101", "10011", "10001", "10001", "10001"],
                'U' => ["10001", "10001", "10001", "10001", "10001", "10001", "01110"],
                '3' => ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
                'Y' => ["10001", "10001", "01010", "00100", "00100", "00100", "00100"],
                'A' => ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
                'D' => ["11110", "10001", "10001", "10001", "10001", "10001", "11110"],
                'H' => ["10001", "10001", "10001", "11111", "10001", "10001", "10001"],
                'E' => ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
                ' ' => ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
                _ => null
            };
        }
    }
}
