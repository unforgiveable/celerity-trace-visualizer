using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using celerity.visualizer.tracedata;
using System;
using Unity.VisualScripting;

namespace celerity.visualizer.timeline
{

	/// <summary>
	/// Data class containing the information for a single timeline chunk.
	/// Keeps track of all compute nodes within the chunk's time slot as well as all chunks containing nodes that overlap this chunk.
	/// </summary>
	public class Chunk
	{
		public ulong ChunkNumber { get; private set; }
		public Dictionary<Command, Node> Nodes { get; private set; }
		public GameObject ChunkObject { get; private set; }
		public List<Chunk> Overlaps { get; private set; }

		public Chunk(ulong chunkNumber, GameObject chunkObject)
		{
			ChunkNumber = chunkNumber;
			ChunkObject = chunkObject;

			Nodes = new();
			Overlaps = new();
		}

		public void AddNode(Node node)
		{
			Nodes.Add(node.Command, node);
		}

		public void AddOverlappingChunk(Chunk chunk)
		{
			if (chunk == this) return;

			if (!Overlaps.Contains(chunk))
			{
				Overlaps.Add(chunk);
			}
		}

		public override bool Equals(object obj)
		{
			return obj is Chunk chunk &&
				   ChunkNumber == chunk.ChunkNumber;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(ChunkNumber);
		}
	}

	/// <summary>
	/// The Chunking System slices the timeline into multiple chunks of equal length.
	/// Acts as a container for the generated chunks as well as the currently shown chunks.
	/// Provides methods for showing/hiding specifc chunks as well as specifc nodes within chunks.
	/// </summary>
	public class ChunkingSystem
	{
		private readonly List<Chunk> _chunks;
		private readonly HashSet<Chunk> _currentShownChunks;

		private readonly ulong _minStartTime;
		private const ulong ChunkTargetSize = 500000; //0.5sec //1000000; //1 sec

		/// <summary>
		/// Creates a new ChunkingSystem.
		/// A new one should be created every time a new timeline is loaded.
		/// </summary>
		/// <param name="duration">Overall duration of the timeline.</param>
		/// <param name="minStartTime">The minimum start time of all nodes in the timeline.</param>
		/// <param name="nodeParent">Reference to the node parent transform object in the scene.</param>
		public ChunkingSystem(ulong duration, ulong minStartTime, Transform nodeParent)
		{
			_chunks = new();
			_minStartTime = minStartTime;

			CreateChunkList(duration, ChunkTargetSize, nodeParent);

			_currentShownChunks = new(_chunks.Count);
		}

		/// <summary>
		/// Returns the transform for the chunk at the given (normalized) time.
		/// </summary>
		public Transform GetChunkTransformForTime(ulong time)
		{
			return GetChunkForTime(time).ChunkObject.transform;
		}

		/// <summary>
		/// Adds a node to the corresponding chunk at the given mid-way time of the node.
		/// </summary>
		/// <param name="node">Node to add to a chunk.</param>
		/// <param name="midTime">Mid-way time of the node (in normalized time).</param>
		public void AddNodeToChunk(Node node, ulong midTime)
		{
			Chunk c = GetChunkForTime(midTime);
			c.AddNode(node);

			// find chunks that get overlapped by this node
			ulong startTime = node.Command.StartTime - _minStartTime;
			ulong endTime = node.Command.EndTime - _minStartTime;

			// TODO: Remove after testing
			//Chunk startChunk = GetChunkForTime(startTime);
			//Chunk endChunk = GetChunkForTime(endTime);
			//int startIdx = _chunks.IndexOf(startChunk);
			//int endIdx = _chunks.IndexOf(endChunk);

			int startIdx = GetChunkIndexForTime(startTime);
			int endIdx = GetChunkIndexForTime(endTime);

			for (int i = startIdx; i <= endIdx; i++)
			{
				_chunks[i].AddOverlappingChunk(c);
			}
		}

		/// <summary>
		/// Shows all chunks in the timeline.
		/// </summary>
		public void ShowAllChunks()
		{
			foreach (Chunk c in _chunks)
			{
				c.ChunkObject.SetActive(true);
			}

			_currentShownChunks.Clear();
			_currentShownChunks.AddRange(_chunks);
		}

		/// <summary>
		/// Enables all nodes in the currently shown chunks. Does NOT override the chunking system if currently only showing specific chunks.
		/// Acts as a reset from the ShowNodesInCurrentChunksByType function.
		/// </summary>
		public void ShowAllNodesInCurrentChunks()
		{
			// TODO remove after testing
			//foreach (Chunk c in _chunks)
			foreach (Chunk c in _currentShownChunks)
			{
				foreach (Node n in c.Nodes.Values)
				{
					n.gameObject.SetActive(true);
				}
			}
		}

		/// <summary>
		/// Shows the nodes in the currently shown chunks if they match the type mask.
		/// </summary>
		/// <param name="typeMask">Bit mask for command types.</param>
		public void ShowNodesInCurrentChunksByType(CommandType typeMask)
		{
			foreach (Chunk c in _currentShownChunks)
			{
				foreach (var kv in c.Nodes)
				{
					if ((kv.Key.CommandType & typeMask) != 0)
					{
						kv.Value.gameObject.SetActive(true);
					}
					else
					{
						kv.Value.gameObject.SetActive(false);
					}
				}
			}
		}

		/// <summary>
		/// Shows the nodes in the currently shown chunks if they're contained in the given nodeSet.
		/// </summary>
		/// <param name="nodeSet">Set of Nodes to show if they're in the currently shown Chunks.</param>
		public void ShowNodesInCurrentChunksBySet(HashSet<Node> nodeSet)
		{
			foreach (Chunk c in _currentShownChunks)
			{
				foreach (var kv in c.Nodes)
				{
					if (nodeSet.Contains(kv.Value))
					{
						kv.Value.gameObject.SetActive(true);
					}
					else
					{
						kv.Value.gameObject.SetActive(false);
					}
				}
			}
		}


		/// <summary>
		/// Computes all chunks with nodes in the given vicinity and hides all others.
		/// </summary>
		/// <param name="timePosition">Time position in microseconds since start of program (minimum of minStartTime).</param>
		/// <param name="vicinitySize">Vicinity size in microseconds.</param>
		public void ShowChunksInVicinity(ulong timePosition, ulong vicinitySize)
		{
			HideAllChunks();

			ulong normalizedTimePosition = timePosition - _minStartTime;

			ulong vicinityStartTime = (vicinitySize >= normalizedTimePosition) ? 0 : normalizedTimePosition - vicinitySize;
			ulong vicinityEndTime = normalizedTimePosition + vicinitySize;

			//Debug.Log("Showing chunks for vicinity of center " + normalizedTimePosition + " with min " + minTime + " and max " + maxTime);

			// Compute the set of chunks that overlap the vicinity
			ComputeChunksInVicinity(_currentShownChunks, vicinityStartTime, vicinityEndTime);

			// show all chunks in the set
			foreach (Chunk c in _currentShownChunks)
			{
				//Debug.Log("Showing chunk " + c.ChunkNumber +" (" + c.TargetChunkStart + " - " + c.TargetChunkEnd + ") - (" + c.EffectiveChunkStart + " - " + c.EffectiveChunkEnd + ")");
				c.ChunkObject.SetActive(true);
			}
		}

		/// <summary>
		/// Computes the set of chunks that overlaps the vicinity given by the vicinityStartTime and vicinityEndTime based on the chunks' start/end times.
		/// </summary>
		/// <param name="vicinityStartTime">Minimum time of the vicinity in normalized time.</param>
		/// <param name="vicinityEndTime">Maximum time of the vicinity in normalized time.</param>
		private void ComputeChunksInVicinity(HashSet<Chunk> currentShownChunks,  ulong vicinityStartTime, ulong vicinityEndTime)
		{
			currentShownChunks.Clear();

			// TODO: Remove after test
			//Chunk startChunk = GetChunkForTime(vicinityStartTime);
			//Chunk endChunk = GetChunkForTime(vicinityEndTime);

			//int startIdx = _chunks.IndexOf(startChunk);
			//int endIdx = _chunks.IndexOf(endChunk);

			int startIdx = GetChunkIndexForTime(vicinityStartTime);
			int endIdx = GetChunkIndexForTime(vicinityEndTime);

			for (int i = startIdx; i <= endIdx; i++)
			{
				// add chunk itself to shown chunks
				currentShownChunks.Add(_chunks[i]);

				// add all chunks that overlap that chunk
				currentShownChunks.AddRange(_chunks[i].Overlaps);
			}

			return;
		}


		/// <summary>
		/// Comptues the target Chunk for a given normalized time.
		/// </summary>
		/// <returns>Reference to the corresponding chunk.</returns>
		private Chunk GetChunkForTime(ulong time)
		{
			int idx = GetChunkIndexForTime(time);
			return _chunks[idx];
		}

		/// <summary>
		/// Computes the target Chunk index for a given normalized time.
		/// </summary>
		/// <returns>The index of the chunk in the _chunks list.</returns>
		/// <exception cref="ArgumentException">Thrown if the time results in an invalid chunk index.</exception>
		private int GetChunkIndexForTime(ulong time)
		{
			int idx = (int)(time / ChunkTargetSize);

			if (idx < 0 || idx >= _chunks.Count)
				throw new ArgumentException("Time " + time + " results in invalid chunk index (" + idx + ")!");

			return idx;
		}

		/// <summary>
		/// Initializes the chunk list with new <see cref="Chunk"/> instances to fit the range of the trace given the <paramref name="chunkTargetSize"/>.
		/// </summary>
		/// <param name="duration">Total duration of the trace.</param>
		/// <param name="chunkTargetSize">Target size for chunks.</param>
		private void CreateChunkList(ulong duration, ulong chunkTargetSize, Transform nodeParent)
		{
			Debug.Log("Timeline duration: " + duration / 1000000 + " sec.");

			// compute number of chunks needed
			ulong numChunks = duration / chunkTargetSize;
			if (duration % chunkTargetSize != 0)
				numChunks++;

			_chunks.Capacity = (int)numChunks;

			for (ulong i = 0; i < numChunks; i++)
			{
				// create game object
				GameObject chunkObject = new();
				chunkObject.transform.SetParent(nodeParent, false);
				chunkObject.name = "Chunk " + i;

				// create chunk instance
				Chunk c = new(i, chunkObject);

				_chunks.Add(c);
			}

			Debug.Log("Created " + numChunks + " chunks.");
		}

		/// <summary>
		/// Hides all chunk objects. Acts as a clear reset.
		/// </summary>
		private void HideAllChunks()
		{
			foreach (Chunk c in _chunks)
			{
				c.ChunkObject.SetActive(false);
			}

			_currentShownChunks.Clear();
		}

	}
}