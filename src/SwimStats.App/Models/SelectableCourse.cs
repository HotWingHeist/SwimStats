using CommunityToolkit.Mvvm.ComponentModel;
using SwimStats.Core.Models;

namespace SwimStats.App.Models
{
    public partial class SelectableCourse : ObservableObject
    {
        public Course CourseValue { get; }

        [ObservableProperty]
        private bool isSelected;

        public string Name { get; }

        public SelectableCourse(Course course, string displayName)
        {
            CourseValue = course;
            Name = displayName;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            // Notify parent when selection changes
        }
    }
}
