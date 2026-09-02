# Laboratório 05 — Atividade 1 — Erick

## Escopo analisado

O backend usa .NET 8 e está dividido nos projetos `Api`, `Application`, `Contracts`, `Domain`, `GameServer` e `Infrastructure`. Antes deste laboratório, `GameServer` já existia na solução, mas não possuía implementação. A autenticação JWT, as salas REST, o chat REST em memória e uma política CORS para o Angular já estavam presentes.

## Implementação SignalR

O `GameHub` foi criado em `src/BattleTanks.GameServer/GameHub.cs` e exposto em:

```text
http://localhost:5049/hubs/game
```

O registro é feito por `AddGameServer()` e o endpoint por `app.MapHub<GameHub>(GameHub.Route)`. O projeto `BattleTanks.GameServer` recebeu uma referência ao framework `Microsoft.AspNetCore.App`, sem um pacote SignalR separado, pois o servidor SignalR faz parte do shared framework do ASP.NET Core.

### Métodos chamados pelo cliente

| Método            | Finalidade                                                                                        |
| ----------------- | ------------------------------------------------------------------------------------------------- |
| `JoinRoom`        | Adiciona a conexão ao grupo SignalR identificado pelo código da sala e envia jogadores/histórico. |
| `MovePlayer`      | Valida e atualiza posição/direção, envia o movimento aos outros clientes e retorna ACK.           |
| `SendChatMessage` | Persiste no repositório de chat existente, transmite ao grupo inteiro e retorna ACK.              |
| `DestroyMapTile`  | Transmite a destruição de parte do mapa aos outros clientes e retorna ACK.                        |

### Eventos enviados pelo servidor

| Evento                    | Destino                                          |
| ------------------------- | ------------------------------------------------ |
| `ReceiveLocalPlayerId`    | Cliente que entrou.                              |
| `ReceivePlayersUpdated`   | Todos os clientes da sala.                       |
| `ReceivePlayerMove`       | Outros clientes da sala, sem eco para o emissor. |
| `ReceiveChatHistory`      | Cliente que entrou.                              |
| `ReceiveChatMessage`      | Todos os clientes da sala, inclusive o emissor.  |
| `ReceiveMapTileDestroyed` | Outros clientes da sala, sem eco para o emissor. |

Os contratos realtime ficam em `src/BattleTanks.Contracts/Realtime`. O chat reutiliza `ChatResponse` para persistência e histórico; a transmissão realtime acrescenta metadados de evento para medição.

## Estado compartilhado

`InMemoryGameRoomState` mantém jogadores por sala e conexões por jogador. O estado:

- é protegido por lock para acesso concorrente;
- atribui um dos quatro spawns do mapa;
- mantém o jogador enquanto pelo menos uma aba/conexão dele estiver ativa;
- remove o jogador após a última desconexão;
- devolve snapshots em vez de expor as coleções mutáveis.

Esse estado é volátil e adequado ao escopo do laboratório. Em produção com múltiplas instâncias, seria necessário um backplane/serviço distribuído e persistência apropriada.

## JWT e autorização

O hub possui `[Authorize]`. O cliente JavaScript fornece o token por `accessTokenFactory`. Para WebSockets e Server-Sent Events, o middleware JWT aceita `access_token` na query string somente quando o caminho começa com `/hubs/game`. O ID e o nome do jogador são lidos das claims já emitidas pelo `JwtService`; valores fornecidos pelo navegador não são usados como identidade.

## CORS

A política existente `AllowFrontend` foi preservada:

- origens explícitas `http://localhost:4200` e `http://127.0.0.1:4200`;
- qualquer header e método;
- credenciais permitidas.

Não foi usada a combinação inválida/insegura `AllowAnyOrigin()` com `AllowCredentials()`.

## Como executar

```bash
dotnet restore BattleTanks.sln
dotnet build BattleTanks.sln --no-restore
dotnet test BattleTanks.sln --no-build
dotnet run --project src/BattleTanks.Api/BattleTanks.Api.csproj --launch-profile http
```

A API REST fica em `http://localhost:5049` e o hub em `http://localhost:5049/hubs/game`.

## Testes adicionados

`InMemoryGameRoomStateTests` cobre:

- entrada de dois jogadores na mesma sala;
- atualização de posição e metadados de movimento;
- permanência do jogador até sua última conexão ser encerrada.

## Validação realizada neste ambiente

- `git diff --check`: aprovado.
- `dotnet restore BattleTanks.sln`: aprovado.
- `dotnet build BattleTanks.sln --no-restore`: aprovado.
- `dotnet test BattleTanks.sln --no-build`: aprovado, 9 testes, 0 falhas e 0 ignorados.

O host não possuía `dotnet` e o daemon Docker negou acesso. Para não deixar a validação pendente, o SDK oficial .NET 8.0.423 foi baixado em `/tmp` e usado nos comandos acima. O primeiro build identificou uma referência desnecessária a `System.IdentityModel.Tokens.Jwt` no `GameServer`; ela foi removida e o build repetido com sucesso.

## Capturas necessárias

[CAPTURA DE TELA 1 — Backend executando e SignalR conectado]

Mostrar o terminal com `dotnet run`, a URL `http://localhost:5049` e o log de uma conexão estabelecida no endpoint `/hubs/game`.

[CAPTURA DE TELA 2 — Dois clientes no mesmo grupo]

Mostrar duas janelas do navegador na mesma sala e, nas ferramentas de desenvolvedor, a conexão do hub ativa sem erro de CORS/autenticação.

[CAPTURA DE TELA 3 — Build e testes do backend]

Mostrar `dotnet build BattleTanks.sln --no-restore` e `dotnet test BattleTanks.sln --no-build` terminando sem erros.

## Referências oficiais

- [ASP.NET Core SignalR overview](https://learn.microsoft.com/en-us/aspnet/core/signalr/introduction?view=aspnetcore-8.0)
- [Use hubs in ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/hubs?view=aspnetcore-8.0)
- [Authentication and authorization in ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz?view=aspnetcore-8.0)
- [Security considerations in ASP.NET Core SignalR](https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-8.0)
- [CORS in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/cors?view=aspnetcore-8.0)
