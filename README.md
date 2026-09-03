# Battle Tanks Backend

API REST em ASP.NET Core 8 (Minimal APIs) para autenticação, gerenciamento de salas e jogadores do Battle Tanks.

## Stack

- .NET 8
- ASP.NET Core Minimal APIs
- PostgreSQL + Entity Framework Core
- JWT Bearer authentication
- Swagger / OpenAPI

## Estrutura

```text
Data/                 # AppDbContext (EF Core)
Domain/Entities/      # User, Room, RoomPlayer
Domain/Enums/         # RoomStatus, PlayerTeam
Dtos/                 # Contratos da API
Endpoints/            # Minimal API groups
Services/             # Auth, JWT, Rooms
Extensions/           # DI e helpers
```

## Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Docker + Docker Compose

## 1. Subir o PostgreSQL

```bash
cp .env.example .env
docker compose up -d postgres
```

Credenciais padrão (`.env.example`):

| Variável | Valor |
|---|---|
| DB | `battle_tanks` |
| User | `battle_tanks_user` |
| Password | `battle_tanks_password` |
| Port | `5433` (host; evita conflito com Postgres local na 5432) |

A connection string está em `appsettings.json` / `appsettings.Development.json`.

## 2. Rodar a API

```bash
dotnet restore
dotnet run
```

- Swagger UI: [http://localhost:5063/swagger](http://localhost:5063/swagger)
- Health: [http://localhost:5063/health](http://localhost:5063/health)

No startup a API aplica as migrations automaticamente (`Database.Migrate()`).

## Endpoints principais

### Auth

| Método | Rota | Auth | Descrição |
|---|---|---|---|
| POST | `/api/auth/register` | Não | Cria usuário |
| POST | `/api/auth/login` | Não | Retorna JWT (`401` se inválido) |
| GET | `/api/auth/me` | Sim | Perfil autenticado |

### Rooms / Players

| Método | Rota | Descrição |
|---|---|---|
| GET | `/api/rooms` | Lista salas |
| GET | `/api/rooms/{id}` | Detalhe da sala |
| POST | `/api/rooms` | Cria sala (owner entra automaticamente) |
| PUT | `/api/rooms/{id}` | Atualiza sala (owner) |
| DELETE | `/api/rooms/{id}` | Remove sala (owner) |
| POST | `/api/rooms/{id}/join` | Entra na sala (`409` se cheia) |
| POST | `/api/rooms/{id}/leave` | Sai da sala |
| GET | `/api/rooms/{id}/players` | Lista jogadores |
| PUT | `/api/rooms/{id}/players/{playerId}` | Atualiza jogador |
| DELETE | `/api/rooms/{id}/players/{playerId}` | Remove jogador |

## Testando

### Swagger

1. Abra `/swagger`
2. `POST /api/auth/register` ou `/login`
3. Copie o `accessToken`
4. Clique em **Authorize** e informe `Bearer {token}`
5. Teste os endpoints de salas

### Arquivo `.http`

Use `BattleTanks.Backend.http` no VS Code / Rider / Visual Studio.

### Postman

Importe as mesmas rotas e valide:

- `201` no register/create
- `401` no login inválido
- `409` ao entrar em sala cheia
- `204` em deletes

## Migrations (opcional)

```bash
dotnet ef migrations add NomeDaMigration
dotnet ef database update
```

## Notas técnicas (para o relatório PDF)

- Minimal APIs para endpoints simples e diretos (auth/salas/jogadores).
- Controllers tradicionais seriam preferíveis se a superfície crescer muito (filtros complexos, versionamento pesado).
- JWT mantém a API stateless e facilita escala horizontal.
- `409 Conflict` é usado quando a sala está cheia ou não aceita novos jogadores.
- `401 Unauthorized` é usado para credenciais inválidas ou token ausente/inválido.
