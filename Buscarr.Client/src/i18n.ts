const ptBR = {
  api: {
    loadUserError: (status: number) => `Erro ${status} ao carregar o usuário.`,
    wrongCredentials: 'E-mail ou senha incorretos.',
    loginError: (status: number) => `Erro ${status} ao entrar.`,
    emailTaken: 'Já existe um usuário com esse e-mail.',
    invalidRegistration: 'Preencha todos os campos. A senha precisa ter pelo menos 8 caracteres.',
    registerError: (status: number) => `Erro ${status} ao criar o usuário.`,
    logoutError: (status: number) => `Erro ${status} ao sair.`,
    searchError: (status: number) => `Erro ${status} na busca.`,
    artistError: (status: number) => `Erro ${status} ao carregar o artista.`,
    releasesError: (status: number) => `Erro ${status} ao carregar os releases.`,
  },
  login: {
    email: 'E-mail',
    name: 'Nome',
    password: 'Senha',
    signIn: 'Entrar',
    createUser: 'Criar usuário',
    haveUser: 'Já tenho usuário',
    tagline: 'Encontre o artista certo.',
  },
  search: {
    greeting: (name: string) => `Olá, ${name}`,
    logout: 'Sair',
    artistName: 'Nome do artista',
    search: 'Buscar',
    searching: 'Buscando…',
    notFound: 'Nenhum artista encontrado.',
    error: 'Erro na busca.',
    hint: 'Busque pelo nome do artista.',
  },
  releases: {
    back: 'Voltar',
    albums: 'Álbuns',
    eps: 'EPs',
    singles: 'Singles',
    all: 'Todos',
    loading: 'Carregando…',
    empty: 'Nenhum release oficial encontrado.',
    studio: 'Estúdio',
    live: 'Ao vivo',
    other: 'Outros',
    type: 'Tipo de release',
  },
};

type Messages = typeof ptBR;

const en: Messages = {
  api: {
    loadUserError: (status) => `Error ${status} loading the user.`,
    wrongCredentials: 'Incorrect email or password.',
    loginError: (status) => `Error ${status} signing in.`,
    emailTaken: 'A user with this email already exists.',
    invalidRegistration: 'Fill in all fields. The password must be at least 8 characters long.',
    registerError: (status) => `Error ${status} creating the user.`,
    logoutError: (status) => `Error ${status} signing out.`,
    searchError: (status) => `Error ${status} searching.`,
    artistError: (status) => `Error ${status} loading the artist.`,
    releasesError: (status) => `Error ${status} loading the releases.`,
  },
  login: {
    email: 'Email',
    name: 'Name',
    password: 'Password',
    signIn: 'Sign in',
    createUser: 'Create account',
    haveUser: 'I already have an account',
    tagline: 'Find the right artist.',
  },
  search: {
    greeting: (name) => `Hi, ${name}`,
    logout: 'Sign out',
    artistName: 'Artist name',
    search: 'Search',
    searching: 'Searching…',
    notFound: 'No artist found.',
    error: 'Search failed.',
    hint: "Search by the artist's name.",
  },
  releases: {
    back: 'Back',
    albums: 'Albums',
    eps: 'EPs',
    singles: 'Singles',
    all: 'All',
    loading: 'Loading…',
    empty: 'No official releases found.',
    studio: 'Studio',
    live: 'Live',
    other: 'Other',
    type: 'Release type',
  },
};

export type Locale = 'pt-BR' | 'en';

const messages: Record<Locale, Messages> = { 'pt-BR': ptBR, en };

export function detectLocale(languages: readonly string[]): Locale {
  const language = languages
    .map((tag) => tag.split('-')[0].toLowerCase())
    .find((base) => base === 'pt' || base === 'en');

  return language === 'pt' ? 'pt-BR' : 'en';
}

export const locale = detectLocale(navigator.languages);

export const t = messages[locale];
