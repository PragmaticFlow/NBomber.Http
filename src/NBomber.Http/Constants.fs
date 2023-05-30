module private NBomber.Http.Constants

[<Literal>]
let HTTP1_CONNECTIONS_CURRENT_TOTAL = "http11-connections-current-total"
[<Literal>]
let HTTP1_REQUESTS_QUEUE_DURATION = "http11-requests-queue-duration"
[<Literal>]
let HTTP1_CONNECTIONS_METRIC = "HTTP/1.1: connections"
[<Literal>]
let HTTP1_REQUESTS_QUEUE_METRIC = "HTTP/1.1: requests-queue-duration"

[<Literal>]
let HTTP2_CONNECTIONS_CURRENT_TOTAL = "http20-connections-current-total"
[<Literal>]
let HTTP2_REQUESTS_QUEUE_DURATION = "http20-requests-queue-duration"
[<Literal>]
let HTTP2_CONNECTIONS_METRIC = "HTTP/2: connections"
[<Literal>]
let HTTP2_REQUESTS_QUEUE_METRIC = "HTTP/2: requests-queue-duration"

[<Literal>]
let HTTP3_CONNECTIONS_CURRENT_TOTAL = "http30-connections-current-total"
[<Literal>]
let HTTP3_REQUESTS_QUEUE_DURATION = "http30-requests-queue-duration"
[<Literal>]
let HTTP3_CONNECTIONS_METRIC = "HTTP/3: connections"
[<Literal>]
let HTTP3_REQUESTS_QUEUE_METRIC = "HTTP/3: requests-queue-duration"
