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
		/// The reference node.
		/// </summary>
		Self = 1,

		/// <summary>
		/// Siblings of the reference node.
		/// </summary>
		Siblings = 2,

		/// <summary>
		/// Immediate parent of the reference node.
		/// </summary>
		Parent = 4,

		/// <summary>
		/// Ancestors of the reference node.
		/// </summary>
		GrandParents = 8,

		/// <summary>
		/// Immediate children of the reference node.
		/// </summary>
		Children = 16,

		/// <summary>
		/// Descendants of the reference node.
		/// </summary>
		GrandChildren = 32
	}


	/// <summary>
	/// Represents a enumerable query of components.
	/// </summary>
	/// <typeparam name="T">Type of the components to query for.</typeparam>
	public readonly struct ComponentQuery<T> : IEnumerable<T> where T : class
	{
		private static readonly List<Component> MainBuffer = new List<Component>(32); // Used for storing query results.
		private static readonly List<Component> TempBuffer = new List<Component>(8); // Used for computing query results.


		/// <summary>
		/// Reference node of the query.
		/// </summary>
		public readonly GameObject Node;

		/// <summary>
		/// Range of the query relative to the reference node.
		/// </summary>
		public readonly TreeRange Range;

		/// <summary>
		/// Maximum number of nodes the query can match.
		/// </summary>
		public readonly int RangeCapacity;

		/// <summary>
		/// Maximum number of components each node can match.
		/// </summary>
		public readonly int NodeCapacity;

		/// <summary>
		/// Initializes a new query with the given parameters.
		/// </summary>
		/// <param name="node">Reference node of the query.</param>
		/// <param name="range">Range of the query relative to the reference node.</param>
		/// <param name="rangeCapacity">Maximum number of nodes the query can match.</param>
		/// <param name="nodeCapacity">Maximum number of components each node can match.</param>
		public ComponentQuery(GameObject node, TreeRange range, int rangeCapacity, int nodeCapacity)
		{
			Node = node;
			Range = range;
			RangeCapacity = rangeCapacity;
			NodeCapacity = nodeCapacity;
		}


		/// <summary>
		/// Retrieves the results of the query as an array.
		/// </summary>
		/// <returns>An array containing the query results.</returns>
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
		/// Retrieves the results of the query.
		/// </summary>
		/// <returns>An enumerator for iterating the results.</returns>
		public Enumerator GetEnumerator()
		{
			int sharedIndex = MainBuffer.Count; // Stores the start index of the query results in the shared buffer.
			int rangeCapacity = RangeCapacity; // Keeps track of the number of matched nodes.

			Transform current = Node.transform;
			Transform parent = current.parent;

			if (Range.HasFlag(TreeRange.Self))
			{
				Query(current, ref rangeCapacity, NodeCapacity);
			}

			if (!ReferenceEquals(parent, null))
			{
				if (Range.HasFlag(TreeRange.Siblings))
				{
					for (int i = 0, len = parent.childCount; i < len && rangeCapacity > 0; i++)
					{
						Transform child = parent.GetChild(i);

						if (!ReferenceEquals(child, current))
						{
							Query(child, ref rangeCapacity, NodeCapacity);
						}
					}
				}

				if (Range.HasFlag(TreeRange.Parent))
				{
					Query(parent, ref rangeCapacity, NodeCapacity);
				}

				if (Range.HasFlag(TreeRange.GrandParents))
				{
					QueryParents(parent, ref rangeCapacity, NodeCapacity);
				}
			}

			if (Range.HasFlag(TreeRange.Children) || Range.HasFlag(TreeRange.GrandChildren))
			{
				for (int i = 0, len = current.childCount; i < len && rangeCapacity > 0; i++)
				{
					Transform child = current.GetChild(i);

					if (Range.HasFlag(TreeRange.Children))
					{
						Query(child, ref rangeCapacity, NodeCapacity);
					}

					if (Range.HasFlag(TreeRange.GrandChildren))
					{
						for (int j = 0, childLen = child.childCount; j < childLen && rangeCapacity > 0; j++)
						{
							QueryChildrenRecursive(child.GetChild(j), ref rangeCapacity, NodeCapacity);
						}
					}
				}
			}

			return new Enumerator(sharedIndex, MainBuffer.Count - sharedIndex);


			static void Query(Transform node, ref int rangeCapacity, int nodeCapacity)
			{
				if (rangeCapacity > 0)
				{
					node.GetComponents(TempBuffer);
					int capacity = nodeCapacity;

					for (int i = 0, len = TempBuffer.Count; i < len && capacity > 0; i++)
					{
						Component component = TempBuffer[i];

						if (component is T)
						{
							MainBuffer.Add(component);
							capacity--;
						}
					}

					if (capacity != nodeCapacity) // If the node is matched
					{
						rangeCapacity--; // Update the range capacity.
					}
				}
			}

			static void QueryParents(Transform node, ref int rangeCapacity, int nodeCapacity)
			{
				while (!ReferenceEquals(node = node.parent, null) && rangeCapacity > 0)
				{
					Query(node, ref rangeCapacity, nodeCapacity);
				}
			}

			static void QueryChildrenRecursive(Transform node, ref int rangeCapacity, int nodeCapacity)
			{
				Query(node, ref rangeCapacity, nodeCapacity);

				for (int i = 0, len = node.childCount; i < len && rangeCapacity > 0; i++)
				{
					QueryChildrenRecursive(node.GetChild(i), ref rangeCapacity, nodeCapacity);
				}
			}
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>
		/// Iterates the matched components of a query.
		/// </summary>
		/// <remarks>Copying these enumerators should be avoided. Due to the usage of a shared backing buffer, disposal of one copy, affects all other copies and results in unexpected behaviours.</remarks>
		public struct Enumerator : IEnumerator<T>
		{
			private readonly int _SharedIndex; // Index of the first matched component in the shared buffer.
			private int _index; // Index of the current matched component.

			/// <summary>
			/// Number of matches.
			/// </summary>
			public readonly int Length;

			internal Enumerator(int sharedIndex, int length)
			{
				_SharedIndex = sharedIndex;
				_index = -1;
				Length = length;
			}

			/// <summary>
			/// Current match.
			/// </summary>
			public T Current => MainBuffer[_SharedIndex + _index] as T;
			object IEnumerator.Current => Current;

			/// <summary>
			/// Advances to the next match.
			/// </summary>
			/// <returns>True if there are any more matches available; false otherwise.</returns>
			public bool MoveNext() => ++_index < Length;

			/// <summary>
			/// Resets to the initial state for another iteration.
			/// </summary>
			public void Reset() => _index = -1;

			/// <summary>
			/// Clears the query results from the shared backing buffer.
			/// </summary>
			/// <remarks>This method should be called consistently to prevent unbounded growth of the shared backing buffer and invalid behaviours.</remarks>
			public void Dispose() => MainBuffer.RemoveRange(_SharedIndex, Length);
		}
	}


	public static partial class Utils
	{
		/// <summary>
		/// Retrieves the first component matching the query.
		/// </summary>
		/// <typeparam name="T">Type of the component to query for.</typeparam>
		/// <returns>The matched component if any; null otherwise.</returns>
		/// <inheritdoc cref="ComponentQuery{T}.ComponentQuery(GameObject, TreeRange, int, int)"/>
		public static T GetComponent<T>(this GameObject node, TreeRange range) where T : class
		{
			using (var enumerator = new ComponentQuery<T>(node, range, 1, 1).GetEnumerator())
			{
				return enumerator.MoveNext() ? enumerator.Current : null;
			}
		}

		/// <inheritdoc cref="GetComponent{T}(GameObject, TreeRange)"/>
		public static T GetComponent<T>(this Component node, TreeRange range) where T : class => GetComponent<T>(node.gameObject, range);

		/// <typeparam name="T">Type of the components to query for.</typeparam>
		/// <inheritdoc cref="ComponentQuery{T}.ComponentQuery(GameObject, TreeRange, int, int)"/>
		public static ComponentQuery<T> GetComponents<T>(this GameObject node, TreeRange range, int rangeCapacity = int.MaxValue, int nodeCapacity = int.MaxValue) where T : class => new ComponentQuery<T>(node, range, rangeCapacity, nodeCapacity);

		/// <inheritdoc cref="GetComponents{T}(GameObject, TreeRange, int, int)"/>
		public static ComponentQuery<T> GetComponents<T>(this Component node, TreeRange range, int rangeCapacity = int.MaxValue, int nodeCapacity = int.MaxValue) where T : class => new ComponentQuery<T>(node.gameObject, range, rangeCapacity, nodeCapacity);
	}
}