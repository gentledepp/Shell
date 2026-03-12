using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AvaloniaInside.Shell;

public class NavigationChain : INotifyPropertyChanged
{
   private bool _isVisible = true;

   public NavigationNode Node { get; internal set; } = default!;
   public object Instance { get; internal set; } = default!;
   public NavigateType Type { get; internal set; }
   public Uri Uri { get; internal set; } = default!;
   public NavigationChain? Back { get; internal set; }
   public bool Hosted { get; internal set; }

   public bool IsVisible
   {
      get => _isVisible;
      set
      {
         if (_isVisible == value) return;
         _isVisible = value;
         OnPropertyChanged();
      }
   }

   public IEnumerable<NavigationChain> GetAscendingNodes()
   {
      yield return this;
      if (Back == null) yield break;

      foreach (var node in Back.GetAscendingNodes())
         yield return node;
   }

   public event PropertyChangedEventHandler? PropertyChanged;

   protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
      => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
