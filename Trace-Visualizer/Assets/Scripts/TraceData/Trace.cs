using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#nullable enable

namespace celerity.visualizer.tracedata
{
	public class Trace
	{
		public string? RunId { get; set; }
		public string? ExecutableName { get; set; }
		public List<string>? ExecutableArgs { get; set; }

		/// <summary>
		/// Start time of the earliest recorded <see cref="Command"/> in the trace.
		/// In microseconds since start of the program as given by the reference time of compute node 0.
		/// </summary>
		public ulong MinStartTime { get; set; }

		/// <summary>
		/// End time of the last recorded <see cref="Command"/> in the trace.
		/// In microseconds since start of the program as given by the reference time of compute node 0.
		/// </summary>
		public ulong MaxEndTime { get; set; }

		/// <summary>
		/// Number of microseconds between the <see cref="MinStartTime"/> and <see cref="MaxEndTime"/>.
		/// = Length of the recorded trace.
		/// </summary>
		public ulong Duration { get; set; }

		/// <summary>
		/// Mapping of TaskID to Tasks. Should only be modified during trace conversion.
		/// </summary>
		public Dictionary<ulong, Task> Tasks { get; private set; }

		/// <summary>
		/// Mapping of CommandID to Commands. Should only be modified during trace conversion.
		/// </summary>
		public Dictionary<ulong, Command> Commands { get; private set; }

		/// <summary>
		/// Mapping of BufferID to Buffers. Should never be modified directly since it is fully managed by the <see cref="GetBufferAccess(ulong, AccessMode, Vector3Int, Vector3Int)"/> method.
		/// </summary>
		public Dictionary<ulong, Buffer> Buffers { get; private set; }

		/// <summary>
		/// Mapping of ComputeNodeID to ComputeNodes. Should never be modified directly since it is fully managed by the <see cref="GetOrCreateComputeNode(ulong)"/> method.
		/// </summary>
		public Dictionary<ulong, ComputeNode> ComputeNodes { get; private set; }

		public Trace()
		{
			Tasks = new Dictionary<ulong, Task>();
			Commands = new Dictionary<ulong, Command>();
			Buffers = new Dictionary<ulong, Buffer>();
			ComputeNodes = new Dictionary<ulong, ComputeNode>();
		}

		/// <summary>
		/// Returns a reference to a Compute Node, creating it if it wasn't referenced before.
		/// Should only be used during trace conversion, not at runtime!
		/// </summary>
		public ComputeNode GetOrCreateComputeNode(ulong id)
		{
			if (ComputeNodes.ContainsKey(id))
				return ComputeNodes[id];

			ComputeNode node = new(id);
			ComputeNodes.Add(id, node);
			return node;
		}

		/// <summary>
		/// Generates a new BufferAccess, creating or updating the corresponding buffer with the information from the access.
		/// Should only be used during trace conversion, not at runtime!
		/// </summary>
		public BufferAccess GetBufferAccess(ulong bufferId, AccessMode accessMode, Vector3Int start, Vector3Int end)
		{
			if (!Buffers.ContainsKey(bufferId))
			{
				Buffers.Add(bufferId, new(bufferId, new Vector3Int(0, 0, 0)));
			}

			// update buffer size if new access goes past current size
			Buffer buffer = Buffers[bufferId];
			buffer.Size = Vector3Int.Max(buffer.Size, end);

			return new(buffer, accessMode, start, end);
		}

	}


	public class Task
	{
		public ulong Id { get; set; }
		public string Name { get; set; }
		public ExecutionTarget ExecutionTarget { get; set; }
		public List<Command> Commands { get; private set; }
		public List<Dependency<Task>> Predecessors { get; private set; }
		public List<Dependency<Task>> Successors { get; private set; }

		public Task(ulong id, string name, ExecutionTarget executionTarget)
		{
			Id = id;
			Name = name;
			ExecutionTarget = executionTarget;
			Commands = new List<Command>();
			Predecessors = new List<Dependency<Task>>();
			Successors = new List<Dependency<Task>>();
		}

		public override bool Equals(object? obj)
		{
			return obj is Task task &&
				   Id == task.Id;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Id);
		}
	}

	public class Command
	{
		public ulong Id { get; private set; }
		public ComputeNode ComputeNode { get; private set; }
		public CommandType CommandType { get; private set; }

		/// <summary>
		/// Time in microseconds since start of program, normalized to reference start time of compute node 0.
		/// </summary>
		public ulong StartTime { get; set; }

		/// <summary>
		/// Time in microseconds since start of program, normalized to reference start time of compute node 0.
		/// </summary>
		public ulong EndTime { get; set; }

		public Task? Task { get; private set; }
		public ExecutionRange? ExecutionRange { get; private set; }
		public ComputeNode? OtherNode { get; private set; }

		public List<BufferAccess> BufferAccesses { get; private set; }
		public List<Dependency<Command>> Predecessors { get; private set; }
		public List<Dependency<Command>> Successors { get; private set; }

		public Command(ulong id, ComputeNode node, CommandType commandType, Task? task, ExecutionRange? executionRange, ComputeNode? otherNode)
		{
			Id = id;
			ComputeNode = node;
			CommandType = commandType;
			Task = task;
			ExecutionRange = executionRange;
			BufferAccesses = new List<BufferAccess>();
			Predecessors = new List<Dependency<Command>>();
			Successors = new List<Dependency<Command>>();
			OtherNode = otherNode;

		}

		public override bool Equals(object? obj)
		{
			return obj is Command command &&
				   Id == command.Id;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Id);
		}
	}

	public class Dependency<T>
	{
		public T Origin { get; private set; }
		public T Target { get; private set; }
		public DependencyKind Kind;

		public Dependency(T origin, T target, DependencyKind Kind)
		{
			Origin = origin;
			Target = target;
			this.Kind = Kind;
		}
	}

	/// <summary>
	/// [start, end)
	/// </summary>
	public class ExecutionRange
	{
		public Vector3Int Start { get; set; }
		public Vector3Int End { get; set; }

		public ExecutionRange(Vector3Int start, Vector3Int end)
		{
			Start = start;
			End = end;
		}
	}

	public class BufferAccess
	{
		public Buffer Buffer { get; private set; }
		public AccessMode AccessMode { get; set; }
		public Vector3Int Start { get; set; }
		public Vector3Int End { get; set; }

		/// <summary>
		/// Not to be used directly, managed by the Trace.
		/// </summary>
		/// <param name="buffer"></param>
		/// <param name="accessMode"></param>
		/// <param name="start"></param>
		/// <param name="end"></param>
		public BufferAccess(Buffer buffer, AccessMode accessMode, Vector3Int start, Vector3Int end)
		{
			Buffer = buffer;
			AccessMode = accessMode;
			Start = start;
			End = end;
		}

		/// <summary>
		/// BufferAccesses are equal if everything *excluding* the AccessMode matches.
		/// </summary>
		public override bool Equals(object? obj)
		{
			return obj is BufferAccess access &&
				   EqualityComparer<Buffer>.Default.Equals(Buffer, access.Buffer) &&
				   Start.Equals(access.Start) &&
				   End.Equals(access.End);
		}

		/// <summary>
		/// BufferAccesses have the same HashCode if everything *excluding* the AccessMode matches.
		/// </summary>
		public override int GetHashCode()
		{
			return HashCode.Combine(Buffer, Start, End);
		}

		public override string ToString()
		{
			return $"BufferAccess[BufferID {Buffer.Id}, start {Start}, end {End}]";
		}
	}

	public class Buffer
	{
		public ulong Id { get; set; }
		public Vector3Int Size { get; set; }

		/// <summary>
		/// Not to be used directly, managed by the Trace.
		/// </summary>
		/// <param name="id"></param>
		/// <param name="size"></param>
		public Buffer(ulong id, Vector3Int size)
		{
			Id = id;
			Size = size;
		}

		public override bool Equals(object? obj)
		{
			return obj is Buffer buffer &&
				   Id == buffer.Id;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Id);
		}
	}

	public class ComputeNode
	{
		public ulong Id { get; set; }

		public ComputeNode(ulong id)
		{
			Id = id;
		}

		public override bool Equals(object? obj)
		{
			return obj is ComputeNode node &&
				   Id == node.Id;
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Id);
		}
	}

	public enum DependencyKind
	{
		AntiDep = 0,
		OrderDep = 1,
		TrueDep = 2,
		DataDep = 3,
	}

	public enum AccessMode
	{
		Read = 0,
		Write = 1,
		ReadWrite = 2,
		DiscardWrite = 3,
		DiscardReadWrite = 4,
		Atomic = 5,
	}

	public enum ExecutionTarget
	{
		Device = 0,
		Host = 1,
		None = 2,
	}

	/// <summary>
	/// Command type enum; Set up as a bit field so types can be combined into bit masks for easy filtering.
	/// </summary>
	public enum CommandType
	{
		None =		0b00000000,
		Nop =		0b00000001,
		Horizon =	0b00000010,
		Task =		0b00000100,
		Push =		0b00001000,
		AwaitPush = 0b00010000,
		Shutdown =  0b00100000,
		Sync =		0b01000000,
		All =		0b01111111,
	}
}