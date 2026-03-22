using Communication;
using WoWStateManager.Clients;
using WoWStateManager.Listeners;
using WoWStateManager.Logging;
using WoWStateManager.Repository;
using WoWStateManager.Settings;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Runtime.InteropServices;
using static WinProcessImports;
using System.Threading.Tasks;
using System;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace WoWStateManager
{
    public partial class StateManagerWorker
    {

        private void OnWorldStateUpdate(AsyncRequest dataMessage)
        {
            _logger.LogDebug($"Received world state update message with ID {dataMessage.Id}, Case={dataMessage.ParameterCase}.");

            var response = new StateChangeResponse();

            switch (dataMessage.ParameterCase)
            {
                case AsyncRequest.ParameterOneofCase.StateChange:
                    HandleStateChange(dataMessage.StateChange);
                    break;

                case AsyncRequest.ParameterOneofCase.SnapshotQuery:
                    HandleSnapshotQuery(dataMessage.SnapshotQuery, response);
                    break;

                case AsyncRequest.ParameterOneofCase.ActionForward:
                    HandleActionForward(dataMessage.ActionForward, response);
                    break;

                default:
                    _logger.LogWarning($"Unknown request type: {dataMessage.ParameterCase}");
                    break;
            }

            _worldStateManagerSocketListener.SendMessageToClient(dataMessage.Id, response);
            _logger.LogDebug($"StateChangeResponse dispatched to {dataMessage.Id}.");
        }


        private void HandleStateChange(StateChangeRequest stateChange)
        {
            string? parameterDetails = null;
            if (stateChange.RequestParameter != null)
            {
                RequestParameter param = stateChange.RequestParameter;
                parameterDetails = param.ParameterCase switch
                {
                    RequestParameter.ParameterOneofCase.FloatParam => param.FloatParam.ToString(),
                    RequestParameter.ParameterOneofCase.IntParam => param.IntParam.ToString(),
                    RequestParameter.ParameterOneofCase.LongParam => param.LongParam.ToString(),
                    RequestParameter.ParameterOneofCase.StringParam => param.StringParam,
                    _ => null
                };
            }

            _logger.LogInformation(
                $"State change request received: ChangeType={stateChange.ChangeType}, Parameter={parameterDetails ?? "<none>"}");
        }


        private void HandleSnapshotQuery(SnapshotQueryRequest query, StateChangeResponse response)
        {
            var snapshots = _activityMemberSocketListener.CurrentActivityMemberList;

            if (!string.IsNullOrEmpty(query.AccountName))
            {
                // Filtered: return snapshot for specific account
                if (snapshots.TryGetValue(query.AccountName, out var snapshot))
                {
                    response.Snapshots.Add(snapshot);
                    _logger.LogDebug($"Snapshot query: returning snapshot for '{query.AccountName}'");
                }
                else
                {
                    _logger.LogWarning($"Snapshot query: account '{query.AccountName}' not found");
                    response.Response = ResponseResult.Failure;
                }
            }
            else
            {
                // Unfiltered: return all snapshots
                foreach (var kvp in snapshots)
                    response.Snapshots.Add(kvp.Value);
                _logger.LogDebug($"Snapshot query: returning {response.Snapshots.Count} snapshots");
            }

            if (response.Response != ResponseResult.Failure)
                response.Response = ResponseResult.Success;
        }


        private void HandleActionForward(ActionForwardRequest forward, StateChangeResponse response)
        {
            if (string.IsNullOrEmpty(forward.AccountName) || forward.Action == null)
            {
                _logger.LogWarning("Action forward: missing account name or action");
                response.Response = ResponseResult.Failure;
                return;
            }

            var enqueued = _activityMemberSocketListener.EnqueueAction(forward.AccountName, forward.Action);
            response.Response = enqueued ? ResponseResult.Success : ResponseResult.Failure;
            Console.WriteLine($"[ACTION-DIAG] Action forward: {(enqueued ? "queued" : "DROPPED")} {forward.Action.ActionType} for '{forward.AccountName}'");
            _logger.LogInformation($"Action forward: {(enqueued ? "queued" : "DROPPED")} {forward.Action.ActionType} for '{forward.AccountName}'"  );
        }

        /// <summary>
        /// Checks if a service is listening on the specified IP and port.
        /// Returns true if the service is ready to accept connections.
        /// </summary>
    }
}
