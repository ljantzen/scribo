using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia;
using Avalonia.Controls;

namespace Scribo.Converters;

public class BooleanToGridLengthConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string param)
        {
            var parts = param.Split(',');
            if (parts.Length == 2)
            {
                var trueValue = parts[0].Trim();
                var falseValue = parts[1].Trim();
                
                if (boolValue)
                {
                    return ParseGridLength(trueValue);
                }
                else
                {
                    return ParseGridLength(falseValue);
                }
            }
        }
        
        return GridLength.Auto;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private GridLength ParseGridLength(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return GridLength.Auto;
            
        value = value.Trim();
        
        if (value == "Auto")
            return GridLength.Auto;
            
        if (value == "*")
            return new GridLength(1, GridUnitType.Star);
            
        if (value == "0" || value == "0*")
            return new GridLength(0, GridUnitType.Star);
            
        if (value.EndsWith("*"))
        {
            var starValue = value.Substring(0, value.Length - 1);
            if (double.TryParse(starValue, out var num))
                return new GridLength(num, GridUnitType.Star);
        }
        
        if (double.TryParse(value, out var pixelValue))
        {
            // Use star sizing with 0 stars for 0 pixel values to ensure no space is reserved
            if (pixelValue == 0)
                return new GridLength(0, GridUnitType.Star);
            return new GridLength(pixelValue);
        }
            
        return GridLength.Auto;
    }
}
