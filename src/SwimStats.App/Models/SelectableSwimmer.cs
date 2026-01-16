using CommunityToolkit.Mvvm.ComponentModel;
using SwimStats.Core.Models;

namespace SwimStats.App.Models
{
    public partial class SelectableSwimmer : ObservableObject
    {
        public Swimmer Swimmer { get; }

        [ObservableProperty]
        private bool isSelected;

        public string Name => Swimmer.Name;
        public int Id => Swimmer.Id;

        public SelectableSwimmer(Swimmer swimmer)
        {
            Swimmer = swimmer;
        }

        partial void OnIsSelectedChanged(bool value)
        {
            // Notify parent when selection changes
        }
    }
}
