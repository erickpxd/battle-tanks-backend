# Evidência textual dos testes k6

Execuções locais em 2026-09-06 contra `GET http://localhost:5049/api/room?page=1&pageSize=20`. Os valores abaixo foram transcritos das saídas integrais `k6-basic.txt` e `k6-load.txt`.

| Métrica | Básico | Progressivo |
|---|---:|---:|
| HTTP requests | 100 | 686 |
| Requests/s | 4.966894/s | 11.39512/s |
| Avg latency | 5.83 ms | 2.4 ms |
| Median latency | 2.51 ms | 2.38 ms |
| p90 latency | 4.81 ms | 2.7 ms |
| p95 latency | 9.24 ms | 2.85 ms |
| Max latency | 63.43 ms | 7.63 ms |
| Failed requests | 0.00% (0/100) | 0.00% (0/686) |
| Checks | 100.00% (300/300) | 100.00% (1372/1372) |
| Iterations | 100 | 686 |
| VUs | 5 (min 5, max 5) | 1 ao fim (min 1, max 25) |
| VUs max | 5 | 25 |

O teste progressivo apresentou maior throughput agregado (`11.39512 req/s`) que o básico (`4.966894 req/s`) e, nesta execução, menor p95 (`2.85 ms` contra `9.24 ms`). Como ele foi executado depois, aquecimento do backend, das conexões e dos caches pode explicar parte da redução; os dados não permitem atribuí-la à carga.

Não houve falhas HTTP nem checks reprovados, e nenhum threshold falhou. A latência permaneceu baixa até o pico configurado, sem evidência de saturação nesta amostra. Assim, 25 VUs parecem confortáveis para este endpoint e ambiente local, sem que isso represente o limite do backend ou o comportamento do multiplayer SignalR.
