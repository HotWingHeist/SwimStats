using System;
using System.Globalization;
using System.Windows.Data;
using SwimStats.Core.Models;

namespace SwimStats.App.Converters
{
    public class CourseToDisplayNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Course course)
            {
                return course switch
                {
                    Course.LongCourse => "50m pool",
                    Course.ShortCourse => "25m pool",
                    _ => course.ToString()
                };
            }
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
