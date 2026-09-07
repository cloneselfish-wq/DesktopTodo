using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace DesktopTodo
{
    // Shared colors and small UI builders for the modern light theme:
    // white cards on a soft gray page, one indigo accent, hover-revealed actions.
    internal static class Theme
    {
        public static readonly SolidColorBrush Accent = Frozen(0x4F, 0x6B, 0xED);
        public static readonly SolidColorBrush AccentDark = Frozen(0x43, 0x5C, 0xD6);
        public static readonly SolidColorBrush TextPrimary = Frozen(0x1F, 0x24, 0x30);
        public static readonly SolidColorBrush TextSecondary = Frozen(0x6B, 0x72, 0x80);
        public static readonly SolidColorBrush TextTertiary = Frozen(0xA0, 0xA6, 0xB1);
        public static readonly SolidColorBrush PageBg = Frozen(0xF4, 0xF5, 0xF9);
        public static readonly SolidColorBrush CardBorder = Frozen(0xEC, 0xEE, 0xF4);
        public static readonly SolidColorBrush CardBorderHover = Frozen(0xD8, 0xDF, 0xF6);
        public static readonly SolidColorBrush CheckBorder = Frozen(0xC4, 0xCB, 0xD8);
        public static readonly SolidColorBrush HoverFill = Frozen(0xF3, 0xF4, 0xF8);
        public static readonly SolidColorBrush Danger = Frozen(0xE5, 0x48, 0x4D);
        public static readonly SolidColorBrush DangerFill = Frozen(0xFD, 0xEC, 0xEC);
        public static readonly SolidColorBrush ChipBg = Frozen(0xF5, 0xF6, 0xFA);
        public static readonly SolidColorBrush ChipBorder = Frozen(0xE4, 0xE7, 0xEF);

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            SolidColorBrush br = new SolidColorBrush(Color.FromRgb(r, g, b));
            br.Freeze();
            return br;
        }

        // Soft drop shadow for cards and popups (frozen; one instance can be shared).
        public static DropShadowEffect CardShadow()
        {
            DropShadowEffect e = new DropShadowEffect();
            e.Color = Color.FromRgb(0x23, 0x2D, 0x4D);
            e.BlurRadius = 16;
            e.ShadowDepth = 3;
            e.Direction = 270;
            e.Opacity = 0.10;
            e.Freeze();
            return e;
        }

        public static TextBlock Glyph(string text, double size, Brush brush)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontFamily = new FontFamily("Segoe MDL2 Assets");
            t.FontSize = size;
            t.Foreground = brush;
            return t;
        }

        // Round ghost icon button used in the title bar and the settings header.
        public static Border GhostButton(string glyph, double fontSize, string tooltip, bool dangerHover, Action onClick)
        {
            Border b = new Border();
            b.Width = 32;
            b.Height = 28;
            b.CornerRadius = new CornerRadius(8);
            b.Background = Brushes.Transparent;
            b.Cursor = Cursors.Hand;
            if (!string.IsNullOrEmpty(tooltip)) b.ToolTip = tooltip;

            TextBlock t = Glyph(glyph, fontSize, TextSecondary);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;

            b.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e) { e.Handled = true; };
            b.MouseEnter += delegate
            {
                b.Background = dangerHover ? DangerFill : HoverFill;
                t.Foreground = dangerHover ? Danger : TextSecondary;
            };
            b.MouseLeave += delegate
            {
                b.Background = Brushes.Transparent;
                t.Foreground = TextSecondary;
            };
            b.MouseLeftButtonUp += delegate { onClick(); };
            return b;
        }

        // Rounded-square check target; the caller animates its state on completion.
        public static Border CheckCircle()
        {
            Border cb = new Border();
            cb.Width = 20;
            cb.Height = 20;
            cb.CornerRadius = new CornerRadius(7);
            cb.BorderBrush = CheckBorder;
            cb.BorderThickness = new Thickness(1.5);
            cb.Background = Brushes.White;
            cb.Cursor = Cursors.Hand;

            TextBlock check = Glyph("\uE73E", 10, Brushes.White);
            check.FontWeight = FontWeights.Bold;
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
            check.Visibility = Visibility.Collapsed;
            cb.Child = check;
            return cb;
        }

        // Slim modern scrollbar: thin rounded thumb, no track chrome, no arrows.
        // Built from a XAML string because Track.Thumb and Track's repeat buttons
        // are plain CLR properties that FrameworkElementFactory cannot set.
        public static Style SlimScrollBar()
        {
            try
            {
                string x =
                    "<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
                    " xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'>" +
                "<Setter Property='Width' Value='10'/>" +
                "<Setter Property='Background' Value='Transparent'/>" +
                "<Setter Property='Template'>" +
                "<Setter.Value>" +
                "<ControlTemplate TargetType='ScrollBar'>" +
                "<Track x:Name='PART_Track' IsDirectionReversed='true'" +
                " Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}'" +
                " Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'>" +
                "<Track.DecreaseRepeatButton>" +
                "<RepeatButton Command='ScrollBar.PageUpCommand' Focusable='False'>" +
                "<RepeatButton.Template>" +
                "<ControlTemplate TargetType='RepeatButton'><Rectangle Fill='Transparent'/></ControlTemplate>" +
                "</RepeatButton.Template>" +
                "</RepeatButton>" +
                "</Track.DecreaseRepeatButton>" +
                "<Track.IncreaseRepeatButton>" +
                "<RepeatButton Command='ScrollBar.PageDownCommand' Focusable='False'>" +
                "<RepeatButton.Template>" +
                "<ControlTemplate TargetType='RepeatButton'><Rectangle Fill='Transparent'/></ControlTemplate>" +
                "</RepeatButton.Template>" +
                "</RepeatButton>" +
                "</Track.IncreaseRepeatButton>" +
                "<Track.Thumb>" +
                "<Thumb Width='8'>" +
                "<Thumb.Template>" +
                "<ControlTemplate TargetType='Thumb'>" +
                "<Border x:Name='bd' Background='#C7CDD9' CornerRadius='3' Margin='2,0'/>" +
                "<ControlTemplate.Triggers>" +
                "<Trigger Property='IsMouseOver' Value='True'>" +
                "<Setter TargetName='bd' Property='Background' Value='#A9B1C0'/>" +
                "</Trigger>" +
                "</ControlTemplate.Triggers>" +
                "</ControlTemplate>" +
                "</Thumb.Template>" +
                "</Thumb>" +
                "</Track.Thumb>" +
                "</Track>" +
                "</ControlTemplate>" +
                "</Setter.Value>" +
                "</Setter>" +
                "</Style>";
                return (Style)System.Windows.Markup.XamlReader.Parse(x);
            }
            catch (Exception ex)
            {
                // Styling must never take the app down; log and fall back to the default scrollbar.
                Logger.Log("SlimScrollBar: " + ex.Message);
                return null;
            }
        }
    }
}
