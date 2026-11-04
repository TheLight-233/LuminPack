using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LuminPack.Code;

/// <summary>Provides checked storage access for public sequence traversal and observable insertion.</summary>
public static partial class LuminPackCollectionAccess
{
    /// <summary>Finds the exact list or array enumerated by known framework wrappers without bypassing a derived implementation.</summary>
    /// <typeparam name="T">The sequence element type.</typeparam>
    /// <param name="source">The original public sequence.</param>
    /// <returns>The exact list or array used by public enumeration, or null when the layout is unavailable.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static object? GetSequenceStorage<T>(IEnumerable<T> source)
    {
        object? current = source;
        for (int depth = 0; depth <= MaxDepth && current is not null; depth++)
        {
            var type = current.GetType();
            if (type == typeof(List<T>) || type == typeof(T[])) return current;
            if (depth == MaxDepth) return null;
            int offset;
            if (type == typeof(Collection<T>)) offset = LuminCollectionLayouts.CollectionItems;
            else if (type == typeof(ObservableCollection<T>)) offset = LuminCollectionLayouts.ObservableCollectionItems;
            else if (type == typeof(ReadOnlyCollection<T>)) offset = LuminCollectionLayouts.ReadOnlyCollectionList;
            else if (type == typeof(ReadOnlyObservableCollection<T>)) offset = LuminCollectionLayouts.ReadOnlyObservableCollectionList;
            else return null;
            if (offset < 0) return null;
            current = LuminFieldLayout.Field<IList<T>?>(current, offset);
        }
        return null;
    }

    /// <summary>Appends without constructing unobserved event payloads, retaining the public operation whenever callbacks can run.</summary>
    /// <typeparam name="T">The appended element type.</typeparam>
    /// <param name="collection">The existing observable result.</param>
    /// <param name="item">The element after its formatter has completed.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void AppendObservable<T>(ObservableCollection<T> collection, T item)
    {
        if (collection.GetType() == typeof(ObservableCollection<T>) && ObservableListeners.Available &&
            LuminCollectionLayouts.ObservableCollectionItems >= 0 &&
            LuminFieldLayout.Field<object?>(collection, ObservableListeners.CollectionChanged) is null &&
            LuminFieldLayout.Field<object?>(collection, ObservableListeners.PropertyChanged) is null)
        {
            var items = LuminFieldLayout.Field<IList<T>>(collection, LuminCollectionLayouts.ObservableCollectionItems);
            if (items.GetType() == typeof(List<T>))
            {
                Unsafe.As<List<T>>(items).Add(item);
                return;
            }
        }
        collection.Add(item);
    }

    /// <summary>Checks observable listener fields across value and reference element instantiations before using them.</summary>
    private static class ObservableListeners
    {
        internal static readonly int CollectionChanged = Offset("CollectionChanged", (NotifyCollectionChangedEventHandler)OnCollectionChanged);
        internal static readonly int PropertyChanged = Offset("PropertyChanged", (PropertyChangedEventHandler)OnPropertyChanged);
        internal static readonly bool Available = CollectionChanged >= 0 && PropertyChanged >= 0 && Check();

        /// <summary>Finds a listener slot only when both generic instantiations agree.</summary>
        /// <param name="name">The event backing field name.</param>
        /// <param name="marker">A concrete delegate assignable to the listener field.</param>
        /// <returns>The common byte offset, or an unavailable marker.</returns>
        private static int Offset(string name, object marker)
        {
            try
            {
                int first = LuminFieldLayout.ObjectField(typeof(ObservableCollection<int>), name, marker);
                int second = LuminFieldLayout.ObjectField(typeof(ObservableCollection<object>), name, marker);
                return first >= 0 && first == second ? first : LuminFieldLayout.Unavailable;
            }
            catch { return LuminFieldLayout.Unavailable; }
        }

        /// <summary>Verifies empty and subscribed slots through the actual public events.</summary>
        /// <returns>Whether each located slot contains exactly its public subscription.</returns>
        private static bool Check()
        {
            try { return Check<int>() && Check<object>(); }
            catch { return false; }
        }

        /// <summary>Checks one closed observable layout without modifying user collections.</summary>
        /// <typeparam name="T">The probe's element storage.</typeparam>
        /// <returns>Whether the event fields match subscriptions and removals.</returns>
        private static bool Check<T>()
        {
            var collection = new ObservableCollection<T>();
            if (LuminFieldLayout.Field<object?>(collection, CollectionChanged) is not null ||
                LuminFieldLayout.Field<object?>(collection, PropertyChanged) is not null) return false;
            NotifyCollectionChangedEventHandler changed = OnCollectionChanged;
            PropertyChangedEventHandler property = OnPropertyChanged;
            collection.CollectionChanged += changed;
            ((INotifyPropertyChanged)collection).PropertyChanged += property;
            bool matches = ReferenceEquals(LuminFieldLayout.Field<object?>(collection, CollectionChanged), changed) &&
                ReferenceEquals(LuminFieldLayout.Field<object?>(collection, PropertyChanged), property);
            collection.CollectionChanged -= changed;
            ((INotifyPropertyChanged)collection).PropertyChanged -= property;
            return matches && LuminFieldLayout.Field<object?>(collection, CollectionChanged) is null &&
                LuminFieldLayout.Field<object?>(collection, PropertyChanged) is null;
        }

        /// <summary>Receives the probe's collection notification.</summary>
        /// <param name="sender">The probe instance.</param>
        /// <param name="args">The unused event arguments.</param>
        private static void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) { }

        /// <summary>Receives the probe's property notification.</summary>
        /// <param name="sender">The probe instance.</param>
        /// <param name="args">The unused event arguments.</param>
        private static void OnPropertyChanged(object? sender, PropertyChangedEventArgs args) { }
    }
}
