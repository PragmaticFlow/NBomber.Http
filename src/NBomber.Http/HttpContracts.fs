namespace NBomber.Http

open System
open System.Net.Http
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Text.Json
open System.Threading
open Serilog

[<Struct; IsReadOnly>]
type HttpResponse<'T> = {
    Data: 'T
    Response: HttpResponseMessage
}

/// <summary>
/// Additional arguments for sending HTTP requests: CancellationToken, HttpCompletionOption, JSON serializer options and tracing.
/// The type is an immutable struct. Create it with <c>HttpClientArgs.Create</c>.
/// </summary>
[<Struct; IsReadOnly>]
type HttpClientArgs = {
    HttpCompletion: HttpCompletionOption
    CancellationToken: CancellationToken
    JsonSerializerOptions: JsonSerializerOptions option
    Logger: ILogger option
    TraceId: string
}
with
    static member private build(cancellationToken, httpCompletion, jsonOptions: JsonSerializerOptions, logger: ILogger, traceId: string) = {
        HttpCompletion = httpCompletion
        CancellationToken = cancellationToken
        JsonSerializerOptions = jsonOptions |> Option.ofObj
        Logger = logger |> Option.ofObj
        TraceId =
            if not (isNull traceId) then traceId
            elif isNull logger then ""
            else Guid.NewGuid().ToString("N")
    }

    /// <summary>Creates client arguments.</summary>
    /// <param name="cancellationToken">The token that cancels the request (and reading the response body for typed requests).</param>
    /// <param name="httpCompletion">Defines when the operation completes. The default value: <c>ResponseContentRead</c>.</param>
    /// <param name="jsonOptions">The JSON serializer options. If null, <c>Http.GlobalJsonSerializerOptions</c> is used.</param>
    /// <param name="logger">The logger for HTTP tracing. If null, tracing is disabled.</param>
    /// <param name="traceId">The trace id that is written to the logs. If null and <c>logger</c> is set, a new GUID is used.</param>
    [<CompiledName("Create")>]
    static member create(cancellationToken: CancellationToken,
                         [<Optional;DefaultParameterValue(HttpCompletionOption.ResponseContentRead)>] httpCompletion,
                         [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] jsonOptions: JsonSerializerOptions,
                         [<Optional;DefaultParameterValue(null:ILogger)>] logger: ILogger,
                         [<Optional;DefaultParameterValue(null:string)>] traceId: string) =

        HttpClientArgs.build(cancellationToken, httpCompletion, jsonOptions, logger, traceId)

    /// <summary>Creates client arguments with <c>CancellationToken.None</c>.</summary>
    /// <param name="httpCompletion">Defines when the operation completes. The default value: <c>ResponseContentRead</c>.</param>
    /// <param name="jsonOptions">The JSON serializer options. If null, <c>Http.GlobalJsonSerializerOptions</c> is used.</param>
    /// <param name="logger">The logger for HTTP tracing. If null, tracing is disabled.</param>
    /// <param name="traceId">The trace id that is written to the logs. If null and <c>logger</c> is set, a new GUID is used.</param>
    [<CompiledName("Create")>]
    static member create([<Optional;DefaultParameterValue(HttpCompletionOption.ResponseContentRead)>] httpCompletion,
                         [<Optional;DefaultParameterValue(null:JsonSerializerOptions)>] jsonOptions: JsonSerializerOptions,
                         [<Optional;DefaultParameterValue(null:ILogger)>] logger: ILogger,
                         [<Optional;DefaultParameterValue(null:string)>] traceId: string) =

        HttpClientArgs.build(CancellationToken.None, httpCompletion, jsonOptions, logger, traceId)
