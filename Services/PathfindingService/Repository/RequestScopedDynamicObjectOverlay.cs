using Microsoft.Extensions.Logging;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PathfindingService.Repository;

public interface IDynamicObjectOverlayRegistry
{
    bool RegisterObject(ulong guid, uint entry, uint displayId, uint mapId, float scale);
    void UpdatePosition(ulong guid, float x, float y, float z, float orientation, uint goState);
    void UnregisterObject(ulong guid);
}

public sealed class NativeDynamicObjectOverlayRegistry : IDynamicObjectOverlayRegistry
{
    private const string DllName = "Navigation.dll";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "RegisterDynamicObject")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool RegisterDynamicObjectNative(
        ulong guid,
        uint entry,
        uint displayId,
        uint mapId,
        float scale);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "UpdateDynamicObjectPosition")]
    private static extern void UpdateDynamicObjectPositionNative(
        ulong guid,
        float x,
        float y,
        float z,
        float orientation,
        uint goState);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "UnregisterDynamicObject")]
    private static extern void UnregisterDynamicObjectNative(ulong guid);

    public bool RegisterObject(ulong guid, uint entry, uint displayId, uint mapId, float scale)
        => RegisterDynamicObjectNative(guid, entry, displayId, mapId, scale);

    public void UpdatePosition(ulong guid, float x, float y, float z, float orientation, uint goState)
        => UpdateDynamicObjectPositionNative(guid, x, y, z, orientation, goState);

    public void UnregisterObject(ulong guid)
        => UnregisterDynamicObjectNative(guid);
}

public readonly record struct RequestScopedDynamicObjectOverlaySummary(
    int RequestedCount,
    int RegisteredCount,
    int FilteredCount,
    IReadOnlyList<uint> RegisteredDisplayIds);

public readonly record struct OverlayExecutionResult<T>(
    T Value,
    RequestScopedDynamicObjectOverlaySummary Summary);

public sealed class RequestScopedDynamicObjectOverlay(IDynamicObjectOverlayRegistry registry)
{
    private const ulong OverlayGuidPrefix = 0xA000000000000000UL;

    private readonly IDynamicObjectOverlayRegistry _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    private readonly object _registryGate = new();
    private bool _operationActive;
    private int _pendingPriorityOperations;
    private long _nextRequestId;

    public T ExecuteExclusive<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        EnterSharedOperation();
        try
        {
            return action();
        }
        finally
        {
            ExitOperation();
        }
    }

    public OverlayExecutionResult<T> ExecuteWithOverlay<T>(
        uint mapId,
        IReadOnlyList<DynamicObjectProto> nearbyObjects,
        Func<T> action,
        ILogger? logger = null,
        string operationName = "request")
    {
        ArgumentNullException.ThrowIfNull(nearbyObjects);
        ArgumentNullException.ThrowIfNull(action);

        EnterPriorityOperation();
        try
        {
            if (nearbyObjects.Count == 0)
            {
                return new OverlayExecutionResult<T>(
                    action(),
                    new RequestScopedDynamicObjectOverlaySummary(
                        RequestedCount: 0,
                        RegisteredCount: 0,
                        FilteredCount: 0,
                        RegisteredDisplayIds: Array.Empty<uint>()));
            }
            var requestId = Interlocked.Increment(ref _nextRequestId);
            var registeredGuids = new List<ulong>(nearbyObjects.Count);
            var registeredDisplayIds = new List<uint>(nearbyObjects.Count);
            var filteredCount = 0;

            for (var i = 0; i < nearbyObjects.Count; i++)
            {
                if (!TryCreateRegistration(requestId, i, mapId, nearbyObjects[i], out var registration))
                {
                    filteredCount++;
                    continue;
                }

                try
                {
                    if (!_registry.RegisterObject(registration.Guid, 0, registration.DisplayId, registration.MapId, registration.Scale))
                    {
                        filteredCount++;
                        continue;
                    }

                    _registry.UpdatePosition(
                        registration.Guid,
                        registration.X,
                        registration.Y,
                        registration.Z,
                        registration.Orientation,
                        registration.GoState);

                    registeredGuids.Add(registration.Guid);
                    registeredDisplayIds.Add(registration.DisplayId);
                }
                catch
                {
                    filteredCount++;
                }
            }

            var summary = new RequestScopedDynamicObjectOverlaySummary(
                RequestedCount: nearbyObjects.Count,
                RegisteredCount: registeredGuids.Count,
                FilteredCount: filteredCount,
                RegisteredDisplayIds: registeredDisplayIds.AsReadOnly());

            if (summary.RequestedCount > 0)
            {
                logger?.LogDebug(
                    "[DynOverlay] op={Operation} map={MapId} requested={Requested} registered={Registered} filtered={Filtered} displayIds=[{DisplayIds}]",
                    operationName,
                    mapId,
                    summary.RequestedCount,
                    summary.RegisteredCount,
                    summary.FilteredCount,
                    summary.RegisteredDisplayIds.Count > 0 ? string.Join(",", summary.RegisteredDisplayIds) : string.Empty);
            }

            try
            {
                return new OverlayExecutionResult<T>(action(), summary);
            }
            finally
            {
                foreach (var guid in registeredGuids)
                {
                    try
                    {
                        _registry.UnregisterObject(guid);
                    }
                    catch
                    {
                    }
                }
            }
        }
        finally
        {
            ExitOperation();
        }
    }

    private void EnterSharedOperation()
    {
        lock (_registryGate)
        {
            while (_operationActive || _pendingPriorityOperations > 0)
            {
                Monitor.Wait(_registryGate);
            }

            _operationActive = true;
        }
    }

    private void EnterPriorityOperation()
    {
        lock (_registryGate)
        {
            _pendingPriorityOperations++;
            try
            {
                while (_operationActive)
                {
                    Monitor.Wait(_registryGate);
                }

                _operationActive = true;
            }
            finally
            {
                _pendingPriorityOperations--;
            }
        }
    }

    private void ExitOperation()
    {
        lock (_registryGate)
        {
            _operationActive = false;
            Monitor.PulseAll(_registryGate);
        }
    }

    private static bool TryCreateRegistration(
        long requestId,
        int index,
        uint mapId,
        DynamicObjectProto proto,
        out OverlayRegistration registration)
    {
        registration = default;
        if (proto.DisplayId == 0
            || !float.IsFinite(proto.X)
            || !float.IsFinite(proto.Y)
            || !float.IsFinite(proto.Z)
            || !float.IsFinite(proto.Orientation))
        {
            return false;
        }

        registration = new OverlayRegistration(
            Guid: CreateSyntheticGuid(requestId, index),
            DisplayId: proto.DisplayId,
            MapId: mapId,
            X: proto.X,
            Y: proto.Y,
            Z: proto.Z,
            Orientation: proto.Orientation,
            Scale: proto.Scale > 0f && float.IsFinite(proto.Scale) ? proto.Scale : 1f,
            GoState: proto.GoState);
        return true;
    }

    private static ulong CreateSyntheticGuid(long requestId, int index)
    {
        var requestBits = ((ulong)requestId & 0x00000FFFFFFFFFFFUL) << 16;
        var indexBits = (ulong)(index & 0xFFFF);
        return OverlayGuidPrefix | requestBits | indexBits;
    }

    private readonly record struct OverlayRegistration(
        ulong Guid,
        uint DisplayId,
        uint MapId,
        float X,
        float Y,
        float Z,
        float Orientation,
        float Scale,
        uint GoState);
}
