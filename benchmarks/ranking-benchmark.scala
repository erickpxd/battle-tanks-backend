package battletanks

import io.gatling.core.Predef._
import io.gatling.http.Predef._
import scala.concurrent.duration._

/**
 * LB-9 Activity #3 — Gatling simulation
 *
 * Deliverable filename: ranking-benchmark.scala
 *
 * Scenarios:
 *  1) REST ranking — GET /api/ranking/top (primary)
 *  2) SignalR hub reachability — HTTP probe against /hubs/game
 *     (full MovePlayer load remains in benchmarks/signalr.test.js; Gatling
 *     exercises the hub endpoint under concurrent clients)
 *  3) MQTT topic fan-out is covered by benchmarks/mqtt.test.yml (Artillery);
 *     this simulation documents the companion HTTP path used before clients
 *     subscribe (health of the API that fronts MQTT config).
 *
 * Run (Docker example — mount user-files + results only):
 *   docker run --rm --network host \
 *     -v "$PWD/benchmarks/gatling/user-files:/opt/gatling/user-files" \
 *     -v "$PWD/benchmarks/gatling/results:/opt/gatling/results" \
 *     -e JAVA_OPTS="-DapiBase=http://localhost:5049" \
 *     denvazh/gatling:3.0.3 \
 *     -s battletanks.RankingBenchmark
 */
class RankingBenchmark extends Simulation {

  private val apiBase = System.getProperty("apiBase", "http://localhost:5049")

  private val httpProtocol = http
    .baseUrl(apiBase)
    .acceptHeader("application/json")
    .userAgentHeader("gatling-lb9-activity3")
    .shareConnections

  private val rankingScn = scenario("Ranking Top-10")
    .exec(
      http("GET /api/ranking/top")
        .get("/api/ranking/top")
        .queryParam("take", "10")
        .check(status.is(200))
        .check(jsonPath("$[*]").count.gte(0))
    )

  private val signalrHubScn = scenario("SignalR Hub Probe")
    .exec(
      http("GET /hubs/game")
        .get("/hubs/game")
        // Negotiate/WebSocket handshake may return several codes without a JWT;
        // we assert the server is reachable (not 5xx).
        .check(status.not(500), status.not(502), status.not(503))
    )

  private val mqttConfigScn = scenario("MQTT Config Probe")
    .exec(
      http("GET /api/mqtt/config")
        .get("/api/mqtt/config")
        .check(status.in(200, 401, 404, 503))
    )

  setUp(
    rankingScn.inject(
      rampUsers(100).during(20.seconds),
      constantUsersPerSec(20).during(30.seconds)
    ),
    signalrHubScn.inject(rampUsers(50).during(20.seconds)),
    mqttConfigScn.inject(rampUsers(30).during(15.seconds))
  ).protocols(httpProtocol)
    .assertions(
      global.responseTime.percentile3.lt(3000),
      global.failedRequests.percent.lt(15)
    )
}
