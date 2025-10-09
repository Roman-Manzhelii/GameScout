using System.Collections.ObjectModel;
using GameScout.Domain.Models;

namespace GameScout.State;
public class AppState
{
    public ObservableCollection<SavedItem> Backlog { get; } = new();
}
