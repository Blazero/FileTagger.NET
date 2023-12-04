using Avalonia.Data.Converters;
using Avalonia.Data;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;

namespace FileTagger.NET;

public class SetToStringConverter : IValueConverter
{
    public static readonly SetToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is HashSet<string> set && targetType.IsAssignableTo(typeof(string)))
        {
            return string.Join(", ", set);
        }
        // converter used for the wrong type
        return new BindingNotification(new InvalidCastException(), BindingErrorType.Error);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
