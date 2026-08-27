using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AccessControlPro.WPF.Helpers;

namespace AccessControlPro.WPF.Controls;

/// <summary>One stop on the guided tour: which control to spotlight and what to say.</summary>
public class CoachStep
{
    public FrameworkElement? Target { get; set; }
    public string TitleAr { get; set; } = "";
    public string TitleEn { get; set; } = "";
    public string BodyAr { get; set; } = "";
    public string BodyEn { get; set; } = "";
}

/// <summary>
/// A first-run guided tour: dims the screen, spotlights one control at a time with a glowing ring,
/// and shows a short plain-language card ("tap here to add a member") with Next / Skip. Runs once on
/// first launch and again whenever the operator taps the "?" help button — so a non-technical owner
/// can learn the app hands-on and, crucially, becomes able to use it WITHOUT calling for help.
/// </summary>
public partial class CoachMarkOverlay : UserControl
{
    private List<CoachStep> _steps = new();
    private int _index;
    public event Action? Completed;

    public CoachMarkOverlay()
    {
        InitializeComponent();
        SizeChanged += (_, _) => { if (Visibility == Visibility.Visible) RenderStep(); };
    }

    public void Start(List<CoachStep> steps)
    {
        _steps = steps ?? new List<CoachStep>();
        if (_steps.Count == 0) { Finish(); return; }
        _index = 0;
        Visibility = Visibility.Visible;
        ApplyStaticLabels();
        // Defer one layout pass so target bounds are valid.
        Dispatcher.BeginInvoke(new Action(RenderStep), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyStaticLabels()
    {
        bool ar = LanguageManager.Instance.IsArabic;
        // Keep the OVERLAY itself Left-To-Right so Canvas coordinates and TransformToVisual agree
        // (an RTL overlay mirrors Canvas.Left and misplaces the spotlight). Only the card's CONTENT
        // follows the language direction, so Arabic text still reads/aligns correctly.
        FlowDirection = FlowDirection.LeftToRight;
        Card.FlowDirection = LanguageManager.Instance.FlowDirection;
        SkipText.Text = ar ? "تخطّي" : "Skip";
    }

    private void RenderStep()
    {
        if (_index < 0 || _index >= _steps.Count) { Finish(); return; }
        bool ar = LanguageManager.Instance.IsArabic;
        var step = _steps[_index];

        StepTitle.Text = ar ? step.TitleAr : step.TitleEn;
        StepBody.Text = ar ? step.BodyAr : step.BodyEn;
        StepCounter.Text = $"{_index + 1} / {_steps.Count}";
        NextText.Text = _index == _steps.Count - 1
            ? (ar ? "تم" : "Done")
            : (ar ? "التالي" : "Next");

        var full = new Rect(0, 0, ActualWidth, ActualHeight);
        Rect target = Rect.Empty;
        try
        {
            if (step.Target != null && step.Target.IsVisible && step.Target.ActualWidth > 0)
            {
                // TransformBounds gives the correct axis-aligned rect even when the target sits inside
                // an RTL (mirrored) container — Transform(Point 0,0) would land on the wrong corner.
                target = step.Target.TransformToVisual(this)
                    .TransformBounds(new Rect(0, 0, step.Target.ActualWidth, step.Target.ActualHeight));
                target.Inflate(6, 6);
            }
        }
        catch { target = Rect.Empty; }

        // Backdrop with a hole around the target (or a plain dim if no target).
        var outer = new RectangleGeometry(full);
        if (target != Rect.Empty)
        {
            var inner = new RectangleGeometry(target, 14, 14);
            MaskPath.Data = Geometry.Combine(outer, inner, GeometryCombineMode.Exclude, null);

            Ring.Visibility = Visibility.Visible;
            Ring.Width = target.Width;
            Ring.Height = target.Height;
            Canvas.SetLeft(Ring, target.Left);
            Canvas.SetTop(Ring, target.Top);

            PositionCard(target, full);
        }
        else
        {
            MaskPath.Data = outer;
            Ring.Visibility = Visibility.Collapsed;
            // Center the card.
            Canvas.SetLeft(Card, Math.Max(20, (full.Width - Card.Width) / 2));
            Canvas.SetTop(Card, Math.Max(20, (full.Height - 200) / 2));
        }
    }

    private void PositionCard(Rect target, Rect full)
    {
        Card.UpdateLayout();
        double cardW = Card.ActualWidth > 0 ? Card.ActualWidth : Card.Width;
        double cardH = Card.ActualHeight > 0 ? Card.ActualHeight : 170;

        // Prefer placing the card to the RIGHT of the target (sidebar items); fall back to below/above.
        double left = target.Right + 18;
        double top = target.Top;

        if (left + cardW > full.Width - 12)
        {
            // Not enough room on the right — place below, or above if the target is low.
            left = Math.Min(target.Left, full.Width - cardW - 12);
            top = target.Bottom + 18;
            if (top + cardH > full.Height - 12)
                top = Math.Max(12, target.Top - cardH - 18);
        }

        left = Math.Max(12, Math.Min(left, full.Width - cardW - 12));
        top = Math.Max(12, Math.Min(top, full.Height - cardH - 12));
        Canvas.SetLeft(Card, left);
        Canvas.SetTop(Card, top);
    }

    private void Next_Click(object sender, RoutedEventArgs e)
    {
        _index++;
        if (_index >= _steps.Count) Finish();
        else RenderStep();
    }

    private void Skip_Click(object sender, RoutedEventArgs e) => Finish();

    private void Finish()
    {
        Visibility = Visibility.Collapsed;
        TourState.MarkSeen();
        Completed?.Invoke();
    }
}
