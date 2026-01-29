using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Shogun.Avalonia;

/// <summary>
/// bool をステータステキストに変換するコンバーター。
/// </summary>
public class BoolToStatusConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isProcessing)
        {
            return isProcessing ? "AI処理中..." : "待機中";
        }
        return "待機中";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
