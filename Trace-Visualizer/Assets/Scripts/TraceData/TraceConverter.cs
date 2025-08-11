using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;
using celerity.visualizer.tracedata;//.rawtrace;

namespace celerity.visualizer.tracedata
{
	/// <summary>
	/// System for converting the raw tracedata from the protobuf format into the internal data structure formats.
	/// </summary>
	public class TraceConverter
	{
		private const bool DEBUG = true;

		private const bool WarnForMissingReferences = false;

		public static Trace ConvertTrace(List<rawtrace.TracePayload> tracePayloads)
		{
			Trace trace = new();

			// first pass - extract tasks and commands, retrieve general info, integrity check
			ExtractTasksAndCommands(tracePayloads, trace);
			if (DEBUG) Debug.Log("Tasks and Commands extracted.");

			// second pass - set up dependencies
			ExtractDependencies(tracePayloads, trace);
			if (DEBUG) Debug.Log("Dependencies extracted.");

			// third pass - combine time measurements from all nodes (set start/end times considering startpoint offsets)
			ExtractTimes(tracePayloads, trace);
			if (DEBUG) Debug.Log("Times extracted.");

			// fourth pass - compute data dependencies between Push/AwaitPush commmands
			ComputeDataDependencies(trace);
			if (DEBUG) Debug.Log("Data Depedencies computed.");

			return trace;
		}

		private static void ExtractTasksAndCommands(List<rawtrace.TracePayload> tracePayloads, Trace trace)
		{
			// pre-reseverve space in Tasks dict
			int taskCount = tracePayloads.Sum(x => x.Tasks.Count);
			trace.Tasks.EnsureCapacity(taskCount);

			// pre-reserve space in Commands dict
			int commandCount = tracePayloads.Sum(x => x.Commands.Count);
			trace.Commands.EnsureCapacity(commandCount);

			// Debug.Log("Task Count: " + taskCount + ", Command Count: " +  commandCount);

			// iterate over all trace payloads to combine into one Trace
			foreach (rawtrace.TracePayload payload in tracePayloads)
			{
				// combine RunIds
				if (payload.RunId != null) // contains a runID?
				{	
					if (trace.RunId != null) // already have a runID
					{
						if (trace.RunId != payload.RunId)
						{
							if (DEBUG)
								Debug.Log("mismatched runID: " + payload.RunId + " vs. " + trace.RunId);
						}
					}
					else // no runID yet -> use this one
					{
						trace.RunId = payload.RunId;
					}
				}

				// combine RunInfo
				if (payload.RunInfo != null)
				{
					// sanity check that all payloads have a valid reference point
					// gets checked again in pass 3, but this is early exit point
					if (payload.RunInfo.ReferenceTimePoint <= 0)
					{
						throw new ArgumentException("Missing ReferenceTimePoint for trace");
					}

					// get executable name
					if (trace.ExecutableName == null && !string.IsNullOrEmpty(payload.RunInfo.ExecutableName))
					{
						trace.ExecutableName = payload.RunInfo.ExecutableName;
					}

					// get executable arguments
					if (trace.ExecutableArgs == null && payload.RunInfo.ExecutableArgs != null && payload.RunInfo.ExecutableArgs.Count > 0)
					{
						trace.ExecutableArgs = payload.RunInfo.ExecutableArgs;
					}
				}

				// extract tasks
				if (payload.Tasks != null)
				{
					foreach (rawtrace.Task rawTask in payload.Tasks)
					{
						trace.Tasks.Add(rawTask.Id, new tracedata.Task(rawTask.Id, rawTask.Name, (ExecutionTarget)rawTask.Target));
					}
				}

				// extract commands
				if (payload.Commands != null)
				{
					foreach (rawtrace.Command command in payload.Commands)
					{
						ExecutionRange executionRange = null;
						if (command.ExecutionRange != null)
						{
							Box3DToVectors(command.ExecutionRange, out Vector3Int min, out Vector3Int max);
							executionRange = new ExecutionRange(min, max);
						}

						ComputeNode otherNode = (command.Type == rawtrace.CommandType.Push || command.Type == rawtrace.CommandType.AwaitPush || command.Type == rawtrace.CommandType.Sync) ? trace.GetOrCreateComputeNode(command.OtherNodeId) : null;

						trace.Commands.Add(command.Id, new tracedata.Command(
							command.Id,
							trace.GetOrCreateComputeNode(command.NodeId),
							ConvertCommandType(command.Type),
							trace.Tasks.ContainsKey(command.TaskId) ? trace.Tasks[command.TaskId] : null,
							executionRange,
							otherNode
							));

						// add buffer accesses
						if (command.BufferAccesses != null)
						{
							// pre-reserve space in buffer accesses list
							trace.Commands[command.Id].BufferAccesses.Capacity = command.BufferAccesses.Count;

							foreach (rawtrace.BufferAccess access in command.BufferAccesses)
							{
								Box3DToVectors(access.Range, out Vector3Int min, out Vector3Int max);
								var bAccess = trace.GetBufferAccess(access.BufferId, (AccessMode)access.Mode, min, max);
								trace.Commands[command.Id].BufferAccesses.Add(bAccess);
							}
						}


					}
				}
			}
		}

		private static CommandType ConvertCommandType(rawtrace.CommandType oldType)
		{
			switch(oldType)
			{
				case rawtrace.CommandType.Nop:
					return CommandType.Nop;
				case rawtrace.CommandType.Horizon:
					return CommandType.Horizon;
				case rawtrace.CommandType.Task:
					return CommandType.Task;
				case rawtrace.CommandType.Push:
					return CommandType.Push;
				case rawtrace.CommandType.AwaitPush:
					return CommandType.AwaitPush;
				case rawtrace.CommandType.Shutdown:
					return CommandType.Shutdown;
				case rawtrace.CommandType.Sync:
					return CommandType.Sync;
				default:
					Debug.LogError("invalid command type to convert: " + oldType);
					return CommandType.Nop;
			}
		}

		private static void ExtractDependencies(List<rawtrace.TracePayload> tracePayloads, Trace trace)
		{
			int commandDepCount = 0;

			foreach (rawtrace.TracePayload payload in tracePayloads)
			{
				// setup task dependencies
				if (payload.Tasks != null)
				{
					foreach (rawtrace.Task task in payload.Tasks)
					{
						foreach (var predecessor in task.Predecessors)
						{
							if (!trace.Tasks.ContainsKey(predecessor.Id))
							{
								if (WarnForMissingReferences)
#pragma warning disable CS0162 // Unreachable code detected
									Debug.LogWarning("Task dependency to unknown target task '" + predecessor.Id + "'");
#pragma warning restore CS0162 // Unreachable code detected
								continue;
							}
							Task origin = trace.Tasks[task.Id];
							Task target = trace.Tasks[predecessor.Id];
							Dependency<Task> dependency = new(origin, target, (DependencyKind)predecessor.Kind);
							origin.Predecessors.Add(dependency);
							target.Successors.Add(dependency);
						}
					}
				}

				// setup command dependencies
				if (payload.Commands != null)
				{
					foreach (rawtrace.Command command in payload.Commands)
					{
						foreach (var predecessor in command.Predecessors)
						{
							if (!trace.Commands.ContainsKey(predecessor.Id))
							{
								if (WarnForMissingReferences)
#pragma warning disable CS0162 // Unreachable code detected
									Debug.LogWarning("Command dependency to unknown target command '" + predecessor.Id + "' for command id " + command.Id);
#pragma warning restore CS0162 // Unreachable code detected
								continue;
							}
							Command origin = trace.Commands[command.Id];
							Command target = trace.Commands[predecessor.Id];
							Dependency<Command> dependency = new(origin, target, (DependencyKind)predecessor.Kind);
							origin.Predecessors.Add(dependency);
							target.Successors.Add(dependency);
							commandDepCount++;
						}
					}
				}
			}

			if (DEBUG) Debug.Log($"Extracted {commandDepCount} command dependencies.");
		}


		/// <summary>
		/// Extracts the start and end times for all commands and normalizes their times to the reference point of compute node 0.
		/// </summary>
		private static void ExtractTimes(List<rawtrace.TracePayload> rawTracePayloads, Trace trace)
		{
			// might have trace payloads without a referenceTimePoint - do a pre-pass over all payloads and extract the reference time for each node
			Dictionary<ulong, ulong> referenceTimes = new();
			
			foreach (rawtrace.TracePayload rawTracePayload in rawTracePayloads)
			{
				if (rawTracePayload.RunInfo == null)
					continue;

				ulong nodeId = rawTracePayload.SourceNodeId;
				ulong refTime = rawTracePayload.RunInfo.ReferenceTimePoint;

				if (!referenceTimes.ContainsKey(nodeId))
				{
					referenceTimes.Add(nodeId, refTime);
				}
				else
				{
					Debug.LogWarning("Found multiple referenceTimePoints for node " + nodeId + ": " + referenceTimes[nodeId] + " and " + refTime);
				}
			}

			// check if we have a refTime for each node
			foreach (ulong id in trace.ComputeNodes.Keys)
			{
				if (!referenceTimes.ContainsKey(id))
				{
					Debug.LogError("Missing referenceTimePoint for node " + id);
					throw new ArgumentException("Missing referenceTimePoint for node " + id);
				}
			}

			ulong minStart = ulong.MaxValue;
			ulong maxEnd = ulong.MinValue;

			// get job times and associate them with the commands
			foreach (rawtrace.TracePayload rawTracePayload in rawTracePayloads)
			{
				ulong nodeId = rawTracePayload.SourceNodeId;
				ulong refTime = referenceTimes[nodeId];

				// normalize to the referenceTimePoint of node 0
				ulong timeOffset = refTime - referenceTimes[0];
				//Debug.Log("nodeID " + nodeId + " timeOffset " + timeOffset);

				foreach (rawtrace.Job job in rawTracePayload.Jobs)
				{
					if (!trace.Commands.ContainsKey(job.CommandId))
					{
						Debug.LogError("Job CommandId is missing in trace! " + job.CommandId);
						continue;
					}

					Command command = trace.Commands[job.CommandId];

					command.StartTime = job.StartedAt + timeOffset;
					command.EndTime = job.FinishedAt + timeOffset;

					if (command.StartTime < minStart) minStart = command.StartTime;
					if (command.EndTime > maxEnd) maxEnd = command.EndTime;
				}
			}

			trace.MinStartTime = minStart;
			trace.MaxEndTime = maxEnd;
			trace.Duration = maxEnd - minStart;
		}

		/// <summary>
		/// Computes the data dependencies between Push/AwaitPush commands in the trace.
		/// </summary>
		/// <exception cref="InvalidOperationException">If any integrity checks fail during the computation, indicating a corrupted or incomplete trace.</exception>
		private static void ComputeDataDependencies(Trace trace)
		{
			/*
			Data dependencies are computed using the following algorithm:
			- Create a map of all Push commands based on their BufferAccesses
				since multiple Push commands can have the same BufferAcces over the course of the trace the value has to be a list of commands
			- Go through all AwaitPush commands (in arbitrary order)
				- get matching list of Push commands
				- find Push command with EndTime closest to the AwaitPush command's EndTime
				- check if both commands match the computeNode constellation (otherNode = own and vice-versa)
				- create data dependency, add to both as corresponding predecessor / successor
			*/

			Dictionary<BufferAccess, List<Command>> bufferAccessMap = new();

			// go through all Push commands in the trace and sort them into the bufferAccessMap
			foreach (Command pushCommand in trace.Commands.Values.Where(x => x.CommandType == CommandType.Push))
			{
				if (pushCommand.BufferAccesses.Count == 0)
					throw new InvalidOperationException("Push command has no buffer accesses!");
				else if (pushCommand.BufferAccesses.Count > 1)
					throw new InvalidOperationException("Push command has more than 1 buffer accesses!");

				BufferAccess ba = pushCommand.BufferAccesses[0];

				// create new entry list if not yet existing
				if (!bufferAccessMap.ContainsKey(ba))
					bufferAccessMap.Add(ba, new List<Command>());

				// add command to list
				bufferAccessMap[ba].Add(pushCommand);
			}

			int depCount = 0;
			// go through all AwaitPush command in the trace and match them to Push commands
			foreach (Command awaitPushCommand in trace.Commands.Values.Where(x => x.CommandType == CommandType.AwaitPush))
			{
				if (awaitPushCommand.BufferAccesses.Count == 0)
					throw new InvalidOperationException("AwaitPush command has no buffer accesses!");
				else if (awaitPushCommand.BufferAccesses.Count > 1)
					throw new InvalidOperationException("AwaitPush command has more than 1 buffer accesses!");

				BufferAccess ba = awaitPushCommand.BufferAccesses[0];
				
				// get corresponding push commands
				if (!bufferAccessMap.TryGetValue(ba, out List<Command> commsInMap))
					throw new InvalidOperationException("No corresponding Push commands found in bufferAccessMap!");

				// find Push command with minimum deviation from AwaitPush EndTime
				ulong awaitPushEndTime = awaitPushCommand.EndTime;
				int minDeviationIndex = -1;
				ulong minDeviation = ulong.MaxValue;
				for (int i = 0; i < commsInMap.Count; i++)
				{
					// check for correct constellation, if not skip
					if (awaitPushCommand.OtherNode != commsInMap[i].ComputeNode || awaitPushCommand.ComputeNode != commsInMap[i].OtherNode)
						continue;

					ulong deviation = awaitPushEndTime > commsInMap[i].EndTime ? awaitPushEndTime - commsInMap[i].EndTime : commsInMap[i].EndTime - awaitPushEndTime;
					if (deviation < minDeviation)
					{
						minDeviationIndex = i;
						minDeviation = deviation;
					}
				}
				if (minDeviationIndex < 0)
					throw new InvalidOperationException("Failed to find Push command with minimum deviation!");

				// this is the closest matching Push command with a correct computeNode constellation, as good as it gets
				Command pushCommand = commsInMap[minDeviationIndex];

				// create new data dependency
				Dependency<Command> dataDep = new(awaitPushCommand, pushCommand, DependencyKind.DataDep);
				awaitPushCommand.Predecessors.Add(dataDep);
				pushCommand.Successors.Add(dataDep);

				depCount++;
			}

			if (DEBUG) Debug.Log($"Added {depCount} data dependencies.");
		}



		private static void Box3DToVectors(rawtrace.Box3D input, out Vector3Int min, out Vector3Int max)
		{
			min = new Vector3Int((int)input.Min0, (int)input.Min1, (int)input.Min2);
			max = new Vector3Int((int)input.Max0, (int)input.Max1, (int)input.Max2);
		}

	}
}
