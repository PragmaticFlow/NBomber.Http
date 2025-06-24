module private NBomber.Http.Constants

let [<Literal>] HTTP1_CONNECTIONS_CURRENT_TOTAL = "http11-connections-current-total"
let [<Literal>] HTTP1_REQUESTS_QUEUE_DURATION = "http11-requests-queue-duration"
let [<Literal>] HTTP1_CONNECTIONS_METRIC = "HTTP/1.1:connections"
let [<Literal>] HTTP1_REQUESTS_QUEUE_METRIC = "HTTP/1.1:requests-queue-duration"

let [<Literal>] HTTP2_CONNECTIONS_CURRENT_TOTAL = "http20-connections-current-total"
let [<Literal>] HTTP2_REQUESTS_QUEUE_DURATION = "http20-requests-queue-duration"
let [<Literal>] HTTP2_CONNECTIONS_METRIC = "HTTP/2:connections"
let [<Literal>] HTTP2_REQUESTS_QUEUE_METRIC = "HTTP/2:requests-queue-duration"

let [<Literal>] HTTP3_CONNECTIONS_CURRENT_TOTAL = "http30-connections-current-total"
let [<Literal>] HTTP3_REQUESTS_QUEUE_DURATION = "http30-requests-queue-duration"
let [<Literal>] HTTP3_CONNECTIONS_METRIC = "HTTP/3:connections"
let [<Literal>] HTTP3_REQUESTS_QUEUE_METRIC = "HTTP/3:requests-queue-duration"

let [<Literal>] HeaderSeparatorLength = 2 // symbol `: `
let [<Literal>] CrlfLength = 2 // \r\n
let [<Literal>] SpaceLength = 1
let [<Literal>] HttpVersionHeaderLength = 8
let [<Literal>] HostHeaderLength = 4
let [<Literal>] StatusCodeLength = 3
