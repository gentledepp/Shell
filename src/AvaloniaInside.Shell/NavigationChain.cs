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

   /// <summary>
   /// True while this back-stack entry has been seeded but its view/argument have not yet been
   /// created. A deferred entry has a null <see cref="Instance"/> until it is first navigated to
   /// (on back), at which point it is materialized via <see cref="DeferredArgumentFactory"/>.
   /// </summary>
   public bool IsDeferred { get; internal set; }

   /// <summary>
   /// Resolves the navigation argument for a deferred entry the first time it is revealed.
   /// Runs on the navigation thread and may do async work (e.g. loading data). Null once consumed.
   /// </summary>
   public Func<System.Threading.CancellationToken, System.Threading.Tasks.Task<object?>>? DeferredArgumentFactory { get; internal set; }

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
