namespace NBomber.Http

open System
open System.Collections.Generic
open System.Data
open System.Diagnostics.Tracing
open System.Threading.Tasks

open NBomber.Contracts
open NBomber.Contracts.Stats
open NBomber.Http.Constants

type private HttpMetricsGrabber(metricsProvider: IMetricsProvider) =
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
                metricsProvider.PublishMetric(HTTP1_CONNECTIONS_METRIC, value)

            | true, name when name = HTTP2_CONNECTIONS_CURRENT_TOTAL ->

                let value = data["Max"] :?> float
                metricsProvider.PublishMetric(HTTP2_CONNECTIONS_METRIC, value)

            | true, name when name = HTTP3_CONNECTIONS_CURRENT_TOTAL ->

                let value = data["Max"] :?> float
                metricsProvider.PublishMetric(HTTP3_CONNECTIONS_METRIC, value)

            | true, name when name = HTTP1_REQUESTS_QUEUE_DURATION ->

                let value = data["Max"] :?> float
                if not(Double.IsInfinity value) then
                    metricsProvider.PublishMetric(HTTP1_REQUESTS_QUEUE_METRIC, value)

            | true, name when name = HTTP2_REQUESTS_QUEUE_DURATION ->

                let value = data["Max"] :?> float
                if not(Double.IsInfinity value) then
                    metricsProvider.PublishMetric(HTTP2_REQUESTS_QUEUE_METRIC, value)

            | true, name when name = HTTP3_REQUESTS_QUEUE_DURATION ->

                let value = data["Max"] :?> float
                if not(Double.IsInfinity value) then
                    metricsProvider.PublishMetric(HTTP3_REQUESTS_QUEUE_METRIC, value)

            | _ -> ()

type HttpMetricsPlugin() =

    let mutable _metricsProvider = Unchecked.defaultof<IMetricsProvider>
    let mutable _metricsGrabber = None

    interface IWorkerPlugin with

        member this.PluginName = "HttpMetricsPlugin"

        member this.Init(context, infraConfig) =
            _metricsProvider <- context.MetricsProvider

            _metricsProvider.RegisterMetric(HTTP1_CONNECTIONS_METRIC, "", 1, MetricType.Gauge)
            _metricsProvider.RegisterMetric(HTTP2_CONNECTIONS_METRIC, "", 1, MetricType.Gauge)
            _metricsProvider.RegisterMetric(HTTP3_CONNECTIONS_METRIC, "", 1, MetricType.Gauge)

            _metricsProvider.RegisterMetric(HTTP1_REQUESTS_QUEUE_METRIC, "ms", 100, MetricType.Gauge)
            _metricsProvider.RegisterMetric(HTTP2_REQUESTS_QUEUE_METRIC, "ms", 100, MetricType.Gauge)
            _metricsProvider.RegisterMetric(HTTP3_REQUESTS_QUEUE_METRIC, "ms", 100, MetricType.Gauge)

            Task.CompletedTask

        member this.GetHints() = Array.empty
        member this.GetStats(stats) = Task.FromResult(new DataSet())

        member this.Start() =
            _metricsGrabber <- Some (new HttpMetricsGrabber(_metricsProvider))
            Task.CompletedTask

        member this.Stop() =
            _metricsGrabber |> Option.iter(fun x -> x.Dispose())
            Task.CompletedTask

        member this.Dispose() =
            _metricsGrabber |> Option.iter(fun x -> x.Dispose())



