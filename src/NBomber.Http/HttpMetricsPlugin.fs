namespace NBomber.Http

open System
open System.Collections.Generic
open System.Data
open System.Diagnostics.Tracing
open System.Threading.Tasks

open NBomber.Contracts
open NBomber.Contracts.Metrics
open NBomber.Http.Constants

type private HttpMetricsGrabber(gauges: Dictionary<string,IGauge>) =
    inherit EventListener()

    override this.OnEventSourceCreated(eventSource) =

        if eventSource.Name = "System.Net.Http" then
            let args =
                 ["EventCounterIntervalSec", "5"]
                 |> dict

            base.EnableEvents(eventSource, EventLevel.LogAlways, EventKeywords.All, args)

    override this.OnEventWritten(eventData) =

        if eventData.Payload <> null
           && eventData.Payload.Count <> 0
           && eventData.Payload[0] :? IDictionary<string,obj> then

            let data = eventData.Payload[0] :?> IDictionary<string,obj>
            match data.TryGetValue "Name" with
            | true, name when name = HTTP1_CONNECTIONS_CURRENT_TOTAL ->

                let value = data["Max"] :?> float
                match gauges.TryGetValue HTTP1_CONNECTIONS_METRIC with
                | true, gauge -> gauge.Set value
                | false, _    -> ()

            | true, name when name = HTTP2_CONNECTIONS_CURRENT_TOTAL ->

                let value = data["Max"] :?> float
                match gauges.TryGetValue HTTP2_CONNECTIONS_METRIC with
                | true, gauge -> gauge.Set value
                | false, _    -> ()

            | true, name when name = HTTP3_CONNECTIONS_CURRENT_TOTAL ->

                let value = data["Max"] :?> float
                match gauges.TryGetValue HTTP3_CONNECTIONS_METRIC with
                | true, gauge -> gauge.Set value
                | false, _    -> ()

            | true, name when name = HTTP1_REQUESTS_QUEUE_DURATION ->

                let value = data["Max"] :?> float
                if not(Double.IsInfinity value) then
                    match gauges.TryGetValue HTTP1_REQUESTS_QUEUE_METRIC with
                    | true, gauge -> gauge.Set value
                    | false, _    -> ()

            | true, name when name = HTTP2_REQUESTS_QUEUE_DURATION ->

                let value = data["Max"] :?> float
                if not(Double.IsInfinity value) then
                    match gauges.TryGetValue HTTP2_REQUESTS_QUEUE_METRIC with
                    | true, gauge -> gauge.Set value
                    | false, _    -> ()

            | true, name when name = HTTP3_REQUESTS_QUEUE_DURATION ->

                let value = data["Max"] :?> float
                if not(Double.IsInfinity value) then
                    match gauges.TryGetValue HTTP3_REQUESTS_QUEUE_METRIC with
                    | true, gauge -> gauge.Set value
                    | false, _    -> ()

            | _ -> ()

type HttpVersion =
    | Version1 = 0
    | Version2 = 1
    | Version3 = 2

type HttpMetricsPlugin(monitorVersions: HttpVersion seq) =

    let _gauges = Dictionary<string,IGauge>()
    let mutable _metricsGrabber: HttpMetricsGrabber option = None

    let dispose () =
        _metricsGrabber |> Option.iter(_.Dispose())
        _gauges.Clear()

    new () = new HttpMetricsPlugin([])

    interface IWorkerPlugin with

        member this.PluginName = "HttpMetricsPlugin"

        member this.Init(ctx, infraConfig) =
            if Seq.isEmpty monitorVersions then
                _gauges[HTTP1_CONNECTIONS_METRIC] <- Metric.createGauge(HTTP1_CONNECTIONS_METRIC, "")
                _gauges[HTTP1_REQUESTS_QUEUE_METRIC] <- Metric.createGauge(HTTP1_REQUESTS_QUEUE_METRIC, "ms")
            else
                monitorVersions
                |> Seq.iter(function
                    | HttpVersion.Version2 ->
                        _gauges[HTTP2_CONNECTIONS_METRIC] <- Metric.createGauge(HTTP2_CONNECTIONS_METRIC, "")
                        _gauges[HTTP2_REQUESTS_QUEUE_METRIC] <- Metric.createGauge(HTTP2_REQUESTS_QUEUE_METRIC, "ms")

                    | HttpVersion.Version3 ->
                        _gauges[HTTP3_CONNECTIONS_METRIC] <- Metric.createGauge(HTTP3_CONNECTIONS_METRIC, "")
                        _gauges[HTTP3_REQUESTS_QUEUE_METRIC] <- Metric.createGauge(HTTP3_REQUESTS_QUEUE_METRIC, "ms")

                    | _ ->
                        _gauges[HTTP1_CONNECTIONS_METRIC] <- Metric.createGauge(HTTP1_CONNECTIONS_METRIC, "")
                        _gauges[HTTP1_REQUESTS_QUEUE_METRIC] <- Metric.createGauge(HTTP1_REQUESTS_QUEUE_METRIC, "ms")
                )

            _gauges |> Seq.iter(fun x -> ctx.RegisterMetric x.Value)

            Task.CompletedTask

        member this.GetHints() = Array.empty
        member this.GetStats(stats) = Task.FromResult(new DataSet())

        member this.Start(sessionInfo) =
            _metricsGrabber <- Some (new HttpMetricsGrabber(_gauges))
            Task.CompletedTask

        member this.Stop() =
            dispose()
            Task.CompletedTask

        member this.Dispose() = dispose()
