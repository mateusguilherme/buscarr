# buscarr

Busca artistas no [MusicBrainz](https://musicbrainz.org) e lista os lançamentos originais de cada um, separados entre estúdio e ao vivo. O nome é uma alusão aos *arr (Sonarr, Radarr, Lidarr…).

## Stack

| Parte | Tecnologias |
|---|---|
| `Buscarr.Server` | .NET 10, ASP.NET Core (Minimal APIs), Entity Framework Core + SQLite, autenticação por cookie |
| `Buscarr.Client` | React 19, TypeScript 6, Vite 8, React Router |

O servidor expõe a API em `/api` e, fora do modo de desenvolvimento, também entrega o build do React.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) 22 ou superior, com npm

## Primeiros passos

```sh
git clone <url-do-repositorio> buscarr
cd buscarr
dotnet tool restore
```

O `dotnet tool restore` instala o `dotnet-ef`, usado para criar migrations. As dependências do React (`npm install`) são instaladas sozinhas no primeiro build do servidor.

## Rodando

### Desenvolvimento (com hot reload)

```sh
cd Buscarr.Server
dotnet watch
```

O servidor sobe em `http://127.0.0.1:3000` e inicia o Vite automaticamente (SpaProxy). Abra `http://127.0.0.1:3000`, que redireciona para o Vite em `http://localhost:5173`. Mudanças no C# são aplicadas pelo `dotnet watch`, e mudanças no React aparecem na hora pelo Vite.

### Sem o Vite

```sh
cd Buscarr.Server
dotnet run --no-launch-profile
```

O build do servidor compila o React em `Buscarr.Server/wwwroot`, e o servidor entrega tudo em `http://127.0.0.1:3000`.

## Como funciona

- **Contas:** cada usuário se cadastra com e-mail, nome e senha (mínimo de 8 caracteres). A senha é guardada como hash (PBKDF2, via `PasswordHasher` do ASP.NET Identity), e a sessão fica num cookie válido por 14 dias.
- **Busca:** devolve o primeiro artista do MusicBrainz cujo nome contém o texto buscado, ignorando maiúsculas e caracteres tipográficos (como o hífen U+2010 em "blink‐182").
- **Lançamentos:** só edições oficiais, filtráveis por tipo (álbuns, EPs, singles ou todos). De cada álbum entra apenas a primeira edição, com data completa. Coletâneas, remixes, demos e trilhas sonoras ficam de fora, e os ao vivo aparecem numa seção própria.
- **MusicBrainz:** as consultas respeitam o limite de 1 requisição por segundo, numa fila compartilhada, e são repetidas até 3 vezes quando a API responde 503. O e-mail de quem está logado vai como contato no `User-Agent`, como a API exige.

## API

| Método | Rota | Login | Descrição |
|---|---|---|---|
| `POST` | `/api/auth/register` | não | Cria a conta e já entra (`{ email, name, password }`) |
| `POST` | `/api/auth/login` | não | Entra (`{ email, password }`) |
| `POST` | `/api/auth/logout` | não | Sai |
| `GET` | `/api/auth/me` | sim | Usuário logado (`{ email, name }`) |
| `GET` | `/api/artists?q=` | sim | Primeiro artista encontrado (`{ id, name }`) |
| `GET` | `/api/artists/{id}/releases?type=` | sim | Nome do artista e lançamentos (`type`: `album`, `ep`, `single` ou `all`) |

## Estrutura

```
buscarr/
├── Buscarr.slnx
├── dotnet-tools.json         ferramentas locais (dotnet-ef)
├── Buscarr.Server/
│   ├── Program.cs            configuração, middleware e registro das rotas
│   ├── Endpoints/            rotas HTTP agrupadas com MapGroup
│   ├── MusicBrainz/          cliente da API do MusicBrainz e modelos
│   ├── Users/                contas e verificação de senha
│   ├── Data/                 DbContext e migrations do EF Core
│   └── Properties/           perfil de lançamento (SpaProxy)
└── Buscarr.Client/
    └── src/                  páginas React e chamadas à API (api.ts)
```

## Banco de dados

O banco é o arquivo `Buscarr.Server/buscarr.db` (SQLite). As migrations pendentes são aplicadas sozinhas quando o servidor inicia.

Para mudar o modelo, altere as entidades ou o `BuscarrDbContext` e crie uma migration:

```sh
cd Buscarr.Server
dotnet ef migrations add NomeDaMudanca -o Data/Migrations
```

## Convenções de código

- Uma linha em branco antes e depois de blocos (`if`, `for`, `while`, `try`…) e antes do último `return` de cada função.
- `async`/`await` no lugar de `.then()`.
- Sem comentários no código.

No `Buscarr.Client`, as regras de linha em branco são verificadas pelo ESLint, que faz parte do build: uma violação faz o `dotnet build` falhar. Para corrigir automaticamente:

```sh
cd Buscarr.Client
npm run lint -- --fix
```

O TypeScript está fixado na versão 6.0 porque o `typescript-eslint` ainda não suporta o TypeScript 7.
