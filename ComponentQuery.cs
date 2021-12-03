using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Utility
{
	/// <summary>
	/// Specifies a range of nodes relative to a reference node in a tree structure.
	/// </summary>
	[System.Flags]
	public enum TreeRange
	{
		/// <summary>
		/// Reference node
		/// </summary>
		Self = 1,

		/// <summary>
		/// Siblings of the reference node
		/// </summary>
		Siblings = 2,

		/// <summary>
		/// Immediate parent of the reference node
		/// </summary>
		Parent = 4,

		/// <summary>
		/// Ancestors of the reference node
		/// </summary>
		GrandParents = 8,

		/// <summary>
		/// Immediate children of the reference node
		/// </summary>
		Children = 16,

		/// <summary>
		/// Descendants of the reference node
		/// </summary>
		GrandChildren = 32
	}

	/// <summary>
	/// Groups the component buffers used while querying components.
	/// </summary>
	public readonly ref struct QueryBufferPair
	{
		/// <summary>
		/// A buffer for storing the results
		/// </summary>
		public readonly List<Component> MainBuffer;

		/// <summary>
		/// A buffer for computing the results
		/// </summary>
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
		/// Origin of the query
		/// </summary>
		public readonly GameObject Target;

		/// <summary>
		/// Range of the query relative to the origin
		/// </summary>
		public readonly TreeRange Range;

		/// <summary>
		/// Maximum number of successful queries allowed in the range
		/// </summary>
		public readonly int Capacity;

		/// <summary>
		/// Constructs a new query.
		/// </summary>
		/// <param name="target">Origin of the query</param>
		/// <param name="range">Range of the query relative to the origin</param>
		/// <param name="capacity">Maximum number of successful queries allowed in the range</param>
		/// <inheritdoc cref="ComponentQuery{T}"/>
		public ComponentQuery(GameObject target, TreeRange range, int capacity)
		{
			Target = target;
			Range = range;
			Capacity = capacity;
		}

		/// <summary>
		/// Retrieves the results of the query as an array.
		/// </summary>
		/// <returns>Query results</returns>
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
		/// <returns>Query results enumerator</returns>
		public Enumerator GetEnumerator()
		{
			int startIndex = Utils.MainComponentBuffer.Count;
			Target.GetComponents<T>(Range, new QueryBufferPair(Utils.MainComponentBuffer, Utils.TempComponentBuffer), Capacity);

			return new Enumerator(startIndex, Utils.MainComponentBuffer.Count - startIndex);
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>
		/// Iterates the matched components in a query.
		/// </summary>
		/// <remarks>Please avoid copying these enumerators. Due to the usage of a shared backing buffer, disposal of one copy of the enumerator, affects all other copies and results in unexpected behaviours.</remarks>
		public struct Enumerator : IEnumerator<T>
		{
			private int _index; // Current index
			private readonly int _StartIndex; // The index of the first matched component in the shared buffer

			/// <summary>
			/// Number of components
			/// </summary>
			public readonly int Length;

			public Enumerator(int listStartIndex, int length)
			{
				_StartIndex = listStartIndex;
				_index = -1;
				Length = length;
			}

			/// <summary>
			/// Current component
			/// </summary>
			public T Current => Utils.MainComponentBuffer[_StartIndex + _index] as T;
			object IEnumerator.Current => Current;

			/// <summary>
			/// Advances to the next component.
			/// </summary>
			/// <returns>True if any more component is available, false otherwise</returns>
			public bool MoveNext() => ++_index < Length;

			/// <summary>
			/// Resets to the initial state for another iteration.
			/// </summary>
			public void Reset() => _index = -1;

			/// <summary>
			/// Clears the shared backing buffer.
			/// </summary>
			/// <remarks>Failure to call this method will result in misbehaviours and unexpected results.</remarks>
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
		/// An small component buffer used to store temporary results.
		/// </summary>
		public static readonly List<Component> TempComponentBuffer = new List<Component>(8); // A buffer for holding temporary data from component query processings.


		/// <inheritdoc cref="GetComponent{T}(GameObject, TreeRange)"/>
		public static T GetComponent<T>(this Component target, TreeRange range) where T : class => GetComponent<T>(target.gameObject, range);

		/// <summary>
		/// Retrieves the first component matching the query.
		/// </summary>
		/// <inheritdoc cref="ComponentQuery{T}.ComponentQuery(GameObject, TreeRange, int)"/>
		/// <returns>The matched component if any, null otherwise</returns>
		public static T GetComponent<T>(this GameObject target, TreeRange range) where T : class
		{
			using (var enumerator = new ComponentQuery<T>(target, range, 1).GetEnumerator())
			{
				return enumerator.MoveNext() ? enumerator.Current : null;
			}
		}

		/// <inheritdoc cref="GetComponents{T}(GameObject, TreeRange, int)"/>
		public static ComponentQuery<T> GetComponents<T>(this Component target, TreeRange range, int capacity = int.MaxValue) where T : class => GetComponents<T>(target.gameObject, range, capacity);

		/// <inheritdoc cref="ComponentQuery{T}.ComponentQuery(GameObject, TreeRange, int)"/>
		public static ComponentQuery<T> GetComponents<T>(this GameObject target, TreeRange range, int capacity = int.MaxValue) where T : class => new ComponentQuery<T>(target, range, capacity);

		/// <summary>
		/// Performs a component query.
		/// </summary>
		/// <param name="buffers">The buffers to use for storing the results</param>
		/// <inheritdoc cref="ComponentQuery{T}.ComponentQuery(GameObject, TreeRange, int)"/>
		public static void GetComponents<T>(this GameObject target, TreeRange range, in QueryBufferPair buffers, int capacity = int.MaxValue) where T : class
		{
			Transform current = target.transform, parent = current.parent;

			if (range.HasFlag(TreeRange.Self))
			{
				Query(current, ref capacity, in buffers);
			}

			if (!ReferenceEquals(parent, null))
			{
				if (range.HasFlag(TreeRange.Siblings))
				{
					for (int i = 0, len = parent.childCount; i < len && capacity > 0; i++)
					{
						Transform child = parent.GetChild(i);

						if (!ReferenceEquals(child, current))
						{
							Query(child, ref capacity, in buffers);
						}
					}
				}

				if (range.HasFlag(TreeRange.Parent))
				{
					Query(parent, ref capacity, in buffers);
				}

				if (range.HasFlag(TreeRange.GrandParents))
				{
					QueryParents(parent, ref capacity, in buffers);
				}
			}

			if (range.HasFlag(TreeRange.Children) || range.HasFlag(TreeRange.GrandChildren))
			{
				for (int i = 0, len = current.childCount; i < len && capacity > 0; i++)
				{
					Transform child = current.GetChild(i);

					if (range.HasFlag(TreeRange.Children))
					{
						Query(child, ref capacity, in buffers);
					}

					if (range.HasFlag(TreeRange.GrandChildren))
					{
						for (int j = 0, childLen = child.childCount; j < childLen && capacity > 0; j++)
						{
							QueryDepthRecursive(child.GetChild(j), ref capacity, in buffers);
						}
					}
				}
			}


			static void Query(Transform target, ref int capacity, in QueryBufferPair buffers)
			{
				if (capacity > 0)
				{
					target.GetComponents(buffers.TempBuffer);
					bool querySucceeded = false;

					for (int i = 0, len = buffers.TempBuffer.Count; i < len; i++)
					{
						Component component = buffers.TempBuffer[i];

						if (component is T)
						{
							buffers.MainBuffer.Add(component);
							querySucceeded = true;
						}
					}

					if (querySucceeded)
					{
						capacity--;
					}
				}
			}

			static void QueryParents(Transform target, ref int capacity, in QueryBufferPair buffers)
			{
				while (!ReferenceEquals(target = target.parent, null) && capacity > 0)
				{
					Query(target, ref capacity, in buffers);
				}
			}

			static void QueryDepthRecursive(Transform target, ref int capacity, in QueryBufferPair buffers)
			{
				Query(target, ref capacity, in buffers);

				for (int i = 0, len = target.childCount; i < len && capacity > 0; i++)
				{
					QueryDepthRecursive(target.GetChild(i), ref capacity, in buffers);
				}
			}
		}
	}
}