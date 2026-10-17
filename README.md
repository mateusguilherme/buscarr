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
git config core.hooksPath .githooks
```

O `dotnet tool restore` instala o `dotnet-ef`, usado para criar migrations. O `git config` ativa o hook de `pre-push`, que roda os testes antes de cada push. As dependências do React (`npm install`) são instaladas sozinhas no primeiro build do servidor.

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
- **Busca:** devolve os artistas do MusicBrainz cujo nome contém o texto buscado, ignorando maiúsculas e caracteres tipográficos (como o hífen U+2010 em "blink‐182"), cada um com a descrição que o MusicBrainz usa para separar homônimos e a área de origem. Dá para filtrar por país e por tipo (banda ou solo). No país, entram também os artistas cuja área é uma cidade ou um estado dele (o índice de busca do MusicBrainz só preenche o país quando a área é o próprio país, então esses são conferidos um a um). Artista sem tipo cadastrado no MusicBrainz continua aparecendo com o filtro de tipo.
- **Lançamentos:** só edições oficiais, filtráveis por tipo (álbuns, EPs, singles ou todos). De cada álbum entra apenas a primeira edição, com data completa. Coletâneas, remixes, demos e trilhas sonoras ficam de fora, e os ao vivo aparecem numa seção própria.
- **Idioma:** a interface sai em português ou em inglês, conforme a preferência do navegador (`navigator.languages`). Qualquer variante de `pt` usa o português, e idiomas sem tradução caem no inglês. Os textos ficam em `Buscarr.Client/src/i18n.ts`, e o nome dos países vem do `Intl.DisplayNames` no mesmo idioma.
- **MusicBrainz:** as consultas respeitam o limite de 1 requisição por segundo, numa fila compartilhada, e são repetidas até 3 vezes quando a API responde 503. O e-mail de quem está logado vai como contato no `User-Agent`, como a API exige.

## API

| Método | Rota | Login | Descrição |
|---|---|---|---|
| `POST` | `/api/auth/register` | não | Cria a conta e já entra (`{ email, name, password }`) |
| `POST` | `/api/auth/login` | não | Entra (`{ email, password }`) |
| `POST` | `/api/auth/logout` | não | Sai |
| `GET` | `/api/auth/me` | sim | Usuário logado (`{ email, name }`) |
| `GET` | `/api/artists?q=&country=&type=` | sim | Artistas encontrados (`[{ id, name, disambiguation, area }]`); `country` é opcional, com o código ISO de 2 letras (ex.: `GB`), e `type` também (`group` para banda, `person` para solo) |
| `GET` | `/api/artists/{id}/releases?type=` | sim | Nome do artista e lançamentos (`type`: `album`, `ep`, `single` ou `all`) |

## Estrutura

```
buscarr/
├── Buscarr.slnx
├── dotnet-tools.json         ferramentas locais (dotnet-ef)
├── Buscarr.Server.Tests/     testes do servidor (xUnit)
├── Buscarr.Server/
│   ├── Program.cs            configuração, middleware e registro das rotas
│   ├── Endpoints/            rotas HTTP agrupadas com MapGroup
│   ├── MusicBrainz/          cliente da API do MusicBrainz e modelos
│   ├── Users/                contas e verificação de senha
│   ├── Data/                 DbContext e migrations do EF Core
│   └── Properties/           perfil de lançamento (SpaProxy)
└── Buscarr.Client/
    └── src/                  páginas React, chamadas à API (api.ts) e traduções (i18n.ts)
```

## Banco de dados

O banco é o arquivo `Buscarr.Server/buscarr.db` (SQLite). As migrations pendentes são aplicadas sozinhas quando o servidor inicia.

Para mudar o modelo, altere as entidades ou o `BuscarrDbContext` e crie uma migration:

```sh
cd Buscarr.Server
dotnet ef migrations add NomeDaMudanca -o Data/Migrations
```

## Testes

```sh
dotnet test                    # servidor, na raiz do repositório
cd Buscarr.Client && npm test  # cliente
```

- **Servidor (`Buscarr.Server.Tests`, xUnit):** testes de unidade do cliente do MusicBrainz, dos filtros de lançamentos e das contas (com SQLite em memória), e testes de integração da API com `WebApplicationFactory`, cada classe com um banco SQLite temporário.
- **Cliente (Vitest + Testing Library):** chamadas da `api.ts`, a escolha do idioma e as telas de login, busca e lançamentos. Os testes rodam com o navegador em português (definido em `test-setup.ts`).

Os testes nunca acessam o MusicBrainz de verdade: as respostas são simuladas por um `HttpMessageHandler` falso, e o intervalo de 1 segundo é controlado por um `FakeTimeProvider`.

O hook `.githooks/pre-push` roda as duas suítes antes de cada `git push` e bloqueia o push se algum teste falhar. Para pular o hook numa emergência, use `git push --no-verify`.

## Convenções de código

- TDD: toda funcionalidade ou correção começa por um teste que falha.
- Uma linha em branco antes e depois de blocos (`if`, `for`, `while`, `try`…) e antes do último `return` de cada função.
- `async`/`await` no lugar de `.then()`.
- Sem comentários no código.

No `Buscarr.Client`, as regras de linha em branco são verificadas pelo ESLint, que faz parte do build: uma violação faz o `dotnet build` falhar. Para corrigir automaticamente:

```sh
cd Buscarr.Client
npm run lint -- --fix
```

O TypeScript está fixado na versão 6.0 porque o `typescript-eslint` ainda não suporta o TypeScript 7.
