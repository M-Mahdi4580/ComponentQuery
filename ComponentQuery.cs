using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Utility
{
    /// <summary>
    /// Specifies a range of nodes in a tree structure.
    /// </summary>
    [System.Flags]
    public enum TreeRange
    {
        Self = 1,
        Siblings = 2,
        Parent = 4,
        GrandParents = 8,
        Children = 16,
        GrandChildren = 32
    }

    /// <summary>
    /// A pair of component buffers used for querying components.
    /// </summary>
    public readonly ref struct QueryBufferPair
    {
        public readonly List<Component> MainBuffer;
        public readonly List<Component> TempBuffer;

        public QueryBufferPair(List<Component> mainBuffer, List<Component> tempBuffer)
        {
            MainBuffer = mainBuffer;
            TempBuffer = tempBuffer;
        }
    }

    /// <summary>
    /// Represents a enumerable query of components.
    /// </summary>
    /// <typeparam name="T">Type of the components to query for</typeparam>
    public readonly struct ComponentQuery<T> : IEnumerable<T> where T : class
    {
        /// <summary>
        /// The object to query for components.
        /// </summary>
        public readonly GameObject Target;

        /// <summary>
        /// Range of the query.
        /// </summary>
        public readonly TreeRange Range;

        /// <summary>
        /// Maximum number of components to query for.
        /// </summary>
        public readonly int Capacity;

        public ComponentQuery(GameObject target, TreeRange range, int capacity)
        {
            Target = target;
            Range = range;
            Capacity = capacity;
        }

        /// <summary>
        /// Retrieves the results of the query as an array.
        /// </summary>
        /// <returns>An array holding the results of the query</returns>
        public T[] ToArray()
        {
            using Enumerator enumerator = GetEnumerator();

            T[] array = new T[enumerator.Length];
            int index = -1;

            while (enumerator.MoveNext())
            {
                array[++index] = enumerator.Current;
            }

            return array;
        }

        /// <summary>
        /// Performs the query.
        /// </summary>
        /// <returns>An enumerator to iterate the results of the query</returns>
        public Enumerator GetEnumerator()
        {
            int startIndex = Utils.MainComponentBuffer.Count;
            Target.GetComponents<T>(Range, new QueryBufferPair(Utils.MainComponentBuffer, Utils.TempComponentBuffer), Capacity);

            return new Enumerator(startIndex, Utils.MainComponentBuffer.Count - startIndex);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// A disposable iterator 
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private int _index;
            private readonly int _StartIndex;
            public readonly int Length;

            public Enumerator(int listStartIndex, int length)
            {
                _StartIndex = listStartIndex;
                _index = -1;
                Length = length;
            }

            public T Current => Utils.MainComponentBuffer[_StartIndex + _index] as T;
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++_index < Length;

            /// <summary>
            /// Resets the iterator to its initial state.
            /// </summary>
            public void Reset() => _index = -1;

            /// <summary>
            /// Clears the backing shared buffer.
            /// </summary>
            public void Dispose() => Utils.MainComponentBuffer.RemoveRange(_StartIndex, Length);
        }
    }


    public static partial class Utils
	{
        /// <summary>
        /// A large component buffer used to store long-term results.
        /// </summary>
        public static readonly List<Component> MainComponentBuffer = new List<Component>(32); // Main buffer for holding results from component queries.
        
        /// <summary>
        /// An small component buffer used to store short-term results.
        /// </summary>
        public static readonly List<Component> TempComponentBuffer = new List<Component>(8); // A buffer for holding temporary data from component query processings.

        /// <summary>
        /// Retrieves the first component matching the query.
        /// </summary>
        /// <typeparam name="T">Type of the component to query for</typeparam>
        /// <param name="obj">The object to query for components</param>
        /// <param name="range">Range of the query</param>
        /// <returns>First matching component</returns>
        public static T GetComponent<T>(this GameObject obj, TreeRange range) where T : class
        {
            using (var enumerator = new ComponentQuery<T>(obj, range, 1).GetEnumerator())
            {
                return enumerator.MoveNext() ? enumerator.Current : null;
            }
        }

        /// <summary>
        /// Constructs a component query.
        /// </summary>
        /// <param name="capacity">Maximum number of components to query for</param>
        /// <returns>The component query</returns>
        /// <inheritdoc cref="GetComponent{T}(GameObject, TreeRange)"/>
        public static ComponentQuery<T> GetComponents<T>(this GameObject obj, TreeRange range, int capacity = int.MaxValue) where T : class => new ComponentQuery<T>(obj, range, capacity);
        
        /// <summary>
        /// Performs a component query.
        /// </summary>
        /// <param name="buffers">The buffers used to store the results</param>
        /// <inheritdoc cref="GetComponents{T}(GameObject, TreeRange, int)"/>
        public static void GetComponents<T>(this GameObject obj, TreeRange range, in QueryBufferPair buffers, int capacity = int.MaxValue) where T : class
        {
            Transform transform = obj.transform, parent = transform.parent;

            if (range.HasFlag(TreeRange.Self) && capacity > 0)
            {
                GetComponents(transform, ref capacity, in buffers);
            }

            if (!ReferenceEquals(parent, null))
            {
                if (range.HasFlag(TreeRange.Siblings))
                {
                    for (int i = 0, len = parent.childCount; i < len && capacity > 0; i++)
                    {
                        Transform child = parent.GetChild(i);

                        if (!ReferenceEquals(child, transform))
                        {
                            GetComponents(child, ref capacity, in buffers);
                        }
                    }
                }

                if (range.HasFlag(TreeRange.Parent) && capacity > 0)
                {
                    GetComponents(parent, ref capacity, in buffers);
                }

                if (range.HasFlag(TreeRange.GrandParents))
                {
                    GetComponentsInParents(parent.parent, ref capacity, in buffers);
                }
            }

            if (range.HasFlag(TreeRange.Children) || range.HasFlag(TreeRange.GrandChildren))
            {
                for (int i = 0, len = transform.childCount; i < len && capacity > 0; i++)
                {
                    Transform child = transform.GetChild(i);

                    if (range.HasFlag(TreeRange.Children))
                    {
                        GetComponents(child, ref capacity, in buffers);
                    }

                    if (range.HasFlag(TreeRange.GrandChildren))
                    {
                        for (int j = 0, childLen = child.childCount; j < childLen && capacity > 0; j++)
                        {
                            GetComponentsInChildren(child.GetChild(j), ref capacity, in buffers);
                        }
                    }
                }
            }


            static void GetComponents(Transform transform, ref int capacity, in QueryBufferPair buffers)
            {
                transform.GetComponents(buffers.TempBuffer);

                for (int i = 0, len = buffers.TempBuffer.Count; i < len && capacity > 0; i++)
                {
                    Component component = buffers.TempBuffer[i];

                    if (component is T)
                    {
                        buffers.MainBuffer.Add(component);
                        capacity--;
                    }
                }
            }

            static void GetComponentsInParents(Transform transform, ref int capacity, in QueryBufferPair buffers)
            {
                while (!ReferenceEquals(transform, null) && capacity > 0)
                {
                    GetComponents(transform, ref capacity, in buffers);
                    transform = transform.parent;
                }
            }

            static void GetComponentsInChildren(Transform transform, ref int capacity, in QueryBufferPair buffers)
            {
                GetComponents(transform, ref capacity, in buffers);

                for (int i = 0, len = transform.childCount; i < len && capacity > 0; i++)
                {
                    GetComponentsInChildren(transform.GetChild(i), ref capacity, in buffers);
                }
            }
        }
    }
}