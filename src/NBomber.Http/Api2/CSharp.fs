namespace NBomber.Http.Api2.CSharp

open System
open System.Net
open System.Net.Http
open System.Threading.Tasks

open NBomber.CSharp
open NBomber.Contracts
open NBomber.Http
open NBomber.Http.FSharp
open System.Runtime.InteropServices

module Map =
    let ofStructTuples (values : struct(string*string) array) =
        let mutable result = Map.empty
        if isNull values then result else
        for struct(name,value) in values do
            result <- Map.add name value result
        result
            
type Http =
    /// Creates a http request with specified parameters
    static member CreateRequest(url: string,
                                /// http method, default is GET
                                [<Optional; DefaultParameterValue (null : HttpMethod) >]
                                method: HttpMethod,
                                /// optional http headers list
                                [<Optional; DefaultParameterValue (null:struct(string*string) array) >]
                                headers: struct(string*string) array,
                                /// optional http version, default is 2.0
                                [<Optional; DefaultParameterValue (null : string) >]
                                version: string,
                                /// optional http content body
                                [<Optional; DefaultParameterValue (null : HttpContent) >]
                                body : HttpContent,
                                /// expected response code. If not defined will be checked to be a success code
                                [<Optional; DefaultParameterValue (Nullable<HttpStatusCode>() : HttpStatusCode Nullable) >]
                                responseCode : HttpStatusCode Nullable,
                                /// optional check response function
                                [<Optional; DefaultParameterValue (null : System.Func<HttpResponseMessage,Task<bool>>) >]
                                checkResponse : System.Func<HttpResponseMessage,Task<bool>>) =
        { Url = Uri(url)
          Version = Version.parse version
          Method = if isNull method then HttpMethod.Get else method
          Headers = if isNull headers then [||] else headers |> Array.map (fun struct(k,v) -> k,v)
          Body = body
          ResponseCode = ValueOption.ofNullable responseCode
          Check = if isNull checkResponse
                  then (fun _ -> Task.FromResult true)
                  else checkResponse.Invoke
        }
        
type HttpStep =

    static member Create(name: string, createRequest: Func<StepContext<Unit>, HttpRequest>) =
        HttpStep.create(name, createRequest.Invoke)
        
    static member Create(name: string, createRequest: Func<StepContext<Unit>, Task<HttpRequest>>) =
        HttpStep.create(name, createRequest.Invoke)
        
type Scenario =
    static member Create(name : string,
                         [<ParamArray>]steps : IStep array) =
        ScenarioBuilder.CreateScenario(name, steps)
    static member Create(name : string,
                         concurrentCopies : int,
                         duration : TimeSpan,
                         [<ParamArray>]steps : IStep array) =
        ScenarioBuilder.CreateScenario(name, steps)
            .WithDuration(duration)
            .WithConcurrentCopies(concurrentCopies)