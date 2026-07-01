using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using ClaudeUsageTracker.Windows.Models;

namespace ClaudeUsageTracker.Windows.Views;

public sealed class StatusToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            UsageStatusLevel.Safe => new SolidColorBrush(Color.FromRgb(52, 168, 83)),
            UsageStatusLevel.Moderate => new SolidColorBrush(Color.FromRgb(251, 140, 0)),
            UsageStatusLevel.Critical => new SolidColorBrush(Color.FromRgb(217, 48, 37)),
            _ => new SolidColorBrush(Colors.Gray)
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
