# Battle Tanks — Benchmarking e profiling

**Data da inspeção:** 2026-09-06  
**Escopo:** backend ASP.NET Core, frontend Angular, API REST, SignalR, PostgreSQL, Redis e Canvas 2D.  
**Estado deste documento:** pronto para conversão em PDF; as áreas de evidência do Chrome continuam deliberadamente vazias até uma medição manual.

## 1. Resumo e rastreabilidade

Este trabalho não altera o comportamento funcional do Battle Tanks. Foram adicionados dois cenários k6 e documentação de execução/profiling.

| Categoria | Situação |
|---|---|
| Encontrado no código | API em `http://localhost:5049`, Angular em `http://localhost:4200`, Hub `/hubs/game`, endpoint público `GET /api/room`, NgRx + Angular Signals, Canvas com `requestAnimationFrame`, PostgreSQL e Redis |
| Implementado nesta atividade | `k6/basic.js`, `k6/load.js`, `CHROME-PROFILING.md`, este relatório e a pasta de resultados |
| Realmente executado | inspeção estática; migrations EF existentes; API e frontend locais; confirmação HTTP 200; validação e execução dos testes k6 básico e progressivo |
| Não executado | captura do Chrome DevTools; nenhuma métrica ou screenshot foi inventado |

## 2. Inventário técnico encontrado

### URLs, portas e persistência

- Backend: `http://localhost:5049` no profile HTTP; também há `https://localhost:7281` no profile HTTPS. Evidência: `src/BattleTanks.Api/Properties/launchSettings.json`.
- Frontend: `http://localhost:4200`. Evidência: `frontend/README.md`, configuração padrão do Angular CLI e CORS em `src/BattleTanks.Api/Program.cs`.
- PostgreSQL: Npgsql/EF Core, banco `battle_tanks` em `localhost:5433`, contextos de escrita e leitura (`ApplicationDbContext` e `ApplicationReadDbContext`) e pooling configurado. Evidência: `appsettings.Development.json` e `BattleTanks.Infrastructure/DependencyInjection.cs`.
- Redis: StackExchange.Redis em `localhost:6379`; histórico de eventos, presença online, ranking e blacklist de JWT compartilham um `IConnectionMultiplexer`. Evidência: `BattleTanks.Infrastructure/DependencyInjection.cs`, `Caching/` e `Messaging/`.

### Endpoints REST

| Método e rota | Autorização | Função/dependência relevante |
|---|---:|---|
| `POST /api/auth/register` | pública | cria jogador no PostgreSQL |
| `POST /api/auth/login` | pública | autentica jogador no PostgreSQL |
| `POST /api/auth/logout` | JWT | inclui JWT na blacklist Redis |
| `GET /api/room?page=&pageSize=` | pública | lista paginada de salas em espera no PostgreSQL |
| `POST /api/room` | JWT | cria sala |
| `PUT /api/room/join` | JWT | entra em sala pelo código |
| `GET /api/ranking/top?take=` | pública | lê Redis; em cache miss reconstrói a partir do PostgreSQL |
| `POST /api/ranking/refresh` | JWT | reconstrói ranking Redis a partir do PostgreSQL |
| `GET /api/chat/{roomCode}` | JWT | consulta repositório de chat simulado em memória |
| `POST /api/chat/{roomCode}` | JWT | grava mensagem no repositório simulado |
| `GET /api/mqtt/config` | JWT | fornece configuração MQTT do frontend |

O endpoint escolhido para os dois testes foi **`GET /api/room?page=1&pageSize=20`**: é público, somente leitura e já era usado no benchmark existente. Ele não é um health check: percorre o repositório EF Core e exige PostgreSQL, característica que deve constar na interpretação dos resultados.

### SignalR e estado Angular

- Hub: `GameHub`, rota constante `/hubs/game`, mapeada em `Program.cs` e protegida por JWT.
- Métodos cliente → Hub: `JoinRoom`, `MovePlayer`, `SendChatMessage`, `HitPlayer` e `DestroyMapTile`; `OnDisconnectedAsync` limpa presença e publica a lista atualizada.
- Eventos Hub → cliente: `ReceiveLocalPlayerId`, `ReceiveChatHistory`, `ReceiveGameEventHistory`, `ReceivePlayersUpdated`, `ReceivePlayerMove`, `ReceiveChatMessage`, `ReceivePlayerHit` e `ReceiveMapTileDestroyed`.
- Serviço Angular principal: `core/services/game.service.ts`, baseado em `@microsoft/signalr`, token via `accessTokenFactory`, reconexão automática e dispatch de actions NgRx. `realtime-benchmark.service.ts` mede acknowledgements/eventos; `mqtt-game.service.ts` cobre o canal MQTT paralelo.
- Estado: NgRx Store (`game.actions`, `game.reducer`, `game.selectors`) e Angular Signals em `BattleMapSignalStore`, `TankSignalStore`, `ExplosionSignalStore`, `PlayerStatusSignalStore` e componentes auxiliares. Não foi encontrado `@ngrx/signals`/SignalStore oficial; as classes com nome `SignalStore` usam `signal()` e `computed()` diretamente.

### Canvas e scripts prévios

- Componente: `frontend/src/app/features/game/game-canvas/game-canvas.ts`, `<canvas>` em `game-canvas.html`.
- Loop: `requestAnimationFrame(this.gameLoop)`; por frame chama `updateLocalTankAnimation`, `updateProjectile`, `updateExplosions`, `sendMovementWhenNeeded` e `draw`.
- Scripts prévios: `backend/benchmarks/k6.test.js`, `artillery.test.yml`, `auth.test.js`, `signalr.test.js`, `mqtt.test.yml`, JMeter, Gatling e Apache Bench; no frontend há `signalr-benchmark.mjs` e `realtime-benchmark.mjs`. O `k6.test.js` anterior usa `/api/room`, mas só limita p95 e mistura ramp-up/sustentação/ramp-down; não atende sozinho aos requisitos do teste básico.

## 3. k6 versus Artillery

| Critério | k6 | Artillery | Consequência para o Battle Tanks |
|---|---|---|---|
| Definição | JavaScript/TypeScript em runtime próprio, APIs de teste k6 | YAML ou JavaScript/TypeScript em Node.js | k6 dá checks, métricas e thresholds muito explícitos; YAML do Artillery é rápido para fluxos simples |
| Cenários HTTP | Código JS, executors/stages e métricas customizadas | Fases e flows declarativos, hooks JS quando necessário | ambos cobrem bem `/api/room` |
| WebSocket | módulos `k6/websockets` e legado `k6/ws` | engine WebSocket nativo | ambos falam frames WebSocket, não a semântica SignalR automaticamente |
| SignalR | sem cliente SignalR nativo no core; implementar negociação/protocolo é frágil ou requer extensão/componente externo | sem engine SignalR oficial; um processor/hook Node pode usar `@microsoft/signalr`, mas deixa de ser um fluxo WebSocket YAML simples | o script Node existente `signalr.test.js` é mais seguro para o Hub real do que fingir que `/hubs/game` é WebSocket puro |
| InfluxDB/Grafana | saída InfluxDB documentada; dashboards Grafana naturais | integrações/plugins/reporters dependem da versão e configuração, ou publicação via OpenTelemetry/serviço | o repositório já usa k6 + InfluxDB 1.8 em `:8087` + Grafana, reduzindo trabalho de integração |
| Escala/VUs | binário Go eficiente; execução local, Grafana Cloud ou distribuição por infraestrutura | Node.js local e execução distribuída/cloud em AWS/Azure | capacidade real deve ser calibrada na máquina; não existe número universal de VUs |
| CPU/memória do gerador | em geral baixo overhead por VU porque VUs não são browsers/threads completos, mas JS, conexões e métricas ainda consomem recursos | o runtime Node e plugins/processors influenciam o custo; Playwright é muito mais pesado | medir CPU/RSS do próprio gerador; não assumir que um resultado genérico vale nesta máquina |
| CI/CD | thresholds produzem exit code não zero; binário/container simples | `ensure`/expectativas e CLI integram com pipelines; npm facilita projetos JS | k6 encaixa diretamente nos thresholds solicitados e na infraestrutura já presente |

**Escolha recomendada:** k6 para os benchmarks REST desta atividade. A decisão não é “k6 é sempre melhor”: aqui pesam os thresholds nativos, a saída InfluxDB, o Grafana já configurado e os scripts k6 existentes. Artillery continua adequado aos testes HTTP/MQTT já presentes e é atraente quando o time prefere fases YAML e hooks Node.

Para SignalR, nenhum dos dois deve ser usado como WebSocket cru sem trabalho adicional. O cliente primeiro negocia transporte e formato, autentica, executa o handshake do protocolo Hub e troca mensagens enquadradas pelo protocolo SignalR; pode usar WebSockets, Server-Sent Events ou Long Polling. Um teste que apenas obtém HTTP 101 em `/hubs/game` não valida `JoinRoom` ou `MovePlayer`. A opção do projeto é manter o cliente oficial `@microsoft/signalr` no benchmark Node existente, ou construir/adotar uma integração SignalR conscientemente e validar compatibilidade de versões. Referências: [transportes e configuração do ASP.NET Core SignalR](https://learn.microsoft.com/aspnet/core/signalr/configuration), [WebSockets no k6](https://grafana.com/docs/k6/latest/using-k6/protocols/websockets/), [engine WebSocket do Artillery](https://www.artillery.io/docs/reference/engines/websocket), [extensões k6](https://grafana.com/docs/k6/latest/extensions/explore/).

## 4. Testes implementados

### Básico

Arquivo: `docs/performance-lab/k6/basic.js`.

- 5 VUs constantes por 20 segundos, pausa de 1 segundo por iteração.
- GET no endpoint real `/api/room?page=1&pageSize=20`.
- Checks de status 200, `Content-Type` JSON e contrato paginado (`items` array e `totalCount` inteiro).
- Thresholds: `http_req_failed < 1%`, p95 de `http_req_duration < 500 ms` e checks `> 99%`.

### Progressivo

Arquivo: `docs/performance-lab/k6/load.js`.

- 10 s até 5 VUs; 20 s até 15; 15 s até o pico local de 25; 10 s reduzindo a 5; 5 s até zero.
- Mesmos endpoint e objetivos. A carga máxima de 25 VUs é deliberadamente moderada para uma estação local.
- A comparação válida exige executar os dois no mesmo host, build, seed e estado de cache. O básico fornece uma linha de base estável; o progressivo mostra como p95, falhas e RPS mudam durante rampas e pico. Os resultados reais estão na seção 5 e em `results/k6-summary.md`.

Os thresholds são critérios iniciais da atividade, não SLOs de produção aprovados. O k6 retorna código diferente de zero quando um threshold falha. Referência: [thresholds do k6](https://grafana.com/docs/k6/latest/using-k6/thresholds/).

## 5. Execução real e pendência

### O que foi executado

```text
$ k6 version
k6 v2.2.0+dirty (commit/00a9a1b7f5-dirty, go1.26.5, linux/amd64)

$ curl 'http://localhost:5049/api/room?page=1&pageSize=20'
HTTP/1.1 200 OK
Content-Type: application/json; charset=utf-8

$ k6 inspect docs/performance-lab/k6/basic.js
# exit code 0; confirmou 5 VUs, 20 s e os três thresholds

$ k6 inspect docs/performance-lab/k6/load.js
# exit code 0; confirmou as cinco etapas e os três thresholds
```

Na primeira chamada após iniciar o backend, a API retornou HTTP 500 com `42703: column g.Region does not exist`. Foram aplicadas apenas as migrations EF existentes `AddPerformanceIndexes` e `AddRegionAndScalabilityIndexes`, usando `ApplicationDbContext`; nenhuma regra de negócio ou arquivo de configuração foi alterado. Após isso, o endpoint respondeu HTTP 200. PostgreSQL (`5433`) e Redis (`6379`) já estavam escutando, embora o usuário da sessão não tivesse permissão para consultar `/var/run/docker.sock`; a conectividade real da aplicação confirmou as dependências. EMQX não foi iniciado.

O k6 foi executado com `set -o pipefail` e `tee`. Ambos os processos terminaram com exit code 0. As saídas integrais estão em `results/k6-basic.txt` e `results/k6-load.txt`.

### Comandos manuais

Terminal 1, a partir de `backend/`:

```bash
docker compose up -d postgres redis
docker compose ps
dotnet run --project src/BattleTanks.Api --launch-profile http
```

Terminal 2, também em `backend/`:

```bash
curl --fail-with-body http://localhost:5049/api/room?page=1\&pageSize=20
mkdir -p docs/performance-lab/results
BASE_URL=http://localhost:5049 k6 run docs/performance-lab/k6/basic.js \
  2>&1 | tee docs/performance-lab/results/k6-basic.txt
BASE_URL=http://localhost:5049 k6 run docs/performance-lab/k6/load.js \
  2>&1 | tee docs/performance-lab/results/k6-load.txt
```

Se o ambiente não tiver `.env`, copie `.env.example` para `.env` e confira as portas/credenciais antes do `docker compose up`. Para preservar corretamente o exit code do k6 ao usar `tee`, execute em Bash com `set -o pipefail` ou confira `${PIPESTATUS[0]}` logo após cada comando.

### Métricas a extrair das saídas reais

| Métrica | Significado |
|---|---|
| `http_reqs` | total de requisições HTTP e taxa por segundo exibida ao lado (`count / duração`) |
| `http_req_duration` | tempo da requisição após obter conexão, agregado em min/med/avg/max e percentis |
| `p(95)` | valor abaixo do qual ficaram 95% das durações; os 5% mais lentos ficam acima |
| `http_req_failed` | proporção/contagem de respostas que k6 classifica como falha HTTP |
| `checks` | proporção/contagem das asserções aprovadas; são três por iteração no básico |
| `iterations` | número de execuções completas da função `default` |
| `vus` / `vus_max` | VUs ativos no ponto final/amostrado e máximo alocado |
| requests/s | taxa ao lado de `http_reqs`, útil para comparar vazão sob a mesma configuração |

| Execução | http_reqs | Requests/s | avg | med | p(90) | p(95) | max | failed | checks | iterations | vus | vus_max |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|---|
| Básica | 100 | 4.966894/s | 5.83 ms | 2.51 ms | 4.81 ms | 9.24 ms | 63.43 ms | 0.00% (0/100) | 100.00% (300/300) | 100 | 5 (min 5, max 5) | 5 |
| Progressiva | 686 | 11.39512/s | 2.4 ms | 2.38 ms | 2.7 ms | 2.85 ms | 7.63 ms | 0.00% (0/686) | 100.00% (1372/1372) | 686 | 1 ao fim (min 1, max 25) | 25 |

### Comparação dos resultados

O progressivo teve maior throughput agregado, `11.39512 req/s` contra `4.966894 req/s`, coerente com sua quantidade maior de VUs. O p95 caiu de `9.24 ms` no básico para `2.85 ms` no progressivo; isso não prova uma melhoria causada pela carga e pode refletir aquecimento do backend, conexões e caches, já que o progressivo foi executado depois. O máximo também caiu de `63.43 ms` para `7.63 ms`.

Não houve requisição falha nem check reprovado, e todos os thresholds passaram nos dois testes. Nesta amostra não há sinal de saturação: ao chegar a 25 VUs, a latência permaneceu baixa e a taxa de sucesso foi 100%. Portanto, 25 VUs parecem uma carga confortável para este endpoint na máquina local sob estas condições; isso não estabelece a capacidade máxima do sistema nem cobre SignalR.

### Frontend preparado para profiling

O Angular foi iniciado em `http://localhost:4200`; `/` e `/tutorial` responderam HTTP 200. A rota `/tutorial` carrega diretamente o componente `Game`, que inclui `<app-game-canvas>`, sem exigir autenticação, sala, SignalR ou EMQX, e é o fluxo mínimo para profiling visual do Canvas. Para perfilar o fluxo multiplayer real: acesse `/auth`, registre-se ou faça login, volte a `/menu`, informe o nome, crie ou entre em uma sala, aguarde em `/waiting-room` e então abra o jogo. Esse fluxo requer JWT e associação à sala; para a captura básica do Canvas, use `http://localhost:4200/tutorial`.

## 6. Métricas relevantes para o multiplayer

- **Latência:** atraso de uma etapa, por exemplo cliente → servidor. No código, `clientSentAtUnixMs` e `serverReceivedAt` permitem observar parte desse caminho, sujeitos a relógios diferentes.
- **RTT:** ida e volta entre envio e acknowledgement. É a medida mais direta para a sensação de resposta de movimento, tiro e chat.
- **Throughput:** mensagens/eventos ou bytes por segundo. Destruição de mapa e movimentação multiplicam por jogadores e destinatários.
- **FPS:** frames apresentados por segundo no cliente. O alvo deve ser associado ao dispositivo/tela; 60 FPS implica orçamento aproximado de 16,7 ms por frame.
- **Frame time:** duração individual do frame. É melhor que apenas FPS médio para revelar engasgos.
- **Jitter:** variação da latência/RTT. Mesmo uma média aceitável com alta variação causa movimento irregular.
- **Taxa de erros:** HTTP, falhas de Hub, reconexões, mensagens rejeitadas/perdidas e thresholds quebrados.
- **CPU e memória:** devem ser coletadas separadamente no backend, banco, Redis, navegador e gerador. Pressão de GC pode aparecer como picos de frame time ou p99.
- **Tráfego de rede:** bytes/s, mensagens/s e tamanho por evento; broadcasts SignalR aumentam com os participantes da sala.

Faixas abaixo são **metas práticas iniciais para medição**, não limites universais. Devem ser ajustadas após playtest, região e arquitetura autoritativa:

| Ação | RTT desejável (aprox.) | Jitter desejável (aprox.) | Observação Battle Tanks |
|---|---:|---:|---|
| Movimento | p95 ≤ 100–150 ms | p95 ≤ 20–30 ms | mais sensível e frequente; cliente envia mudanças com limite de 50 ms |
| Disparo/ack | p95 ≤ 100–150 ms | p95 ≤ 20–30 ms | feedback visual local pode ser imediato, mas acerto deve convergir com o servidor |
| Colisão/dano | p95 ≤ 100–150 ms; p99 ≤ 200–250 ms | baixo | `HitPlayer` é autoritativo e pode também publicar evento MQTT QoS 1 |
| Destruição de mapa | p95 ≤ 150–250 ms | p95 ≤ 30–50 ms | atualiza tiles e dispara evento paralelo; consistência é importante |
| Chat | p95 ≤ 300–500 ms | tolerância maior | menos sensível que combate, mas falhas/reordenação continuam visíveis |

A média resume o centro e pode esconder caudas: nove respostas rápidas e uma pausa longa ainda produzem uma média aparentemente razoável. p95 descreve a experiência dos 5% mais lentos; p99 expõe os 1% extremos, onde GC, contenção, cache miss e rede instável aparecem. Em jogo, poucos frames/acks muito lentos já causam teleporte, tiro tardio ou colisão contestada; por isso p95/p99 devem acompanhar a média.

## 7. Wi-Fi, 4G e 5G

| Meio | Comportamento provável | Impacto na experiência |
|---|---|---|
| Wi-Fi | pode ter baixa latência perto do roteador, mas interferência, canal congestionado, distância e roaming aumentam jitter e perda | microtravamentos e rajadas de updates; testar 2,4/5/6 GHz e distância reais |
| 4G | latência normalmente maior e variável por célula, sinal, mobilidade e handover; perda pode ocorrer em cobertura fraca | comandos/acks mais tardios, reconexões e movimento remoto irregular |
| 5G | pode oferecer menor latência e maior capacidade, mas modo NSA/SA, banda, cobertura, carga e fallback alteram muito o resultado | excelente em boas condições, porém transições 5G/4G podem gerar picos perceptíveis |

Como exemplos de laboratório, podem-se ensaiar RTT adicional de 20/50/100 ms, jitter de 5/20/50 ms e perda de 0,1/1/3%. Esses valores não representam universalmente Wi-Fi, 4G ou 5G. Registre sinal, operadora, local, horário e rota ao servidor; compare p95/p99 e reconexões, não apenas “ping médio”.

## 8. Profiling estático do Canvas real

### Trabalho feito a cada frame

O `gameLoop` roda fora da Angular Zone, uma escolha positiva que evita change detection a ~60 Hz. Ainda assim, cada frame:

1. interpola a animação local;
2. atualiza o projétil em subpassos e verifica colisão contra jogadores e mapa;
3. chama `ExplosionSignalStore.advance(timestamp)`;
4. eventualmente envia movimento via SignalR;
5. limpa **todo o Canvas 800×600**, pinta o fundo, redesenha mapa, grade, tanque local, todos os tanques remotos, projétil e explosões.

O mapa expandido possui 60×45 = **2.700 tiles**. `drawExpandedMap` lê `mapStore.tiles()` e percorre todas as 2.700 posições em todos os frames, consultando `terrainDefinition`; tiles com cor chamam `drawTerrainTile`, que pode emitir até três `fillRect`. `drawGrid` reconstrói o path da grade inteira. `drawTank` usa save/translate/rotate e texto para jogadores remotos. As colisões de projétil e ocupação convertem `remotePlayers.values()` em array (`[...values()]`); `projectileImpactTiles` cria arrays com `map` e `filter` no impacto. `advance` atualiza o signal de explosões a cada frame enquanto há explosões.

### Gargalos potenciais diretamente sustentados pelo código

1. **Redesenho completo e 2.700 iterações por frame:** é o candidato estático principal. A confirmação de custo depende do flame chart, ainda não capturado.
2. **Alocações no caminho de animação/colisão:** spread de `Map.values()` em `findProjectileHitPlayer` e `isCellOccupiedByRemoteTank`; arrays de impacto em colisões. Com até quatro jogadores o custo atual é pequeno, mas é evitável e pode alimentar GC em sessões longas.
3. **Signals em alta frequência:** `ExplosionSignalStore.advance` faz `signal.update` durante cada frame com explosões; `TankSignalStore.moveTo` ocorre no fim de cada movimento, não em cada interpolação; `BattleMapSignalStore` atualiza ao destruir tiles. Logo, não há evidência de update de todos os signals a 60 Hz, somente o de explosões durante sua vida.
4. **Possível layout/scroll:** `centerViewportOnLocalTank` lê `clientWidth/clientHeight` e executa `scrollTo({behavior:'smooth'})` após movimento. Isso não ocorre todo frame, mas pode introduzir trabalho de layout/composição; somente DevTools pode confirmar.
5. **devicePixelRatio:** `resizeCanvas` define backing store em `800 × DPR` por `600 × DPR`. DPR 2 quadruplica pixels; DPR 3 multiplica por nove. O loop continua desenhando em coordenadas lógicas, mas rasteriza mais pixels. O resize não ocorre por frame, embora o custo de pintura permaneça maior.

### Melhorias sugeridas, não implementadas

1. Pré-renderizar mapa estático e grade em `OffscreenCanvas`/Canvas auxiliar e copiar por frame; invalidar/recompor apenas quando `mapRevision` mudar após destruição.
2. Aplicar dirty rectangles ou separar camadas: mapa estático, entidades dinâmicas e efeitos. Isso evita redesenhar 2.700 tiles quando somente tanque/projétil muda.
3. Iterar diretamente `for (const player of this.remotePlayers.values())` nas colisões, retornando ao achar alvo/ocupação, sem materializar arrays.
4. Manter explosões em estado imperativo interno do renderer e publicar signal apenas quando a UI externa realmente precisar; confirmar antes se templates consomem contadores.
5. Limitar o DPR do Canvas (por exemplo, teto configurável) em dispositivos densos e comparar nitidez versus frame time; não aplicar sem profiling visual.
6. Substituir o scroll suave a cada célula por transformação/câmera dentro do Canvas ou agrupar leituras/escritas; primeiro confirmar eventos Layout/Composite no trace.

Nenhuma dessas mudanças foi aplicada nesta atividade, conforme solicitado.

## 9. Evidências Chrome DevTools

O procedimento completo e os nomes dos arquivos estão em `CHROME-PROFILING.md`.

### Screenshot 1 — visão geral da aba Performance

`[INSERIR SCREENSHOT CHROME DEVTOOLS 1 AQUI]`

### Screenshot 2 — Frames com frame time elevado

`[INSERIR SCREENSHOT CHROME DEVTOOLS 2 AQUI]`

### Screenshot 3 — Main Thread/flame chart e função mais custosa

`[INSERIR SCREENSHOT CHROME DEVTOOLS 3 AQUI]`

### Screenshot 4 — Long Task ou evidência de ausência

`[INSERIR SCREENSHOT CHROME DEVTOOLS 4 AQUI]`

Após inserir as capturas, registre duração, FPS/frame time observado, Long Tasks e Self/Total Time. Não transforme uma única amostra em conclusão universal.

## 10. Checklist de conclusão

- [x] Inventário do código real.
- [x] Comparação contextual k6 versus Artillery e limitação SignalR.
- [x] Teste básico k6 com endpoint real e thresholds.
- [x] Teste progressivo local razoável.
- [x] Verificação da instalação do k6 e disponibilidade da API.
- [x] Execução válida do k6.
- [ ] Captura manual do Chrome DevTools.
- [x] Inserção das métricas reais do k6 neste relatório.
- [ ] Inserção das screenshots reais do Chrome DevTools neste relatório.

## Referências técnicas

- [k6 — JavaScript API](https://grafana.com/docs/k6/latest/javascript-api/)
- [k6 — thresholds](https://grafana.com/docs/k6/latest/using-k6/thresholds/)
- [k6 — saída InfluxDB](https://grafana.com/docs/k6/latest/results-output/real-time/influxdb/)
- [k6 — WebSockets](https://grafana.com/docs/k6/latest/using-k6/protocols/websockets/)
- [Artillery — conceitos de load testing](https://www.artillery.io/docs/get-started/load-testing)
- [Artillery — engine WebSocket](https://www.artillery.io/docs/reference/engines/websocket)
- [Microsoft — configuração e transportes do SignalR](https://learn.microsoft.com/aspnet/core/signalr/configuration)
